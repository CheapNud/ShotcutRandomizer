using Bunit;
using Bunit.TestDoubles;
using CheapShotcutRandomizer.Components.Pages;
using CheapShotcutRandomizer.Data.Repositories;
using CheapShotcutRandomizer.Models;
using CheapShotcutRandomizer.Services;
using CheapShotcutRandomizer.Services.Queue;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MudBlazor;
using MudBlazor.Services;

namespace CheapShotcutRandomizer.Tests.Components;

/// <summary>
/// Unit tests for RenderQueue.razor component using BUnit
/// Tests cover component rendering, job management, queue control, and real-time updates
/// </summary>
public class RenderQueueTests : TestContext
{
    private readonly Mock<IRenderQueueService> _mockQueueService;
    private readonly Mock<IRenderJobRepository> _mockRepository;
    private readonly Mock<HardwareDetectionService> _mockHardwareService;
    private readonly Mock<SvpDetectionService> _mockSvpDetection;

    public RenderQueueTests()
    {
        // Setup mocks
        _mockQueueService = new Mock<IRenderQueueService>();
        _mockRepository = new Mock<IRenderJobRepository>();
        _mockSvpDetection = new Mock<SvpDetectionService>();
        _mockHardwareService = new Mock<HardwareDetectionService>(_mockSvpDetection.Object);

        // Register services
        Services.AddMudServices();
        Services.AddSingleton(_mockQueueService.Object);
        Services.AddSingleton(_mockRepository.Object);
        Services.AddSingleton(_mockHardwareService.Object);

        // Setup JSInterop for MudBlazor components
        JSInterop.Mode = JSInteropMode.Loose;

        // Setup mock defaults
        _mockQueueService.Setup(x => x.IsQueuePaused).Returns(true);
        _mockQueueService.Setup(x => x.GetActiveJobsAsync())
            .ReturnsAsync(new List<RenderJob>());
        _mockQueueService.Setup(x => x.GetCompletedJobsAsync())
            .ReturnsAsync(new List<RenderJob>());
        _mockQueueService.Setup(x => x.GetFailedJobsAsync())
            .ReturnsAsync(new List<RenderJob>());
        _mockQueueService.Setup(x => x.GetQueueStatisticsAsync())
            .ReturnsAsync(new QueueStatistics());
        _mockHardwareService.Setup(x => x.DetectHardwareAsync())
            .ReturnsAsync(new HardwareCapabilities
            {
                CpuCoreCount = 8,
                CpuName = "Test CPU",
                NvencAvailable = true
            });
    }

    [Fact]
    public void RenderQueue_Renders_Successfully()
    {
        // Arrange & Act
        var component = RenderComponent<RenderQueue>();

        // Assert
        component.Should().NotBeNull();
        component.Find("h4").TextContent.Should().Be("Render Queue");
    }

