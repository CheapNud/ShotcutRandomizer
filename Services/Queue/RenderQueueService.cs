using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Text.Json;
using CheapShotcutRandomizer.Models;
using CheapShotcutRandomizer.Data.Repositories;
using Polly;
using Polly.Retry;

namespace CheapShotcutRandomizer.Services.Queue;

/// <summary>
/// Main render queue service - processes render jobs in the background
/// </summary>
public class RenderQueueService : BackgroundService, IRenderQueueService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly SemaphoreSlim _concurrencyLimit;
    private readonly int _maxConcurrentRenders;
    private readonly Dictionary<Guid, CancellationTokenSource> _runningJobs = new();
    private readonly object _runningJobsLock = new();
    private readonly ResiliencePipeline _retryPipeline;

    public event EventHandler<RenderProgressEventArgs>? ProgressChanged;
    public event EventHandler<RenderProgressEventArgs>? StatusChanged;

    public RenderQueueService(
        IServiceProvider serviceProvider,
        IBackgroundTaskQueue taskQueue,
        int maxConcurrentRenders = 1)
    {
        _serviceProvider = serviceProvider;
        _taskQueue = taskQueue;
        _maxConcurrentRenders = maxConcurrentRenders;
        _concurrencyLimit = new SemaphoreSlim(_maxConcurrentRenders, _maxConcurrentRenders);

        // Configure retry policy with exponential backoff
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 1, // We handle retries manually in ProcessJobAsync, set to 1 to satisfy Polly validation
                ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .Build();

        Debug.WriteLine($"RenderQueueService initialized with max {_maxConcurrentRenders} concurrent renders");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Debug.WriteLine("RenderQueueService starting...");

        // Perform crash recovery on startup
        await RecoverCrashedJobsAsync();

        // Main processing loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Dequeue the next work item
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                // Wait for available slot (semaphore controls concurrency)
                await _concurrencyLimit.WaitAsync(stoppingToken);

                // Execute work item in background (don't await - allows concurrent processing)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await workItem(stoppingToken);
                    }
                    finally
                    {
                        _concurrencyLimit.Release();
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("RenderQueueService stopping...");
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in RenderQueueService main loop: {ex.Message}");
            }
        }

        Debug.WriteLine("RenderQueueService stopped");
    }

    public async Task<Guid> AddJobAsync(RenderJob renderJob)
    {
        return await EnqueueJobAsync(renderJob);
    }

    public async Task<Guid> EnqueueJobAsync(RenderJob renderJob)
    {
        // Add job to database
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRenderJobRepository>();

        await repository.AddAsync(renderJob);

        // Queue the work item
        await _taskQueue.QueueBackgroundWorkItemAsync(async ct =>
        {
            await ProcessJobAsync(renderJob.JobId, ct);
        });

        FireStatusChanged(renderJob.JobId, RenderJobStatus.Pending, 0, 0);

        Debug.WriteLine($"Enqueued job {renderJob.JobId}");
        return renderJob.JobId;
    }

    public async Task<List<RenderJob>> GetCompletedJobsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRenderJobRepository>();
        return await repository.GetByStatusAsync(RenderJobStatus.Completed);
    }

    public async Task<List<RenderJob>> GetFailedJobsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRenderJobRepository>();

        // Get both Failed and DeadLetter jobs
        var failed = await repository.GetByStatusAsync(RenderJobStatus.Failed);
        var deadLetter = await repository.GetByStatusAsync(RenderJobStatus.DeadLetter);

        return failed.Concat(deadLetter).OrderByDescending(j => j.CreatedAt).ToList();
    }

    public async Task<RenderJob?> GetJobAsync(Guid jobId)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRenderJobRepository>();
        return await repository.GetAsync(jobId);
    }

    public async Task<List<RenderJob>> GetAllJobsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRenderJobRepository>();
        return await repository.GetAllAsync();
    }

    public async Task<List<RenderJob>> GetActiveJobsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRenderJobRepository>();
        return await repository.GetActiveJobsAsync();
    }

    public async Task<bool> CancelJobAsync(Guid jobId)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRenderJobRepository>();

        var renderJob = await repository.GetAsync(jobId);
        if (renderJob == null)
            return false;

        // Cancel running job
        lock (_runningJobsLock)
        {
            if (_runningJobs.TryGetValue(jobId, out var cts))
            {
                cts.Cancel();
                _runningJobs.Remove(jobId);
            }
        }

        // Update status
        renderJob.Status = RenderJobStatus.Cancelled;
        renderJob.CompletedAt = DateTime.UtcNow;
        await repository.UpdateAsync(renderJob);

        FireStatusChanged(jobId, RenderJobStatus.Cancelled, renderJob.ProgressPercentage, renderJob.CurrentFrame);

        Debug.WriteLine($"Cancelled job {jobId}");
        return true;
    }

    public async Task<bool> PauseJobAsync(Guid jobId)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRenderJobRepository>();

        var renderJob = await repository.GetAsync(jobId);
        if (renderJob == null || renderJob.Status != RenderJobStatus.Running)
            return false;

        // Cancel the running job (will be handled as pause)
        lock (_runningJobsLock)
        {
            if (_runningJobs.TryGetValue(jobId, out var cts))
            {
                cts.Cancel();
                _runningJobs.Remove(jobId);
            }
        }

        renderJob.Status = RenderJobStatus.Paused;
        await repository.UpdateAsync(renderJob);

        FireStatusChanged(jobId, RenderJobStatus.Paused, renderJob.ProgressPercentage, renderJob.CurrentFrame);

        Debug.WriteLine($"Paused job {jobId}");
        return true;
    }

    public async Task<bool> ResumeJobAsync(Guid jobId)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRenderJobRepository>();

        var renderJob = await repository.GetAsync(jobId);
        if (renderJob == null || renderJob.Status != RenderJobStatus.Paused)
            return false;

        // Reset to pending and re-enqueue
        renderJob.Status = RenderJobStatus.Pending;
        await repository.UpdateAsync(renderJob);

        await _taskQueue.QueueBackgroundWorkItemAsync(async ct =>
        {
            await ProcessJobAsync(jobId, ct);
        });

        FireStatusChanged(jobId, RenderJobStatus.Pending, renderJob.ProgressPercentage, renderJob.CurrentFrame);

        Debug.WriteLine($"Resumed job {jobId}");
        return true;
    }

    public async Task<bool> RetryJobAsync(Guid jobId)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRenderJobRepository>();

        var renderJob = await repository.GetAsync(jobId);
        if (renderJob == null)
            return false;

        if (renderJob.Status != RenderJobStatus.Failed && renderJob.Status != RenderJobStatus.DeadLetter)
            return false;

        // Reset job for retry
        renderJob.Status = RenderJobStatus.Pending;
        renderJob.RetryCount = 0;
        renderJob.ProgressPercentage = 0;
        renderJob.CurrentFrame = 0;
        renderJob.LastError = null;
        renderJob.ErrorStackTrace = null;
        await repository.UpdateAsync(renderJob);

        await _taskQueue.QueueBackgroundWorkItemAsync(async ct =>
        {
            await ProcessJobAsync(jobId, ct);
        });

        FireStatusChanged(jobId, RenderJobStatus.Pending, 0, 0);

        Debug.WriteLine($"Retrying job {jobId}");
        return true;
    }

    private async Task ProcessJobAsync(Guid jobId, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRenderJobRepository>();

        RenderJob? renderJob = null;
        CancellationTokenSource? jobCts = null;

        try
        {
            // Get the job
            renderJob = await repository.GetAsync(jobId);
            if (renderJob == null)
            {
                Debug.WriteLine($"Job {jobId} not found");
                return;
            }

            // Skip if not pending
            if (renderJob.Status != RenderJobStatus.Pending)
            {
                Debug.WriteLine($"Job {jobId} is not pending (status: {renderJob.Status})");
                return;
            }

            // Create cancellation token for this job
            jobCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            lock (_runningJobsLock)
            {
                _runningJobs[jobId] = jobCts;
            }

            // Update status to running
            renderJob.Status = RenderJobStatus.Running;
            renderJob.StartedAt = DateTime.UtcNow;
            renderJob.ProcessId = Environment.ProcessId;
            renderJob.MachineName = Environment.MachineName;
            await repository.UpdateAsync(renderJob);

            FireStatusChanged(jobId, RenderJobStatus.Running, 0, 0);

            // Deserialize render settings
            var settings = JsonSerializer.Deserialize<MeltRenderSettings>(renderJob.RenderSettings);
            if (settings == null)
            {
                throw new InvalidOperationException("Failed to deserialize render settings");
            }

            // Create progress reporter
            var startTime = DateTime.UtcNow;
            var lastProgressUpdate = DateTime.UtcNow;
            var progress = new Progress<RenderProgress>(renderProgress =>
            {
                // Throttle database updates to every 1 second
                var now = DateTime.UtcNow;
                if ((now - lastProgressUpdate).TotalSeconds >= 1)
                {
                    lastProgressUpdate = now;

                    // Update database (fire and forget for performance)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var updateScope = _serviceProvider.CreateScope();
                            var updateRepo = updateScope.ServiceProvider.GetRequiredService<IRenderJobRepository>();
                            await updateRepo.UpdateProgressAsync(jobId, renderProgress.Percentage, renderProgress.CurrentFrame);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error updating progress: {ex.Message}");
                        }
                    });
                }

                // Always fire progress event (UI can decide how to handle)
                var elapsed = now - startTime;
                TimeSpan? remaining = null;
                if (renderProgress.Percentage > 0)
                {
                    var totalEstimated = elapsed.TotalSeconds / (renderProgress.Percentage / 100.0);
                    remaining = TimeSpan.FromSeconds(totalEstimated - elapsed.TotalSeconds);
                }

                FireProgressChanged(jobId, RenderJobStatus.Running, renderProgress.Percentage,
                    renderProgress.CurrentFrame, null, elapsed, remaining);
            });

            // Execute the render
            var meltService = new MeltRenderService();
            var renderSuccess = await meltService.RenderAsync(
                renderJob.SourceVideoPath,
                renderJob.OutputPath,
                settings,
                progress,
                jobCts.Token);

            // Update final status
            if (renderSuccess)
            {
                renderJob.Status = RenderJobStatus.Completed;
                renderJob.ProgressPercentage = 100;
                renderJob.CompletedAt = DateTime.UtcNow;
                await repository.UpdateAsync(renderJob);

                FireStatusChanged(jobId, RenderJobStatus.Completed, 100, renderJob.CurrentFrame);
                Debug.WriteLine($"Job {jobId} completed successfully");
            }
            else
            {
                throw new Exception("Render failed (melt returned non-zero exit code)");
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"Job {jobId} was cancelled");
            // Status already updated by CancelJobAsync or PauseJobAsync
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Job {jobId} failed: {ex.Message}");

            if (renderJob != null)
            {
                renderJob.LastError = ex.Message;
                renderJob.ErrorStackTrace = ex.StackTrace;
                renderJob.RetryCount++;

                // Determine if we should retry or move to dead letter
                if (renderJob.RetryCount >= renderJob.MaxRetries)
                {
                    renderJob.Status = RenderJobStatus.DeadLetter;
                    renderJob.CompletedAt = DateTime.UtcNow;
                    await repository.UpdateAsync(renderJob);

                    FireStatusChanged(jobId, RenderJobStatus.DeadLetter, renderJob.ProgressPercentage,
                        renderJob.CurrentFrame, ex.Message);

                    Debug.WriteLine($"Job {jobId} moved to dead letter queue after {renderJob.RetryCount} retries");
                }
                else
                {
                    // Retry with exponential backoff
                    renderJob.Status = RenderJobStatus.Pending;
                    await repository.UpdateAsync(renderJob);

                    var delaySeconds = Math.Pow(2, renderJob.RetryCount);
                    Debug.WriteLine($"Job {jobId} will retry in {delaySeconds} seconds (attempt {renderJob.RetryCount}/{renderJob.MaxRetries})");

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);

                    // Re-enqueue
                    await _taskQueue.QueueBackgroundWorkItemAsync(async ct =>
                    {
                        await ProcessJobAsync(jobId, ct);
                    });

                    FireStatusChanged(jobId, RenderJobStatus.Pending, renderJob.ProgressPercentage,
                        renderJob.CurrentFrame, $"Retry {renderJob.RetryCount}/{renderJob.MaxRetries}");
                }
            }
        }
        finally
        {
            // Clean up cancellation token
            lock (_runningJobsLock)
            {
                _runningJobs.Remove(jobId);
            }
            jobCts?.Dispose();
        }
    }

    private async Task RecoverCrashedJobsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IRenderJobRepository>();

            var crashedJobs = await repository.GetCrashedJobsAsync(
                Environment.ProcessId,
                Environment.MachineName);

            if (crashedJobs.Count == 0)
            {
                Debug.WriteLine("No crashed jobs found");
                return;
            }

            Debug.WriteLine($"Found {crashedJobs.Count} crashed jobs, recovering...");

            foreach (var crashedJob in crashedJobs)
            {
                crashedJob.Status = RenderJobStatus.Pending;
                crashedJob.RetryCount++;
                crashedJob.ProcessId = null;
                crashedJob.MachineName = null;
                crashedJob.LastError = "Job recovered after process crash";

                // Move to dead letter if too many retries
                if (crashedJob.RetryCount >= crashedJob.MaxRetries)
                {
                    crashedJob.Status = RenderJobStatus.DeadLetter;
                    crashedJob.CompletedAt = DateTime.UtcNow;
                }

                await repository.UpdateAsync(crashedJob);

                // Re-enqueue if still pending
                if (crashedJob.Status == RenderJobStatus.Pending)
                {
                    await _taskQueue.QueueBackgroundWorkItemAsync(async ct =>
                    {
                        await ProcessJobAsync(crashedJob.JobId, ct);
                    });
                }

                Debug.WriteLine($"Recovered crashed job {crashedJob.JobId}, status: {crashedJob.Status}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during crash recovery: {ex.Message}");
        }
    }

    private void FireProgressChanged(Guid jobId, RenderJobStatus status, double percentage,
        int currentFrame, int? totalFrames, TimeSpan? elapsed, TimeSpan? remaining)
    {
        ProgressChanged?.Invoke(this, new RenderProgressEventArgs
        {
            JobId = jobId,
            Status = status,
            ProgressPercentage = percentage,
            CurrentFrame = currentFrame,
            TotalFrames = totalFrames ?? 0,
            ElapsedTime = elapsed,
            EstimatedTimeRemaining = remaining
        });
    }

    private void FireStatusChanged(Guid jobId, RenderJobStatus status, double percentage,
        int currentFrame, string? errorMessage = null)
    {
        StatusChanged?.Invoke(this, new RenderProgressEventArgs
        {
            JobId = jobId,
            Status = status,
            ProgressPercentage = percentage,
            CurrentFrame = currentFrame,
            ErrorMessage = errorMessage
        });
    }
}
