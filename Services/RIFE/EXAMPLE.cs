// Example usage of RIFE Video Processing Pipeline
// This file demonstrates all major use cases

using CheapShotcutRandomizer.Services;
using CheapShotcutRandomizer.Services.RIFE;
using CheapShotcutRandomizer.Services.Utilities;

namespace CheapShotcutRandomizer.Examples;

/// <summary>
/// Complete examples for RIFE video interpolation
/// </summary>
public class RifeExamples
{
    private readonly RifeVideoProcessingPipeline _pipeline;
    private readonly RifeInterpolationService _rifeService;
    private readonly VideoValidator _validator;

    public RifeExamples(
        RifeVideoProcessingPipeline pipeline,
        RifeInterpolationService rifeService,
        VideoValidator validator)
    {
        _pipeline = pipeline;
        _rifeService = rifeService;
        _validator = validator;
    }

    /// <summary>
    /// Example 1: Basic 30fps → 60fps interpolation with auto-detected settings
    /// </summary>
    public async Task<bool> Example1_BasicInterpolation()
    {
        // Create default options (auto-detects RTX 3080)
        var options = RifePipelineOptions.CreateDefault();

        // Simple progress tracking
        var progress = new Progress<VideoProcessingProgress>(p =>
        {
            Console.WriteLine($"[{p.OverallProgress:F1}%] {p.CurrentStageDescription}");
        });

        // Process video
        var success = await _pipeline.ProcessVideoAsync(
            inputPath: @"C:\Videos\input.mp4",
            outputPath: @"C:\Videos\output_60fps.mp4",
            options: options,
            progress: progress,
            cancellationToken: CancellationToken.None
        );

        return success;
    }

    /// <summary>
    /// Example 2: High quality interpolation with custom settings
    /// </summary>
    public async Task<bool> Example2_HighQualityInterpolation()
    {
        // Start with high quality preset
        var options = RifePipelineOptions.CreateHighQuality();

        // Customize RIFE settings
        options.RifeOptions.ModelName = "rife-v4.22"; // Latest model
        options.RifeOptions.TtaMode = true; // Better quality (slower)
        options.RifeOptions.InterpolationPasses = 1; // 2x interpolation

        // Customize FFmpeg settings for best quality
        options.FFmpegSettings = new FFmpegRenderSettings
        {
            UseHardwareAcceleration = true,
            VideoCodec = "hevc_nvenc",
            NvencPreset = "p7", // Best quality
            RateControl = "vbr",
            Quality = 18, // Near-lossless
            FrameRate = 60
        };

        // Detailed progress tracking
        var progress = new Progress<VideoProcessingProgress>(p =>
        {
            Console.WriteLine($"Stage: {p.CurrentStage}");
            Console.WriteLine($"Progress: {p.StageProgress:F1}% (Overall: {p.OverallProgress:F1}%)");
            Console.WriteLine($"Description: {p.CurrentStageDescription}");
            Console.WriteLine("---");
        });

        var success = await _pipeline.ProcessVideoAsync(
            @"C:\Videos\input.mp4",
            @"C:\Videos\output_high_quality.mp4",
            options,
            progress
        );

        return success;
    }

    /// <summary>
    /// Example 3: Fast processing for quick previews
    /// </summary>
    public async Task<bool> Example3_FastProcessing()
    {
        // Use fast preset
        var options = RifePipelineOptions.CreateFast();

        // Further optimize for speed
        options.RifeOptions.ModelName = "rife-v4.15-lite"; // Lighter model
        options.RifeOptions.TtaMode = false; // Faster
        options.FFmpegSettings.NvencPreset = "p4"; // Faster preset

        var progress = new Progress<VideoProcessingProgress>(p =>
        {
            Console.WriteLine($"[{p.OverallProgress:F1}%] {p.CurrentStageDescription}");
        });

        var success = await _pipeline.ProcessVideoAsync(
            @"C:\Videos\input.mp4",
            @"C:\Videos\output_fast.mp4",
            options,
            progress
        );

        return success;
    }

    /// <summary>
    /// Example 4: 4x interpolation (30fps → 120fps)
    /// </summary>
    public async Task<bool> Example4_QuadrupleFrameRate()
    {
        var options = RifePipelineOptions.CreateDefault();

        // Set interpolation passes to 2 (2^2 = 4x)
        options.RifeOptions.InterpolationPasses = 2;
        // 30fps input → 120fps output

        var progress = new Progress<VideoProcessingProgress>(p =>
        {
            Console.WriteLine($"[{p.OverallProgress:F1}%] {p.CurrentStageDescription}");
        });

        var success = await _pipeline.ProcessVideoAsync(
            @"C:\Videos\input_30fps.mp4",
            @"C:\Videos\output_120fps.mp4",
            options,
            progress
        );

        return success;
    }