    [Fact]
    public void RenderQueue_Shows_HardwareInfo_When_Loaded()
    {
        // Arrange
        var hardware = new HardwareCapabilities
        {
            CpuCoreCount = 16,
            CpuName = "AMD Ryzen 9 5950X",
            NvencAvailable = true,
            GpuName = "NVIDIA RTX 3090"
        };
        _mockHardwareService.Setup(x => x.DetectHardwareAsync()).ReturnsAsync(hardware);

        // Act
        var component = RenderComponent<RenderQueue>();
        component.WaitForState(() => !component.Instance.GetType()
            .GetField("_isLoadingHardware", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(component.Instance)!.Equals(true), TimeSpan.FromSeconds(2));

        // Assert
        component.Markup.Should().Contain("AMD Ryzen 9 5950X");
    }

    [Fact]
    public void RenderQueue_Displays_Queue_Paused_Status()
    {
        // Arrange
        _mockQueueService.Setup(x => x.IsQueuePaused).Returns(true);

        // Act
        var component = RenderComponent<RenderQueue>();

        // Assert
        component.Markup.Should().Contain("Queue PAUSED");
        component.Markup.Should().Contain("Jobs will not start processing until queue is started");
    }

    [Fact]
    public void RenderQueue_Displays_Queue_Running_Status()
    {
        // Arrange
        _mockQueueService.Setup(x => x.IsQueuePaused).Returns(false);

        // Act
        var component = RenderComponent<RenderQueue>();

        // Assert
        component.Markup.Should().Contain("Queue RUNNING");
        component.Markup.Should().Contain("Jobs will start processing automatically");
    }

    [Fact]
    public void RenderQueue_Shows_Active_Jobs()
    {
        // Arrange
        var activeJobs = new List<RenderJob>
        {
            new() { JobId = Guid.NewGuid(), SourceVideoPath = "test1.mlt", Status = RenderJobStatus.Running, ProgressPercentage = 45.0 },
            new() { JobId = Guid.NewGuid(), SourceVideoPath = "test2.mlt", Status = RenderJobStatus.Pending }
        };
        _mockQueueService.Setup(x => x.GetActiveJobsAsync()).ReturnsAsync(activeJobs);

        // Act
        var component = RenderComponent<RenderQueue>();
        component.WaitForAssertion(() => component.Markup.Should().Contain("test1.mlt"));

        // Assert
        component.Markup.Should().Contain("test1.mlt");
        component.Markup.Should().Contain("test2.mlt");
    }

    [Fact]
    public void RenderQueue_Shows_Empty_State_When_No_Active_Jobs()
    {
        // Arrange
        _mockQueueService.Setup(x => x.GetActiveJobsAsync()).ReturnsAsync(new List<RenderJob>());

        // Act
        var component = RenderComponent<RenderQueue>();
        component.WaitForAssertion(() => component.Markup.Should().Contain("Ready to render!"));

        // Assert
        component.Markup.Should().Contain("Ready to render!");
        component.Markup.Should().Contain("Click \"Add Job\" to get started");
    }

    [Fact]
    public void RenderQueue_Shows_Completed_Jobs()
    {
        // Arrange
        var completedJobs = new List<RenderJob>
        {
            new()
            {
                JobId = Guid.NewGuid(),
                SourceVideoPath = "completed1.mlt",
                Status = RenderJobStatus.Completed,
                ProgressPercentage = 100,
                OutputFileSizeBytes = 1024 * 1024 * 500 // 500 MB
            }
        };
        _mockQueueService.Setup(x => x.GetCompletedJobsAsync()).ReturnsAsync(completedJobs);

        // Act
        var component = RenderComponent<RenderQueue>();

        // Assert - component should load completed jobs
        _mockQueueService.Verify(x => x.GetCompletedJobsAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public void RenderQueue_Shows_Failed_Jobs()
    {
        // Arrange
        var failedJobs = new List<RenderJob>
        {
            new()
            {
                JobId = Guid.NewGuid(),
                SourceVideoPath = "failed1.mlt",
                Status = RenderJobStatus.Failed,
                LastError = "FFmpeg error: invalid codec"
            }
        };
        _mockQueueService.Setup(x => x.GetFailedJobsAsync()).ReturnsAsync(failedJobs);

        // Act
        var component = RenderComponent<RenderQueue>();

        // Assert
        _mockQueueService.Verify(x => x.GetFailedJobsAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public void RenderQueue_Calls_StartQueue_When_Start_Button_Clicked()
    {
        // Arrange
        _mockQueueService.Setup(x => x.IsQueuePaused).Returns(true);
        var component = RenderComponent<RenderQueue>();

        // Act
        var startButton = component.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Start Queue"));
        startButton.Should().NotBeNull();
        startButton!.Click();

        // Assert
        _mockQueueService.Verify(x => x.StartQueue(), Times.Once);
    }

    [Fact]
    public void RenderQueue_Calls_StopQueue_When_Pause_Button_Clicked()
    {
        // Arrange
        _mockQueueService.Setup(x => x.IsQueuePaused).Returns(false);
        var component = RenderComponent<RenderQueue>();

        // Act
        var pauseButton = component.FindAll("button").FirstOrDefault(b => b.TextContent.Contains("Pause Queue"));
        pauseButton.Should().NotBeNull();
        pauseButton!.Click();

        // Assert
        _mockQueueService.Verify(x => x.StopQueue(), Times.Once);
    }

    [Fact]
    public async Task RenderQueue_Calls_AddJobAsync_When_Job_Added()
    {
        // Arrange
        var newJob = new RenderJob
        {
            JobId = Guid.NewGuid(),
            SourceVideoPath = "new_job.mlt",
            OutputPath = "output.mp4"
        };
        _mockQueueService.Setup(x => x.AddJobAsync(It.IsAny<RenderJob>()))
            .ReturnsAsync(newJob.JobId);

        var component = RenderComponent<RenderQueue>();

        // Act
        await component.InvokeAsync(async () =>
        {
            var handleJobAddedMethod = component.Instance.GetType()
                .GetMethod("HandleJobAdded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            await (Task)handleJobAddedMethod!.Invoke(component.Instance, new object[] { newJob })!;
        });

        // Assert
        _mockQueueService.Verify(x => x.AddJobAsync(It.Is<RenderJob>(j => j.JobId == newJob.JobId)), Times.Once);
    }

    [Fact]
    public async Task RenderQueue_Calls_PauseJobAsync_When_Job_Paused()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _mockQueueService.Setup(x => x.PauseJobAsync(jobId)).ReturnsAsync(true);

        var component = RenderComponent<RenderQueue>();

        // Act
        await component.InvokeAsync(async () =>
        {
            var handlePauseMethod = component.Instance.GetType()
                .GetMethod("HandlePause", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            await (Task)handlePauseMethod!.Invoke(component.Instance, new object[] { jobId })!;
        });

        // Assert
        _mockQueueService.Verify(x => x.PauseJobAsync(jobId), Times.Once);
    }

    [Fact]
    public async Task RenderQueue_Calls_CancelJobAsync_When_Job_Cancelled()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _mockQueueService.Setup(x => x.CancelJobAsync(jobId)).ReturnsAsync(true);

        var component = RenderComponent<RenderQueue>();

        // Act
        await component.InvokeAsync(async () =>
        {
            var handleCancelMethod = component.Instance.GetType()
                .GetMethod("HandleCancel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            await (Task)handleCancelMethod!.Invoke(component.Instance, new object[] { jobId })!;
        });

        // Assert
        _mockQueueService.Verify(x => x.CancelJobAsync(jobId), Times.Once);
    }

    [Fact]
    public async Task RenderQueue_Calls_RetryJobAsync_When_Job_Retried()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _mockQueueService.Setup(x => x.RetryJobAsync(jobId)).ReturnsAsync(true);

        var component = RenderComponent<RenderQueue>();

        // Act
        await component.InvokeAsync(async () =>
        {
            var handleRetryMethod = component.Instance.GetType()
                .GetMethod("HandleRetry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            await (Task)handleRetryMethod!.Invoke(component.Instance, new object[] { jobId })!;
        });

        // Assert
        _mockQueueService.Verify(x => x.RetryJobAsync(jobId), Times.Once);
    }

    [Fact]
    public async Task RenderQueue_Calls_DeleteAsync_When_Job_Deleted()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        _mockRepository.Setup(x => x.DeleteAsync(jobId)).Returns(Task.CompletedTask);

        var component = RenderComponent<RenderQueue>();

        // Act
        await component.InvokeAsync(async () =>
        {
            var handleDeleteMethod = component.Instance.GetType()
                .GetMethod("HandleDelete", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            await (Task)handleDeleteMethod!.Invoke(component.Instance, new object[] { jobId })!;
        });

        // Assert
        _mockRepository.Verify(x => x.DeleteAsync(jobId), Times.Once);
    }

    [Fact]
    public void RenderQueue_Displays_Queue_Statistics()
    {
        // Arrange
        var stats = new QueueStatistics
        {
            PendingCount = 5,
            RunningCount = 2,
            CompletedCount = 10,
            FailedCount = 1
        };
        _mockQueueService.Setup(x => x.GetQueueStatisticsAsync()).ReturnsAsync(stats);

        // Act
        var component = RenderComponent<RenderQueue>();
        component.WaitForAssertion(() => component.Markup.Should().Contain("5 pending"));

        // Assert
        component.Markup.Should().Contain("5 pending");
        component.Markup.Should().Contain("2 running");
    }

    [Fact]
    public void RenderQueue_Unsubscribes_From_Events_On_Disposal()
    {
        // Arrange
        var component = RenderComponent<RenderQueue>();

        // Act
        component.Dispose();

        // Assert - component should dispose cleanly without errors
        // This test verifies that event unsubscription doesn't throw
        component.Should().NotBeNull();
    }
}
