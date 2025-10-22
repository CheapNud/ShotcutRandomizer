using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CheapShotcutRandomizer.Services;

/// <summary>
/// Melt-based rendering service for MLT project files
/// IMPORTANT: Uses CPU multi-threading, NOT NVENC (MLT's NVENC is broken/slow)
/// </summary>
public class MeltRenderService
{
    private static readonly Regex ProgressRegex = new(
        @"Current Frame:\s+(\d+),\s+percentage:\s+(\d+)",
        RegexOptions.Compiled
    );

    private readonly string _meltExecutable;

    public MeltRenderService(string meltExecutable = "melt")
    {
        _meltExecutable = meltExecutable;
    }

    /// <summary>
    /// Render an MLT project using CPU multi-threading
    /// DO NOT pass UseHardwareAcceleration=true - MLT's NVENC is 2x SLOWER than CPU
    /// </summary>
    public async Task<bool> RenderAsync(
        string mltFilePath,
        string outputPath,
        MeltRenderSettings settings,
        IProgress<RenderProgress> progress = null,
        CancellationToken cancellationToken = default)
    {
        if (settings.UseHardwareAcceleration)
        {
            Debug.WriteLine("WARNING: Hardware acceleration requested for melt, but it's 2x SLOWER than CPU!");
            Debug.WriteLine("Ignoring UseHardwareAcceleration and using CPU multi-threading instead");
        }

        var arguments = BuildMeltArguments(mltFilePath, outputPath, settings);
        Debug.WriteLine($"melt command: {_meltExecutable} {arguments}");

        var startTime = DateTime.Now;

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _meltExecutable,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        var tcs = new TaskCompletionSource<bool>();

        process.ErrorDataReceived += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            // Parse progress from stderr
            var match = ProgressRegex.Match(e.Data);
            if (match.Success)
            {
                var currentFrame = int.Parse(match.Groups[1].Value);
                var percentage = int.Parse(match.Groups[2].Value);

                progress?.Report(new RenderProgress
                {
                    CurrentFrame = currentFrame,
                    Percentage = percentage,
                    ElapsedTime = DateTime.Now - startTime
                });
            }
            else
            {
                Debug.WriteLine($"melt: {e.Data}");
            }
        };

        process.Exited += (sender, e) =>
        {
            tcs.SetResult(process.ExitCode == 0);
        };

        // Register cancellation
        cancellationToken.Register(() =>
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error killing melt process: {ex.Message}");
                }
            }
        });

        try
        {
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"melt execution error: {ex.Message}");
            return false;
        }
    }

    private string BuildMeltArguments(string mltFile, string outputPath, MeltRenderSettings settings)
    {
        var args = new List<string>();

        // Input MLT file
        args.Add($"\"{mltFile}\"");

        // Progress reporting (use -progress2 for line-by-line output)
        args.Add("-progress2");

        // Consumer and output
        args.Add($"-consumer avformat:\"{outputPath}\"");

        // Video codec - ALWAYS use CPU codec (libx264 or libx265)
        args.Add($"vcodec={settings.VideoCodec}");

        // Audio codec
        args.Add($"acodec={settings.AudioCodec}");

        // Quality settings (CRF for constant quality)
        if (settings.Crf.HasValue)
        {
            args.Add($"crf={settings.Crf.Value}");
        }

        // Encoding preset (speed vs compression)
        if (!string.IsNullOrEmpty(settings.Preset))
        {
            args.Add($"preset={settings.Preset}");
        }

        // Audio bitrate
        if (!string.IsNullOrEmpty(settings.AudioBitrate))
        {
            args.Add($"ab={settings.AudioBitrate}");
        }

        // CRITICAL: CPU multi-threading
        // Use NEGATIVE value to disable frame dropping (for file rendering)
        // Use all available cores for maximum performance
        var threadCount = settings.ThreadCount > 0
            ? settings.ThreadCount
            : Environment.ProcessorCount;

        args.Add($"real_time=-{threadCount}");

        Debug.WriteLine($"Using CPU multi-threading with {threadCount} cores");

        // MP4 optimization
        if (outputPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("movflags=+faststart"); // Enable web streaming
        }

        return string.Join(" ", args);
    }
}

/// <summary>
/// Settings for melt-based rendering
/// IMPORTANT: UseHardwareAcceleration should ALWAYS be false for melt
/// </summary>
public class MeltRenderSettings
{
    /// <summary>
    /// DO NOT SET TO TRUE - MLT's NVENC is broken and 2x slower than CPU
    /// This exists only to document that hardware acceleration should NOT be used
    /// </summary>
    public bool UseHardwareAcceleration { get; set; } = false;

    /// <summary>
    /// Number of CPU threads to use. 0 = auto-detect all cores
    /// For Ryzen 9 5900X: use all 12 cores for maximum performance
    /// </summary>
    public int ThreadCount { get; set; } = 0;

    /// <summary>
    /// Video codec: "libx264" (H.264) or "libx265" (H.265/HEVC)
    /// NEVER use "h264_nvenc" or "hevc_nvenc" with melt
    /// </summary>
    public string VideoCodec { get; set; } = "libx264";

    /// <summary>
    /// Audio codec: "aac", "mp3", etc.
    /// </summary>
    public string AudioCodec { get; set; } = "aac";

    /// <summary>
    /// Encoding preset: ultrafast, superfast, veryfast, faster, fast, medium, slow, slower, veryslow
    /// Recommended: "medium" for good balance, "slow" for better compression
    /// </summary>
    public string Preset { get; set; } = "medium";

    /// <summary>
    /// Constant Rate Factor: 0-51 (lower = better quality)
    /// 18 = visually lossless, 23 = default, 28 = lower quality
    /// </summary>
    public int? Crf { get; set; } = 23;

    /// <summary>
    /// Audio bitrate: "128k", "192k", "256k", etc.
    /// </summary>
    public string AudioBitrate { get; set; } = "128k";
}

public class RenderProgress
{
    public int CurrentFrame { get; set; }
    public int Percentage { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public TimeSpan EstimatedTimeRemaining { get; set; }
}