    /// <summary>
    /// Example 5: Processing with cancellation support
    /// </summary>
    public async Task<bool> Example5_WithCancellation()
    {
        var options = RifePipelineOptions.CreateDefault();

        // Create cancellation token source
        using var cts = new CancellationTokenSource();

        // Cancel after 10 minutes
        cts.CancelAfter(TimeSpan.FromMinutes(10));

        // Or cancel from button/event:
        // cancelButton.Click += (s, e) => cts.Cancel();

        var progress = new Progress<VideoProcessingProgress>(p =>
        {
            Console.WriteLine($"[{p.OverallProgress:F1}%] {p.CurrentStageDescription}");
        });

        try
        {
            var success = await _pipeline.ProcessVideoAsync(
                @"C:\Videos\input.mp4",
                @"C:\Videos\output.mp4",
                options,
                progress,
                cts.Token
            );

            if (success)
            {
                Console.WriteLine("Processing completed successfully!");
            }
            else
            {
                Console.WriteLine("Processing failed or was cancelled.");
            }

            return success;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Processing was cancelled by user.");
            return false;
        }
    }

    /// <summary>
    /// Example 6: Processing 4K/UHD content
    /// </summary>
    public async Task<bool> Example6_UHDProcessing()
    {
        var options = RifePipelineOptions.CreateDefault();

        // Enable UHD mode for 4K content
        options.RifeOptions.UhMode = true;
        options.RifeOptions.ModelName = "rife-UHD"; // UHD-optimized model

        // Reduce tile size to fit in VRAM (RTX 3080 = 10GB)
        options.RifeOptions.TileSize = 512;

        // Use HEVC for better compression on large files
        options.FFmpegSettings = new FFmpegRenderSettings
        {
            UseHardwareAcceleration = true,
            VideoCodec = "hevc_nvenc",
            NvencPreset = "p7",
            Quality = 20 // Slightly higher quality for UHD
        };

        var progress = new Progress<VideoProcessingProgress>(p =>
        {
            Console.WriteLine($"[{p.OverallProgress:F1}%] {p.CurrentStageDescription}");
        });

        var success = await _pipeline.ProcessVideoAsync(
            @"C:\Videos\input_4k.mp4",
            @"C:\Videos\output_4k_60fps.mp4",
            options,
            progress
        );

        return success;
    }

    /// <summary>
    /// Example 7: Keep temporary files for debugging
    /// </summary>
    public async Task<bool> Example7_KeepTemporaryFiles()
    {
        var options = RifePipelineOptions.CreateDefault();

        // Keep temp files for debugging
        options.KeepTemporaryFiles = true;

        var progress = new Progress<VideoProcessingProgress>(p =>
        {
            Console.WriteLine($"[{p.OverallProgress:F1}%] {p.CurrentStageDescription}");
        });

        var success = await _pipeline.ProcessVideoAsync(
            @"C:\Videos\input.mp4",
            @"C:\Videos\output.mp4",
            options,
            progress
        );

        if (success)
        {
            Console.WriteLine("Temporary files kept in %TEMP%\\ShotcutRandomizer\\");
            Console.WriteLine("Check input_frames\\ and output_frames\\ folders");
        }

        return success;
    }

    /// <summary>
    /// Example 8: Estimate processing time before starting
    /// </summary>
    public async Task Example8_EstimateTime()
    {
        var inputPath = @"C:\Videos\input.mp4";
        var options = RifePipelineOptions.CreateDefault();

        // Estimate processing time
        var estimatedTime = await _pipeline.EstimateProcessingTimeAsync(inputPath, options);

        Console.WriteLine($"Estimated processing time: {estimatedTime.TotalMinutes:F1} minutes");
        Console.WriteLine($"For a faster estimate, use CreateFast() preset");

        var fastOptions = RifePipelineOptions.CreateFast();
        var fastEstimate = await _pipeline.EstimateProcessingTimeAsync(inputPath, fastOptions);

        Console.WriteLine($"Fast preset estimate: {fastEstimate.TotalMinutes:F1} minutes");
    }

