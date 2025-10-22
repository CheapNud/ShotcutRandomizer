using CheapShotcutRandomizer.Services.Utilities;
using FFMpegCore;
using System.Diagnostics;

namespace CheapShotcutRandomizer.Services.RIFE;

/// <summary>
/// Complete RIFE video processing pipeline
/// Orchestrates: Extract Audio → Extract Frames → RIFE Interpolation → Reassemble Video
/// </summary>
public class RifeVideoProcessingPipeline
{
    private readonly FFmpegRenderService _ffmpegService;
    private readonly RifeInterpolationService _rifeService;
    private readonly HardwareDetectionService _hardwareService;
    private readonly VideoValidator _videoValidator;
    private readonly FFmpegErrorHandler _errorHandler;

    public RifeVideoProcessingPipeline(
        FFmpegRenderService ffmpegService,
        RifeInterpolationService rifeService,
        HardwareDetectionService hardwareService,
        VideoValidator videoValidator,
        FFmpegErrorHandler errorHandler)
    {
        _ffmpegService = ffmpegService;
        _rifeService = rifeService;
        _hardwareService = hardwareService;
        _videoValidator = videoValidator;
        _errorHandler = errorHandler;
    }

    /// <summary>
    /// Process video through complete RIFE pipeline
    /// </summary>
    public async Task<bool> ProcessVideoAsync(
        string inputPath,
        string outputPath,
        RifePipelineOptions options,
        IProgress<VideoProcessingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Debug.WriteLine("=== Starting RIFE Video Processing Pipeline ===");
        Debug.WriteLine($"Input: {inputPath}");
        Debug.WriteLine($"Output: {outputPath}");
        Debug.WriteLine($"Interpolation: {options.RifeOptions.GetFrameMultiplier()}x ({options.RifeOptions.InterpolationPasses} passes)");

        using var tempManager = new TemporaryFileManager();

        try
        {
            // Stage 1: Analyze input video
            progress?.Report(VideoProcessingProgress.Create(
                VideoProcessingProgress.ProcessingStage.Analyzing, 0));

            var validation = await AnalyzeInputAsync(inputPath, cancellationToken);
            if (!validation.IsValid)
            {
                Debug.WriteLine($"Input validation failed: {validation.ErrorMessage}");
                return false;
            }

            progress?.Report(VideoProcessingProgress.Create(
                VideoProcessingProgress.ProcessingStage.Analyzing, 100));

            // Determine input FPS
            var inputFps = options.InputFps ?? (int)Math.Round(validation.FrameRate);
            var outputFps = options.CalculateOutputFps(inputFps);

            Debug.WriteLine($"Input FPS: {inputFps}, Output FPS: {outputFps}");

            // Auto-detect FFmpeg settings if not provided
            if (options.FFmpegSettings == null)
            {
                Debug.WriteLine("Auto-detecting optimal FFmpeg settings for RTX 3080...");
                options.FFmpegSettings = await _hardwareService.GetOptimalFFmpegSettingsAsync(outputFps);
            }
            else
            {
                options.FFmpegSettings.FrameRate = outputFps;
            }

            // Create temporary directories
            var audioPath = tempManager.CreateTempFile("audio.m4a");
            var inputFramesDir = tempManager.CreateTempDirectory("input_frames");
            var outputFramesDir = tempManager.CreateTempDirectory("output_frames");

            // Stage 2: Extract audio
            var hasAudio = validation.HasAudio;
            if (hasAudio)
            {
                if (!await ExtractAudioAsync(inputPath, audioPath, progress, cancellationToken))
                    return false;
            }
            else
            {
                Debug.WriteLine("No audio track found - skipping audio extraction");
            }

            // Stage 3: Extract frames
            if (!await ExtractFramesAsync(
                inputPath,
                inputFramesDir,
                inputFps,
                validation.Duration,
                options,
                progress,
                cancellationToken))
                return false;

            // Stage 4: Validate frame extraction
            var expectedFrameCount = await _videoValidator.CalculateExpectedFrameCountAsync(inputPath, inputFps);
            if (options.ValidateFrameCounts)
            {
                var frameValidation = _videoValidator.ValidateFrameSequence(inputFramesDir, expectedFrameCount);
                if (!frameValidation.IsValid)
                {
                    Debug.WriteLine($"Frame extraction validation failed: {frameValidation.ErrorMessage}");
                    return false;
                }
            }

            // Stage 5: RIFE interpolation
            if (!await InterpolateFramesAsync(
                inputFramesDir,
                outputFramesDir,
                options,
                progress,
                cancellationToken))
                return false;

            // Stage 6: Validate interpolation
            var expectedInterpolatedFrames = expectedFrameCount * options.RifeOptions.GetFrameMultiplier();
            if (options.ValidateFrameCounts)
            {
                var interpolationValidation = _videoValidator.ValidateFrameSequence(
                    outputFramesDir,
                    expectedInterpolatedFrames);

                if (!interpolationValidation.IsValid)
                {
                    Debug.WriteLine($"Interpolation validation failed: {interpolationValidation.ErrorMessage}");
                    return false;
                }
            }

            // Stage 7: Reassemble video
            if (!await ReassembleVideoAsync(
                outputFramesDir,
                hasAudio ? audioPath : null,
                outputPath,
                validation.Duration,
                options.FFmpegSettings,
                progress,
                cancellationToken))
                return false;

            // Stage 8: Validate output
            var outputValidation = await _videoValidator.ValidateOutputVideoAsync(
                outputPath,
                validation.Duration,
                toleranceSeconds: 2.0);

            if (!outputValidation.IsValid)
            {
                Debug.WriteLine($"Output validation failed: {outputValidation.ErrorMessage}");
                return false;
            }

            // Complete
            progress?.Report(VideoProcessingProgress.Create(
                VideoProcessingProgress.ProcessingStage.Complete, 100));

            Debug.WriteLine("=== RIFE Pipeline Completed Successfully ===");
            Debug.WriteLine($"Output: {outputPath}");
            Debug.WriteLine($"Size: {TemporaryFileManager.FormatSize(new FileInfo(outputPath).Length)}");

            // Cleanup temporary files
            if (!options.KeepTemporaryFiles)
            {
                tempManager.Cleanup();
            }
            else
            {
                Debug.WriteLine($"Temporary files kept at: {tempManager.BaseDirectory}");
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("RIFE pipeline cancelled by user");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RIFE pipeline error: {ex.Message}");
            Debug.WriteLine(ex.StackTrace);
            return false;
        }
    }

    /// <summary>
    /// Stage 1: Analyze input video
    /// </summary>
    private async Task<VideoValidationResult> AnalyzeInputAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        Debug.WriteLine("Stage 1: Analyzing input video...");
        return await _videoValidator.ValidateInputVideoAsync(inputPath);
    }

