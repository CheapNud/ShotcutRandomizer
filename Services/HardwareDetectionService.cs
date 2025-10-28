using System.Diagnostics;
using System.Management;
using System.Runtime.Versioning;

namespace CheapShotcutRandomizer.Services;

/// <summary>
/// Detects hardware capabilities and provides optimal rendering settings
/// Automatically configures for RTX 3080 + Ryzen 9 5900X setup
/// Note: Uses Windows Management Instrumentation (WMI) - Windows-only
/// </summary>
[SupportedOSPlatform("windows")]
public class HardwareDetectionService(SvpDetectionService svpDetection)
{
    private HardwareCapabilities? _cachedCapabilities;

    /// <summary>
    /// Detect hardware capabilities (cached after first call)
    /// </summary>
    public virtual async Task<HardwareCapabilities> DetectHardwareAsync()
    {
        if (_cachedCapabilities != null)
            return _cachedCapabilities;

        var capabilities = new HardwareCapabilities
        {
            CpuCoreCount = Environment.ProcessorCount,
            CpuName = GetCpuName(),
            HasNvidiaGpu = await DetectNvidiaGpuAsync(),
            GpuName = GetGpuName(),
            NvencAvailable = await IsNvencAvailableAsync(),
            AvailableGpus = GetAllGpuNames()
        };

        // Detect individual hardware encoders
        await DetectHardwareEncodersAsync(capabilities);

        Debug.WriteLine("=== Hardware Detection ===");
        Debug.WriteLine($"CPU: {capabilities.CpuName} ({capabilities.CpuCoreCount} cores)");
        Debug.WriteLine($"GPU: {capabilities.GpuName}");
        Debug.WriteLine($"NVIDIA GPU: {capabilities.HasNvidiaGpu}");
        Debug.WriteLine($"NVENC Available: {capabilities.NvencAvailable}");
        Debug.WriteLine($"Recommended for melt: CPU multi-threading");
        Debug.WriteLine($"Recommended for FFmpeg: {(capabilities.NvencAvailable ? "NVENC (8-10x faster!)" : "CPU encoding")}");
        Debug.WriteLine($"Hardware Encoders Detected: {capabilities.SupportedEncoders.Count(e => e.Value.IsAvailable)}");
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

    /// <summary>
    /// Get all available GPU names (for multi-GPU systems)
    /// </summary>
    private List<string> GetAllGpuNames()
    {
        var gpuList = new List<string>();

        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            var results = searcher.Get();

            foreach (ManagementObject obj in results)
            {
                var name = obj["Name"]?.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    gpuList.Add(name);
                }
            }

            // If no GPUs found via WMI, try nvidia-smi for NVIDIA GPUs
            if (gpuList.Count == 0)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "nvidia-smi",
                            Arguments = "--query-gpu=name --format=csv,noheader",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        var gpuNames = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        gpuList.AddRange(gpuNames);
                    }
                }
                catch
                {
                    // nvidia-smi not available
                }
            }

            // If still no GPUs found, add a default entry
            if (gpuList.Count == 0)
            {
                gpuList.Add("Unknown GPU");
            }

            Debug.WriteLine($"Detected {gpuList.Count} GPU(s): {string.Join(", ", gpuList)}");
            return gpuList;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error detecting GPUs: {ex.Message}");
            return new List<string> { "Unknown GPU" };
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

    private bool IsIntelCpu()
    {
        try
        {
            var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            var results = searcher.Get();

            foreach (ManagementObject obj in results)
            {
                var cpuName = obj["Name"]?.ToString() ?? "";
                var manufacturer = obj["Manufacturer"]?.ToString() ?? "";

                // Check both manufacturer and name for Intel
                var isIntel = manufacturer.Contains("Intel", StringComparison.OrdinalIgnoreCase) ||
                             cpuName.Contains("Intel", StringComparison.OrdinalIgnoreCase);

                Debug.WriteLine($"CPU Vendor Check: {cpuName} - Manufacturer: {manufacturer} - Is Intel: {isIntel}");
                return isIntel;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error detecting CPU vendor: {ex.Message}");
            return false;
        }
    }

    private async Task DetectHardwareEncodersAsync(HardwareCapabilities capabilities)
    {
        // Define all encoders we want to check with their metadata
        var encodersToCheck = new Dictionary<string, HardwareEncoderInfo>
        {
            ["av1_nvenc"] = new()
            {
                CodecName = "av1_nvenc",
                DisplayName = "AV1 (NVENC)",
                VendorType = "NVENC",
                EstimatedSpeedupFactor = 12.0,
                Description = "Highest compression efficiency, best for archival and streaming"
            },
            ["av1_qsv"] = new()
            {
                CodecName = "av1_qsv",
                DisplayName = "AV1 (Intel Quick Sync)",
                VendorType = "QSV",
                EstimatedSpeedupFactor = 8.0,
                Description = "Good compression efficiency with Intel integrated graphics"
            },
            ["h264_amf"] = new()
            {
                CodecName = "h264_amf",
                DisplayName = "H.264 (AMD AMF)",
                VendorType = "AMF",
                EstimatedSpeedupFactor = 7.0,
                Description = "Universal compatibility with AMD hardware acceleration"
            },
            ["h264_nvenc"] = new()
            {
                CodecName = "h264_nvenc",
                DisplayName = "H.264 (NVENC)",
                VendorType = "NVENC",
                EstimatedSpeedupFactor = 8.0,
                Description = "Most compatible format, excellent speed on NVIDIA GPUs"
            },
            ["h264_qsv"] = new()
            {
                CodecName = "h264_qsv",
                DisplayName = "H.264 (Intel Quick Sync)",
                VendorType = "QSV",
                EstimatedSpeedupFactor = 6.0,
                Description = "Good compatibility with Intel integrated graphics"
            },
            ["hevc_amf"] = new()
            {
                CodecName = "hevc_amf",
                DisplayName = "HEVC/H.265 (AMD AMF)",
                VendorType = "AMF",
                EstimatedSpeedupFactor = 7.5,
                Description = "Better compression than H.264 with AMD hardware"
            },
            ["hevc_nvenc"] = new()
            {
                CodecName = "hevc_nvenc",
                DisplayName = "HEVC/H.265 (NVENC)",
                VendorType = "NVENC",
                EstimatedSpeedupFactor = 8.5,
                Description = "Excellent quality-to-size ratio on NVIDIA GPUs"
            },
            ["hevc_qsv"] = new()
            {
                CodecName = "hevc_qsv",
                DisplayName = "HEVC/H.265 (Intel Quick Sync)",
                VendorType = "QSV",
                EstimatedSpeedupFactor = 6.5,
                Description = "Good compression with Intel integrated graphics"
            },
            ["vp9_qsv"] = new()
            {
                CodecName = "vp9_qsv",
                DisplayName = "VP9 (Intel Quick Sync)",
                VendorType = "QSV",
                EstimatedSpeedupFactor = 5.0,
                Description = "Open format optimized for web streaming"
            }
        };

        // Get FFmpeg path
        var ffmpegPath = await GetFFmpegPathAsync();
        if (string.IsNullOrEmpty(ffmpegPath))
        {
            // No FFmpeg available, mark all as unavailable
            capabilities.SupportedEncoders = encodersToCheck;
            return;
        }

        // Detect CPU vendor for QSV validation
        var isIntelCpu = IsIntelCpu();

        // Check which encoders are available
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = "-encoders",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                foreach (var encoder in encodersToCheck)
                {
                    // Check if encoder exists in FFmpeg
                    var foundInFfmpeg = output.Contains(encoder.Key);

                    // Apply vendor-specific validation
                    if (encoder.Value.VendorType == "QSV")
                    {
                        // QSV requires Intel CPU - ignore FFmpeg listing if not Intel
                        if (!isIntelCpu)
                        {
                            encoder.Value.IsAvailable = false;
                            Debug.WriteLine($"Hardware encoder {encoder.Key}: Disabled (Intel CPU required, detected: {capabilities.CpuName})");
                        }
                        else
                        {
                            encoder.Value.IsAvailable = foundInFfmpeg;
                            Debug.WriteLine($"Hardware encoder {encoder.Key}: {(encoder.Value.IsAvailable ? "Available" : "Not available")}");
                        }
                    }
                    else
                    {
                        encoder.Value.IsAvailable = foundInFfmpeg;
                        Debug.WriteLine($"Hardware encoder {encoder.Key}: {(encoder.Value.IsAvailable ? "Available" : "Not available")}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error detecting hardware encoders: {ex.Message}");
        }

        capabilities.SupportedEncoders = encodersToCheck;
    }

    private async Task<string?> GetFFmpegPathAsync()
    {
        // Priority order: SVP > PATH > Shotcut
        var ffmpegPaths = new List<string>();

        // 1. Try SVP installation first (best FFmpeg build)
        var svp = svpDetection.DetectSvpInstallation();
        if (svp.IsInstalled && File.Exists(svp.FFmpegPath))
        {
            ffmpegPaths.Add(svp.FFmpegPath);
        }

        // 2. Try PATH
        ffmpegPaths.Add("ffmpeg");

        // 3. Shotcut locations (fallback)
        ffmpegPaths.Add(@"C:\Program Files\Shotcut\ffmpeg.exe");
        ffmpegPaths.Add(@"C:\Program Files (x86)\Shotcut\ffmpeg.exe");

        foreach (var ffmpegPath in ffmpegPaths)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    return ffmpegPath;
                }
            }
            catch
            {
                // Continue to next path
            }
        }

        return null;
    }

    private async Task<bool> IsNvencAvailableAsync()
    {
        // Priority order: SVP > PATH > Shotcut
        var ffmpegPaths = new List<string>();

        // 1. Try SVP installation first (best FFmpeg build)
        var svp = svpDetection.DetectSvpInstallation();
        if (svp.IsInstalled && File.Exists(svp.FFmpegPath))
        {
            ffmpegPaths.Add(svp.FFmpegPath);
            Debug.WriteLine($"[HardwareDetection] Prioritizing SVP FFmpeg: {svp.FFmpegPath}");
        }

        // 2. Try PATH
        ffmpegPaths.Add("ffmpeg");

        // 3. Shotcut locations (fallback)
        ffmpegPaths.Add(@"C:\Program Files\Shotcut\ffmpeg.exe");
        ffmpegPaths.Add(@"C:\Program Files (x86)\Shotcut\ffmpeg.exe");
        ffmpegPaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Shotcut", "ffmpeg.exe"));
        ffmpegPaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Shotcut", "ffmpeg.exe"));

        foreach (var ffmpegPath in ffmpegPaths)
        {
            try
            {
                Debug.WriteLine($"Checking FFmpeg at: {ffmpegPath}");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-encoders",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var errorOutput = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                Debug.WriteLine($"FFmpeg exit code: {process.ExitCode}");

                if (process.ExitCode == 0)
                {
                    var hasH264Nvenc = output.Contains("h264_nvenc");
                    var hasHevcNvenc = output.Contains("hevc_nvenc");

                    Debug.WriteLine($"h264_nvenc found: {hasH264Nvenc}");
                    Debug.WriteLine($"hevc_nvenc found: {hasHevcNvenc}");

                    if (hasH264Nvenc || hasHevcNvenc)
                    {
                        Debug.WriteLine($"SUCCESS: NVENC available via {ffmpegPath}");
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine("FFmpeg found but NVENC encoders not available");
                        Debug.WriteLine("This usually means NVIDIA drivers are not installed or GPU doesn't support NVENC");
                        return false;
                    }
                }
                else
                {
                    Debug.WriteLine($"FFmpeg returned error: {errorOutput}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to check {ffmpegPath}: {ex.Message}");
                // Continue to next path
            }
        }

        Debug.WriteLine("FAILED: FFmpeg not found in any common location");
        Debug.WriteLine("Checked locations:");
        foreach (var path in ffmpegPaths)
        {
            Debug.WriteLine($"  - {path}");
        }

        return false;
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

    // Multi-GPU support
    public List<string> AvailableGpus { get; set; } = new();

    // Hardware encoder availability
    public Dictionary<string, HardwareEncoderInfo> SupportedEncoders { get; set; } = new();

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

/// <summary>
/// Information about a hardware encoder
/// </summary>
public class HardwareEncoderInfo
{
    public string CodecName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string VendorType { get; set; } = string.Empty; // NVENC, QSV, AMF
    public double EstimatedSpeedupFactor { get; set; } = 1.0;
    public string Description { get; set; } = string.Empty;
}