    /// <summary>
    /// Example 9: Validate input before processing
    /// </summary>
    public async Task<bool> Example9_ValidateBeforeProcessing()
    {
        var inputPath = @"C:\Videos\input.mp4";

        // Validate input video first
        var validation = await _validator.ValidateInputVideoAsync(inputPath);

        if (!validation.IsValid)
        {
            Console.WriteLine($"Input validation failed: {validation.ErrorMessage}");
            return false;
        }

        Console.WriteLine("Input video is valid:");
        Console.WriteLine($"  Duration: {validation.Duration}");
        Console.WriteLine($"  Resolution: {validation.Width}x{validation.Height}");
        Console.WriteLine($"  Frame Rate: {validation.FrameRate} fps");
        Console.WriteLine($"  Has Audio: {validation.HasAudio}");
        Console.WriteLine($"  File Size: {TemporaryFileManager.FormatSize(validation.FileSize)}");

        // Calculate output info
        var options = RifePipelineOptions.CreateDefault();
        var outputFps = options.CalculateOutputFps((int)Math.Round(validation.FrameRate));

        Console.WriteLine($"\nOutput will be:");
        Console.WriteLine($"  Frame Rate: {outputFps} fps");
        Console.WriteLine($"  Multiplier: {options.RifeOptions.GetFrameMultiplier()}x");

        // Proceed with processing
        var progress = new Progress<VideoProcessingProgress>(p =>
        {
            Console.WriteLine($"[{p.OverallProgress:F1}%] {p.CurrentStageDescription}");
        });

        return await _pipeline.ProcessVideoAsync(
            inputPath,
            @"C:\Videos\output.mp4",
            options,
            progress
        );
    }

    /// <summary>
    /// Example 10: Check if RIFE is available before using
    /// </summary>
    public async Task<bool> Example10_CheckAvailability()
    {
        // Check if RIFE executable is available
        var rifeAvailable = _rifeService.IsRifeAvailable();

        if (!rifeAvailable)
        {
            Console.WriteLine("ERROR: RIFE executable not found!");
            Console.WriteLine("Download from: https://github.com/nihui/rife-ncnn-vulkan/releases");
            Console.WriteLine("Place rife-ncnn-vulkan.exe in application directory or PATH");
            return false;
        }

        Console.WriteLine("RIFE is available!");

        // List available models
        var models = RifeInterpolationService.GetAvailableModels();
        Console.WriteLine("\nAvailable RIFE models:");
        foreach (var model in models)
        {
            Console.WriteLine($"  - {model}");
        }

        // Proceed with processing
        var options = RifePipelineOptions.CreateDefault();
        var progress = new Progress<VideoProcessingProgress>(p =>
        {
            Console.WriteLine($"[{p.OverallProgress:F1}%] {p.CurrentStageDescription}");
        });

        return await _pipeline.ProcessVideoAsync(
            @"C:\Videos\input.mp4",
            @"C:\Videos\output.mp4",
            options,
            progress
        );
    }

    /// <summary>
    /// Example 11: Batch processing multiple videos
    /// </summary>
    public async Task<Dictionary<string, bool>> Example11_BatchProcessing()
    {
        var inputVideos = new[]
        {
            @"C:\Videos\video1.mp4",
            @"C:\Videos\video2.mp4",
            @"C:\Videos\video3.mp4"
        };

        var options = RifePipelineOptions.CreateDefault();
        var batchResults = new Dictionary<string, bool>();

        foreach (var inputPath in inputVideos)
        {
            var outputPath = Path.ChangeExtension(inputPath, "_60fps.mp4");

            Console.WriteLine($"\n=== Processing: {Path.GetFileName(inputPath)} ===");

            var progress = new Progress<VideoProcessingProgress>(p =>
            {
                Console.WriteLine($"[{p.OverallProgress:F1}%] {p.CurrentStageDescription}");
            });

            var success = await _pipeline.ProcessVideoAsync(
                inputPath,
                outputPath,
                options,
                progress
            );

            batchResults[inputPath] = success;

            if (success)
            {
                Console.WriteLine($"✓ Completed: {Path.GetFileName(outputPath)}");
            }
            else
            {
                Console.WriteLine($"✗ Failed: {Path.GetFileName(inputPath)}");
            }
        }

        // Print summary
        Console.WriteLine("\n=== Batch Processing Summary ===");
        var successCount = batchResults.Values.Count(s => s);
        Console.WriteLine($"Successful: {successCount}/{batchResults.Count}");

        return batchResults;
    }

    /// <summary>
    /// Example 12: Custom RIFE path configuration
    /// </summary>
    public async Task<bool> Example12_CustomRifePath()
    {
        // If RIFE is not in PATH, specify custom path
        var customRifeService = new RifeInterpolationService(
            @"C:\Tools\rife-ncnn-vulkan\rife-ncnn-vulkan.exe"
        );

        // Check availability
        if (!customRifeService.IsRifeAvailable())
        {
            Console.WriteLine("RIFE not found at custom path!");
            return false;
        }

        // Note: You'll need to manually create the pipeline with custom service
        // This example shows how to configure custom paths
        Console.WriteLine("RIFE found at custom path!");

        return true;
    }
}
