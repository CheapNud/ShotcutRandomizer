using FFMpegCore;
using FFMpegCore.Enums;
using System.Diagnostics;

namespace CheapShotcutRandomizer.Services;

/// <summary>
/// FFmpeg-based rendering service for direct video encoding
/// IMPORTANT: ABSOLUTELY USE NVENC - it's 8-10x FASTER than CPU encoding
/// Perfect for RIFE frame reassembly workflow
/// </summary>
public class FFmpegRenderService
{
    public FFmpegRenderService()
    {
        // Optional: Set FFmpeg binary path if not in PATH
        // GlobalFFOptions.Configure(new FFOptions { BinaryFolder = @"C:\path\to\ffmpeg" });
    }

    /// <summary>
    /// Reassemble frames into video with audio using NVENC hardware acceleration
    /// RTX 3080: ~500 fps @ 1080p HEVC vs Ryzen 9 5900X: ~30-60 fps
    /// That's 8-10x faster, turning a 4-hour encode into 24-30 minutes
    /// </summary>
    public async Task<bool> ReassembleVideoWithAudioAsync(
        string framesFolder,
        string audioPath,
        string outputPath,
        FFmpegRenderSettings settings,
        IProgress<double> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!settings.UseHardwareAcceleration)
        {
            Debug.WriteLine("WARNING: Hardware acceleration disabled for FFmpeg");
            Debug.WriteLine("With RTX 3080, you're giving up 8-10x speed improvement!");
            Debug.WriteLine("4-hour job will take 4 hours instead of 24-30 minutes");
        }

        var framePattern = Path.Combine(framesFolder, "%06d.png");

