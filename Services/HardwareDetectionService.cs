using System.Diagnostics;
using System.Management;

namespace CheapShotcutRandomizer.Services;

/// <summary>
/// Detects hardware capabilities and provides optimal rendering settings
/// Automatically configures for RTX 3080 + Ryzen 9 5900X setup
/// </summary>
public class HardwareDetectionService
{
    private HardwareCapabilities? _cachedCapabilities;

    /// <summary>
    /// Detect hardware capabilities (cached after first call)
    /// </summary>
    public async Task<HardwareCapabilities> DetectHardwareAsync()
    {
        if (_cachedCapabilities != null)
            return _cachedCapabilities;

        var capabilities = new HardwareCapabilities
        {
            CpuCoreCount = Environment.ProcessorCount,
            CpuName = GetCpuName(),
            HasNvidiaGpu = await DetectNvidiaGpuAsync(),
            GpuName = GetGpuName(),
            NvencAvailable = await IsNvencAvailableAsync()
        };

        Debug.WriteLine("=== Hardware Detection ===");
        Debug.WriteLine($"CPU: {capabilities.CpuName} ({capabilities.CpuCoreCount} cores)");
        Debug.WriteLine($"GPU: {capabilities.GpuName}");
        Debug.WriteLine($"NVIDIA GPU: {capabilities.HasNvidiaGpu}");
        Debug.WriteLine($"NVENC Available: {capabilities.NvencAvailable}");
        Debug.WriteLine($"Recommended for melt: CPU multi-threading");
        Debug.WriteLine($"Recommended for FFmpeg: {(capabilities.NvencAvailable ? "NVENC (8-10x faster!)" : "CPU encoding")}");
        Debug.WriteLine("========================");

        _cachedCapabilities = capabilities;
        return capabilities;
    }

    /// <summary>
    /// Get optimal settings for melt rendering
    /// ALWAYS uses CPU multi-threading (MLT's NVENC is broken)
    /// </summary>
    public async Task<MeltRenderSettings> GetOptimalMeltSettingsAsync()
    {
        var hw = await DetectHardwareAsync();

        var settings = new MeltRenderSettings
        {
            // NEVER use hardware acceleration for melt (it's 2x slower)
            UseHardwareAcceleration = false,

            // Use all CPU cores
            ThreadCount = hw.CpuCoreCount,

            // Good balance of speed/quality
            VideoCodec = "libx264",
            Preset = "medium",
            Crf = 23,
            AudioCodec = "aac",
            AudioBitrate = "128k"
        };

        Debug.WriteLine($"Optimal melt settings: CPU multi-threading with {settings.ThreadCount} cores");

        return settings;
    }

    /// <summary>
    /// Get optimal settings for FFmpeg rendering (RIFE workflow)
    /// Uses NVENC if available (8-10x faster than CPU)
    /// </summary>
    public async Task<FFmpegRenderSettings> GetOptimalFFmpegSettingsAsync(int targetFps = 60)
    {
        var hw = await DetectHardwareAsync();

        var settings = new FFmpegRenderSettings
        {
            FrameRate = targetFps,
            UseHardwareAcceleration = hw.NvencAvailable
        };

        if (hw.NvencAvailable)
        {
            // RTX 3080 settings - maximum quality at insane speed
            settings.VideoCodec = "hevc_nvenc";
            settings.NvencPreset = "p7"; // Best quality (still 500+ fps on RTX 3080)
            settings.RateControl = "vbr";
            settings.Quality = 19; // Visually lossless
        }
        else
        {
            // CPU fallback
            settings.VideoCodec = "libx265";
            settings.CpuPreset = "medium";
            settings.Quality = 23;
        }

        var speedDescription = hw.NvencAvailable
            ? "NVENC hardware acceleration (8-10x faster than CPU!)"
            : "CPU encoding (no NVIDIA GPU detected)";

        Debug.WriteLine($"Optimal FFmpeg settings: {speedDescription}");

        return settings;
    }

    /// <summary>
    /// Get recommended settings for high-quality output
    /// </summary>
    public async Task<FFmpegRenderSettings> GetHighQualityFFmpegSettingsAsync(int targetFps = 60)
    {
        var settings = await GetOptimalFFmpegSettingsAsync(targetFps);

        if (settings.UseHardwareAcceleration)
        {
            settings.Quality = 18; // Even higher quality
            settings.NvencPreset = "p7"; // Already at max
        }
        else
        {
            settings.CpuPreset = "slow"; // Slower but better compression
            settings.Quality = 20;
        }

        return settings;
    }

