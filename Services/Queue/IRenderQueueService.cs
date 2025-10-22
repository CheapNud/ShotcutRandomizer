using CheapShotcutRandomizer.Models;

namespace CheapShotcutRandomizer.Services.Queue;

/// <summary>
/// Interface for render queue service
/// </summary>
public interface IRenderQueueService
{
    // Events for real-time UI updates
    /// <summary>
    /// Event fired when job progress updates
    /// </summary>
    event EventHandler<RenderProgressEventArgs>? ProgressChanged;

    /// <summary>
    /// Event fired when job status changes
    /// </summary>
    event EventHandler<RenderProgressEventArgs>? StatusChanged;

    // Queue management
    /// <summary>
    /// Add a new render job to the queue (alias: EnqueueJobAsync)
    /// </summary>
    Task<Guid> AddJobAsync(RenderJob renderJob);

    /// <summary>
    /// Enqueue a new render job (alias for AddJobAsync)
    /// </summary>
    Task<Guid> EnqueueJobAsync(RenderJob renderJob);

    /// <summary>
    /// Pause a running job
    /// </summary>
    Task<bool> PauseJobAsync(Guid jobId);

    /// <summary>
    /// Resume a paused job
    /// </summary>
    Task<bool> ResumeJobAsync(Guid jobId);

    /// <summary>
    /// Cancel a job
    /// </summary>
    Task<bool> CancelJobAsync(Guid jobId);

    /// <summary>
    /// Retry a failed or dead letter job
    /// </summary>
    Task<bool> RetryJobAsync(Guid jobId);

    // Queue queries
    /// <summary>
    /// Get a job by its ID
    /// </summary>
    Task<RenderJob?> GetJobAsync(Guid jobId);

    /// <summary>
    /// Get all jobs
    /// </summary>
    Task<List<RenderJob>> GetAllJobsAsync();

    /// <summary>
    /// Get all active jobs (Pending, Running, Paused)
    /// </summary>
    Task<List<RenderJob>> GetActiveJobsAsync();

    /// <summary>
    /// Get all completed jobs
    /// </summary>
    Task<List<RenderJob>> GetCompletedJobsAsync();

    /// <summary>
    /// Get all failed jobs
    /// </summary>
    Task<List<RenderJob>> GetFailedJobsAsync();
}
