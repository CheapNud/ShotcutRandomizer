using CheapShotcutRandomizer.Services;

namespace CheapShotcutRandomizer.Models;

/// <summary>
/// Represents a video render job in the queue
/// </summary>
public class RenderJob
{
    /// <summary>
    /// Database primary key
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Unique identifier for this job (used for external references)
    /// </summary>
    public Guid JobId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Path to the source MLT project file
    /// </summary>
    public string SourceVideoPath { get; set; } = string.Empty;

    /// <summary>
    /// Path where the rendered video will be saved
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Serialized JSON of MeltRenderSettings
    /// </summary>
    public string RenderSettings { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the job
    /// </summary>
    public RenderJobStatus Status { get; set; } = RenderJobStatus.Pending;

    /// <summary>
    /// When the job was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the job was added to the queue
    /// </summary>
    public DateTime? QueuedAt { get; set; }

    /// <summary>
    /// When rendering started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the job finished (completed, failed, or cancelled)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Last time the job was updated (used for progress updates)
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current progress percentage (0-100)
    /// </summary>
    public double ProgressPercentage { get; set; }

    /// <summary>
    /// Current frame being rendered
    /// </summary>
    public int CurrentFrame { get; set; }

    /// <summary>
    /// Total frames to render (null if not yet known)
    /// </summary>
    public int? TotalFrames { get; set; }

    /// <summary>
    /// Estimated time remaining for this job
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// Type of render (MLT Project or RIFE Interpolation)
    /// </summary>
    public RenderType RenderType { get; set; } = RenderType.MltProject;

    /// <summary>
    /// MLT-specific render settings (nullable)
    /// NOT MAPPED to database - use RenderSettings JSON property instead
    /// </summary>
    public MeltRenderSettings? MeltSettings { get; set; }

    /// <summary>
    /// FFmpeg-specific render settings (nullable)
    /// NOT MAPPED to database - use RenderSettings JSON property instead
    /// </summary>
    public FFmpegRenderSettings? FFmpegSettings { get; set; }

    /// <summary>
    /// RIFE-specific settings (nullable)
    /// NOT MAPPED to database - use RenderSettings JSON property instead
    /// </summary>
    public RifeSettings? RifeSettings { get; set; }

    /// <summary>
    /// Number of times this job has been retried
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Maximum number of retry attempts before moving to dead letter
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Last error message (if job failed)
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Last error stack trace (if job failed)
    /// </summary>
    public string? ErrorStackTrace { get; set; }

    /// <summary>
    /// Process ID that claimed this job (for crash recovery)
    /// </summary>
    public int? ProcessId { get; set; }

    /// <summary>
    /// Machine name that claimed this job (for crash recovery)
    /// </summary>
    public string? MachineName { get; set; }
}
