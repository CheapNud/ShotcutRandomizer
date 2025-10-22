namespace CheapShotcutRandomizer.Services.RIFE;

/// <summary>
/// Progress tracking for video processing pipeline
/// </summary>
public class VideoProcessingProgress
{
    public enum ProcessingStage
    {
        Analyzing,
        ExtractingAudio,
        ExtractingFrames,
        InterpolatingFrames,
        ReassemblingVideo,
        Complete
    }

    /// <summary>
    /// Current processing stage
    /// </summary>
    public ProcessingStage CurrentStage { get; set; }

    /// <summary>
    /// Progress within current stage (0-100)
    /// </summary>
    public double StageProgress { get; set; }

    /// <summary>
    /// Overall progress across all stages (0-100)
    /// </summary>
    public double OverallProgress => CalculateOverallProgress();

    /// <summary>
    /// Estimated time remaining (if available)
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }

    /// <summary>
    /// Current stage description
    /// </summary>
    public string CurrentStageDescription => CurrentStage switch
    {
        ProcessingStage.Analyzing => "Analyzing input video",
        ProcessingStage.ExtractingAudio => "Extracting audio track",
        ProcessingStage.ExtractingFrames => "Extracting frames from video",
        ProcessingStage.InterpolatingFrames => "Interpolating frames with RIFE AI",
        ProcessingStage.ReassemblingVideo => "Reassembling video with NVENC",
        ProcessingStage.Complete => "Processing complete",
        _ => "Processing"
    };

    /// <summary>
    /// Calculate overall progress based on stage weights
    /// </summary>
    private double CalculateOverallProgress()
    {
        // Stage weight distribution:
        // Analyzing: 0-2%
        // ExtractingAudio: 2-5%
        // ExtractingFrames: 5-20%
        // InterpolatingFrames: 20-80% (60% of total - this is the slow part)
        // ReassemblingVideo: 80-100%
        // Complete: 100%

        return CurrentStage switch
        {
            ProcessingStage.Analyzing => 0 + (StageProgress * 0.02),
            ProcessingStage.ExtractingAudio => 2 + (StageProgress * 0.03),
            ProcessingStage.ExtractingFrames => 5 + (StageProgress * 0.15),
            ProcessingStage.InterpolatingFrames => 20 + (StageProgress * 0.60),
            ProcessingStage.ReassemblingVideo => 80 + (StageProgress * 0.20),
            ProcessingStage.Complete => 100,
            _ => 0
        };
    }

    /// <summary>
    /// Create a progress update for a specific stage
    /// </summary>
    public static VideoProcessingProgress Create(ProcessingStage stage, double stageProgress = 0)
    {
        return new VideoProcessingProgress
        {
            CurrentStage = stage,
            StageProgress = Math.Clamp(stageProgress, 0, 100)
        };
    }

    public override string ToString()
    {
        return $"{CurrentStageDescription}: {StageProgress:F1}% (Overall: {OverallProgress:F1}%)";
    }
}