    /// <summary>
    /// Stage 2: Extract audio track
    /// </summary>
    private async Task<bool> ExtractAudioAsync(
        string inputPath,
        string audioPath,
        IProgress<VideoProcessingProgress>? progress,
        CancellationToken cancellationToken)
    {
        Debug.WriteLine("Stage 2: Extracting audio...");

        progress?.Report(VideoProcessingProgress.Create(
            VideoProcessingProgress.ProcessingStage.ExtractingAudio, 0));

        var success = await _ffmpegService.ExtractAudioAsync(
            inputPath,
            audioPath,
            cancellationToken);

        if (!success)
        {
            Debug.WriteLine("Failed to extract audio");
            return false;
        }

        progress?.Report(VideoProcessingProgress.Create(
            VideoProcessingProgress.ProcessingStage.ExtractingAudio, 100));

        Debug.WriteLine($"Audio extracted: {audioPath}");
        return true;
    }

    /// <summary>
    /// Stage 3: Extract frames from video
    /// </summary>
    private async Task<bool> ExtractFramesAsync(
        string inputPath,
        string framesDir,
        int fps,
        TimeSpan duration,
        RifePipelineOptions options,
        IProgress<VideoProcessingProgress>? progress,
        CancellationToken cancellationToken)
    {
        Debug.WriteLine($"Stage 3: Extracting frames at {fps} fps...");

        var stageProgress = new Progress<double>(percentage =>
        {
            progress?.Report(VideoProcessingProgress.Create(
                VideoProcessingProgress.ProcessingStage.ExtractingFrames,
                percentage));
        });

        var success = await _ffmpegService.ExtractFramesAsync(
            inputPath,
            framesDir,
            fps,
            options.UseHardwareDecode,
            stageProgress,
            cancellationToken);

        if (!success)
        {
            Debug.WriteLine("Failed to extract frames");
            return false;
        }

        var frameCount = Directory.GetFiles(framesDir, "*.png").Length;
        Debug.WriteLine($"Extracted {frameCount} frames to {framesDir}");

        return true;
    }

    /// <summary>
    /// Stage 4: RIFE frame interpolation
    /// </summary>
    private async Task<bool> InterpolateFramesAsync(
        string inputFramesDir,
        string outputFramesDir,
        RifePipelineOptions options,
        IProgress<VideoProcessingProgress>? progress,
        CancellationToken cancellationToken)
    {
        Debug.WriteLine($"Stage 4: RIFE interpolation ({options.RifeOptions.GetFrameMultiplier()}x)...");

        var stageProgress = new Progress<double>(percentage =>
        {
            progress?.Report(VideoProcessingProgress.Create(
                VideoProcessingProgress.ProcessingStage.InterpolatingFrames,
                percentage));
        });

        var success = await _rifeService.InterpolateFramesAsync(
            inputFramesDir,
            outputFramesDir,
            options.RifeOptions,
            stageProgress,
            cancellationToken);

        if (!success)
        {
            Debug.WriteLine("Failed to interpolate frames");
            return false;
        }

        var outputFrameCount = Directory.GetFiles(outputFramesDir, "*.png").Length;
        Debug.WriteLine($"Interpolated to {outputFrameCount} frames in {outputFramesDir}");

        return true;
    }