    /// <summary>
    /// Get recommended settings for fast encoding (draft quality)
    /// </summary>
    public async Task<FFmpegRenderSettings> GetFastFFmpegSettingsAsync(int targetFps = 60)
    {
        var settings = await GetOptimalFFmpegSettingsAsync(targetFps);

        if (settings.UseHardwareAcceleration)
        {
            settings.NvencPreset = "p4"; // Faster (still excellent quality)
            settings.Quality = 23;
        }
        else
        {
            settings.CpuPreset = "fast";
            settings.Quality = 26;
        }

        return settings;
    }

    private async Task<bool> DetectNvidiaGpuAsync()
    {
        try
        {
            // Check for NVIDIA GPU using WMI
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            var results = searcher.Get();

            foreach (ManagementObject obj in results)
            {
                var name = obj["Name"]?.ToString() ?? "";
                if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // Fallback: check if nvidia-smi exists
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "--query-gpu=name --format=csv,noheader",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process != null)
                {
                    await process.WaitForExitAsync();
                    return process.ExitCode == 0;
                }
            }
            catch { }

            return false;
        }
    }

    private string GetGpuName()
    {
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            var results = searcher.Get();

            foreach (ManagementObject obj in results)
            {
                var name = obj["Name"]?.ToString() ?? "";
                if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }

            // Return first GPU if no NVIDIA found
            foreach (ManagementObject obj in results)
            {
                return obj["Name"]?.ToString() ?? "Unknown GPU";
            }

            return "Unknown GPU";
        }
        catch
        {
            return "Unknown GPU";
        }
    }

    private string GetCpuName()
    {
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            var results = searcher.Get();

            foreach (ManagementObject obj in results)
            {
                return obj["Name"]?.ToString() ?? "Unknown CPU";
            }

            return "Unknown CPU";
        }
        catch
        {
            return "Unknown CPU";
        }
    }

    private async Task<bool> IsNvencAvailableAsync()
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

            return output.Contains("h264_nvenc") || output.Contains("hevc_nvenc");
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Detected hardware capabilities
/// </summary>
public class HardwareCapabilities
{
    public int CpuCoreCount { get; set; }
    public string CpuName { get; set; } = "Unknown";
    public bool HasNvidiaGpu { get; set; }
    public string GpuName { get; set; } = "Unknown";
    public bool NvencAvailable { get; set; }

    // Recommendations based on hardware
    public bool ShouldUseMeltNvenc => false; // NEVER - MLT's NVENC is broken
    public bool ShouldUseFFmpegNvenc => NvencAvailable; // ALWAYS if available

    /// <summary>
    /// Estimated speedup when using NVENC vs CPU for FFmpeg
    /// Based on RTX 3080 benchmarks: 500fps vs 30-60fps
    /// </summary>
    public double NvencSpeedupFactor => NvencAvailable ? 8.5 : 1.0;

    /// <summary>
    /// Estimate render time for given duration
    /// </summary>
    public TimeSpan EstimateFFmpegRenderTime(TimeSpan videoDuration, bool useNvenc)
    {
        // Baseline: CPU encoding is roughly 1:1 (1 hour video = 1 hour to encode)
        // with fast preset, or 2:1 with medium preset
        var baselineMultiplier = 2.0;

        if (useNvenc && NvencAvailable)
        {
            // NVENC is 8-10x faster, so 1 hour video = 6-7 minutes
            baselineMultiplier /= NvencSpeedupFactor;
        }

        return TimeSpan.FromTicks((long)(videoDuration.Ticks * baselineMultiplier));
    }

    /// <summary>
    /// Get human-readable time savings
    /// </summary>
    public string GetTimeSavingsDescription(TimeSpan videoDuration)
    {
        if (!NvencAvailable)
            return "No NVIDIA GPU - using CPU encoding";

        var cpuTime = EstimateFFmpegRenderTime(videoDuration, useNvenc: false);
        var nvencTime = EstimateFFmpegRenderTime(videoDuration, useNvenc: true);
        var savings = cpuTime - nvencTime;

        return $"NVENC saves ~{savings.TotalMinutes:F0} minutes (CPU: {cpuTime.TotalMinutes:F0}m vs NVENC: {nvencTime.TotalMinutes:F0}m)";
    }
}