        try
        {
            // Detect video duration for progress tracking
            var mediaInfo = await FFProbe.AnalyseAsync(audioPath);

            var inputArgs = FFMpegArguments
                .FromFileInput(framePattern, false, options => options
                    .WithCustomArgument($"-framerate {settings.FrameRate}"))
                .AddFileInput(audioPath);

            // Choose codec based on hardware acceleration
            FFMpegArgumentProcessor processor = settings.UseHardwareAcceleration
                ? BuildHardwareAcceleratedOutput(inputArgs, outputPath, settings)
                : BuildCpuOutput(inputArgs, outputPath, settings);

            // Add progress tracking
            if (progress != null)
            {
                processor = processor.NotifyOnProgress(percentage =>
                {
                    progress.Report(percentage);
                }, mediaInfo.Duration);
            }

            // Process with cancellation support
            await processor.CancellableThrough(cancellationToken).ProcessAsynchronously();

            return true;
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("FFmpeg rendering cancelled");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FFmpeg error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// NVIDIA NVENC hardware-accelerated encoding
    /// RTX 3080: 500+ fps @ 1080p HEVC, 4% CPU usage
    /// THIS IS THE WAY for RIFE workflow
    /// </summary>
    private FFMpegArgumentProcessor BuildHardwareAcceleratedOutput(
        FFMpegArguments arguments,
        string outputPath,
        FFmpegRenderSettings settings)
    {
        Debug.WriteLine("Using NVENC hardware acceleration - expect 8-10x speedup!");

        return arguments.OutputToFile(outputPath, true, options => options
            .WithVideoCodec(settings.VideoCodec) // h264_nvenc or hevc_nvenc
            .WithCustomArgument($"-preset {settings.NvencPreset}") // p1-p7 (p7=best quality)
            .WithCustomArgument($"-rc {settings.RateControl}") // vbr or cq
            .WithCustomArgument($"-cq {settings.Quality}") // Quality level (18-23)
            .WithCustomArgument("-pix_fmt yuv420p") // Compatibility
            .WithCustomArgument("-c:a copy") // Copy audio without re-encoding
            .WithCustomArgument("-shortest") // Match shortest stream
            .WithCustomArgument("-hwaccel cuda") // Enable CUDA decoding
            .WithCustomArgument("-hwaccel_output_format cuda")); // Keep frames in GPU memory
    }

    /// <summary>
    /// CPU-based encoding (fallback if no NVIDIA GPU)
    /// Ryzen 9 5900X: ~30-60 fps @ 1080p HEVC, 100% all cores
    /// Only use this if NVENC is unavailable
    /// </summary>
    private FFMpegArgumentProcessor BuildCpuOutput(
        FFMpegArguments arguments,
        string outputPath,
        FFmpegRenderSettings settings)
    {
        Debug.WriteLine("Using CPU encoding - this will be SLOW compared to NVENC");

        var cpuCodec = settings.VideoCodec switch
        {
            "h264_nvenc" => "libx264",
            "hevc_nvenc" => "libx265",
            _ => "libx264"
        };

        return arguments.OutputToFile(outputPath, true, options => options
            .WithVideoCodec(cpuCodec)
            .WithConstantRateFactor(settings.Quality) // CRF for CPU encoding
            .WithCustomArgument($"-preset {settings.CpuPreset}") // fast/medium/slow
            .WithCustomArgument("-pix_fmt yuv420p")
            .WithCustomArgument("-c:a copy")
            .WithCustomArgument("-shortest"));
    }

    /// <summary>
    /// Extract frames from video (for RIFE pre-processing)
    /// Can use hardware decoding to speed up extraction
    /// </summary>
    public async Task<bool> ExtractFramesAsync(
        string videoPath,
        string outputFolder,
        int fps,
        bool useHardwareDecode = true,
        IProgress<double> progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputFolder);

        try
        {
            var mediaInfo = await FFProbe.AnalyseAsync(videoPath);
            var totalFrames = (int)(mediaInfo.Duration.TotalSeconds * fps);

            var inputArgs = FFMpegArguments.FromFileInput(videoPath, false, options =>
            {
                if (useHardwareDecode)
                {
                    options.WithCustomArgument("-hwaccel cuda");
                }
            });

            FFMpegArgumentProcessor processor = inputArgs.OutputToFile(
                Path.Combine(outputFolder, "frame_%06d.png"),
                true,
                options => options
                    .WithCustomArgument($"-r {fps}")
                    .WithCustomArgument("-qscale:v 1") // Highest quality
            );

            if (progress != null)
            {
                processor = processor.NotifyOnProgress(percentage =>
                {
                    progress.Report(percentage);
                }, mediaInfo.Duration);
            }

            await processor.CancellableThrough(cancellationToken).ProcessAsynchronously();

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Frame extraction error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Extract audio losslessly (for RIFE pre-processing)
    /// </summary>
    public async Task<bool> ExtractAudioAsync(
        string videoPath,
        string audioOutputPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await FFMpegArguments
                .FromFileInput(videoPath)
                .OutputToFile(audioOutputPath, true, options => options
                    .CopyChannel(Channel.Audio) // Lossless copy
                    .DisableChannel(Channel.Video))
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously();

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Audio extraction error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Check if NVENC is available on this system
    /// </summary>
    public async Task<bool> IsNvencAvailableAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-encoders",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var hasNvenc = output.Contains("h264_nvenc") || output.Contains("hevc_nvenc");

            if (hasNvenc)
            {
                Debug.WriteLine("✅ NVENC detected - hardware acceleration available!");
            }
            else
            {
                Debug.WriteLine("❌ NVENC not detected - will use CPU encoding");
            }

            return hasNvenc;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Settings for FFmpeg-based rendering
/// IMPORTANT: UseHardwareAcceleration should be TRUE if you have NVIDIA GPU
/// </summary>
public class FFmpegRenderSettings
{
    /// <summary>
    /// ABSOLUTELY SET TO TRUE if you have RTX 3080
    /// Speed improvement: 8-10x faster than CPU
    /// Example: 4-hour job becomes 24-30 minutes
    /// </summary>
    public bool UseHardwareAcceleration { get; set; } = true;

    /// <summary>
    /// Frame rate for output video
    /// For RIFE 2x interpolation: typically 60fps
    /// </summary>
    public int FrameRate { get; set; } = 60;

    /// <summary>
    /// Video codec when using hardware acceleration
    /// Options: "h264_nvenc" or "hevc_nvenc"
    /// Recommended: "hevc_nvenc" for better compression
    /// </summary>
    public string VideoCodec { get; set; } = "hevc_nvenc";

    /// <summary>
    /// NVENC preset: p1 (fastest) to p7 (slowest/best quality)
    /// Recommended: p7 for maximum quality (still WAY faster than CPU)
    /// RTX 3080 can handle p7 at 500+ fps
    /// </summary>
    public string NvencPreset { get; set; } = "p7";

    /// <summary>
    /// Rate control mode: "vbr" (variable bitrate) or "cq" (constant quality)
    /// Recommended: "vbr" for general use
    /// </summary>
    public string RateControl { get; set; } = "vbr";

    /// <summary>
    /// Quality level: 0-51 (lower = better quality)
    /// For NVENC: 18-23 recommended
    /// 19 = visually lossless
    /// </summary>
    public int Quality { get; set; } = 19;

    /// <summary>
    /// CPU preset (only used if hardware acceleration disabled)
    /// Options: ultrafast, superfast, veryfast, faster, fast, medium, slow, slower, veryslow
    /// </summary>
    public string CpuPreset { get; set; } = "medium";
}