    /// <summary>
    /// Stage 5: Reassemble video with NVENC
    /// </summary>
    private async Task<bool> ReassembleVideoAsync(
        string framesDir,
        string? audioPath,
        string outputPath,
        TimeSpan duration,
        FFmpegRenderSettings settings,
        IProgress<VideoProcessingProgress>? progress,
        CancellationToken cancellationToken)
    {
        Debug.WriteLine("Stage 5: Reassembling video with NVENC...");

        var stageProgress = new Progress<double>(percentage =>
        {
            progress?.Report(VideoProcessingProgress.Create(
                VideoProcessingProgress.ProcessingStage.ReassemblingVideo,
                percentage));
        });

        bool success;

        if (!string.IsNullOrEmpty(audioPath) && File.Exists(audioPath))
        {
            // Reassemble with audio
            success = await _ffmpegService.ReassembleVideoWithAudioAsync(
                framesDir,
                audioPath,
                outputPath,
                settings,
                stageProgress,
                cancellationToken);
        }
        else
        {
            // Reassemble without audio (frames only)
            success = await ReassembleVideoWithoutAudioAsync(
                framesDir,
                outputPath,
                settings,
                stageProgress,
                cancellationToken);
        }

        if (!success)
        {
            Debug.WriteLine("Failed to reassemble video");
            return false;
        }

        Debug.WriteLine($"Video reassembled: {outputPath}");
        return true;
    }

    /// <summary>
    /// Reassemble video without audio track
    /// </summary>
    private async Task<bool> ReassembleVideoWithoutAudioAsync(
        string framesFolder,
        string outputPath,
        FFmpegRenderSettings settings,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var framePattern = Path.Combine(framesFolder, "%06d.png");

        try
        {
            var inputArguments = FFMpegCore.FFMpegArguments
                .FromFileInput(framePattern, false, options => options
                    .WithCustomArgument($"-framerate {settings.FrameRate}"));

            FFMpegArgumentProcessor processor;

            // Choose codec based on hardware acceleration
            if (settings.UseHardwareAcceleration)
            {
                processor = inputArguments.OutputToFile(outputPath, true, options => options
                    .WithVideoCodec(settings.VideoCodec)
                    .WithCustomArgument($"-preset {settings.NvencPreset}")
                    .WithCustomArgument($"-rc {settings.RateControl}")
                    .WithCustomArgument($"-cq {settings.Quality}")
                    .WithCustomArgument("-pix_fmt yuv420p")
                    .WithCustomArgument("-hwaccel cuda")
                    .WithCustomArgument("-hwaccel_output_format cuda"));
            }
            else
            {
                var cpuCodec = settings.VideoCodec switch
                {
                    "h264_nvenc" => "libx264",
                    "hevc_nvenc" => "libx265",
                    _ => "libx264"
                };

                processor = inputArguments.OutputToFile(outputPath, true, options => options
                    .WithVideoCodec(cpuCodec)
                    .WithConstantRateFactor(settings.Quality)
                    .WithCustomArgument($"-preset {settings.CpuPreset}")
                    .WithCustomArgument("-pix_fmt yuv420p"));
            }

            await processor.CancellableThrough(cancellationToken).ProcessAsynchronously();

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FFmpeg error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get estimated processing time
    /// </summary>
    public async Task<TimeSpan> EstimateProcessingTimeAsync(
        string inputPath,
        RifePipelineOptions options)
    {
        var validation = await _videoValidator.ValidateInputVideoAsync(inputPath);
        if (!validation.IsValid)
            return TimeSpan.Zero;

        var hw = await _hardwareService.DetectHardwareAsync();

        // Rough estimates based on RTX 3080 + Ryzen 9 5900X
        // Frame extraction: ~5x realtime
        // RIFE interpolation: ~0.5x realtime (slow)
        // NVENC reassembly: ~10x realtime

        var extractionTime = validation.Duration.TotalSeconds / 5.0;
        var rifeTime = validation.Duration.TotalSeconds / 0.5;
        var reassemblyTime = hw.NvencAvailable
            ? validation.Duration.TotalSeconds / 10.0
            : validation.Duration.TotalSeconds * 2.0; // CPU encoding is slower

        var totalSeconds = extractionTime + rifeTime + reassemblyTime;
        return TimeSpan.FromSeconds(totalSeconds);
    }
}
