using System.Diagnostics;

namespace CheapShotcutRandomizer.Services;

/// <summary>
/// Detects and locates required executables (ffmpeg, melt, rife) in priority order:
/// 1. Custom paths (from settings)
/// 2. SVP installation (preferred for FFmpeg)
/// 3. Shotcut installation
/// 4. System PATH
/// 5. Common installation directories
/// </summary>
public class ExecutableDetectionService(SvpDetectionService svpDetection)
{
    private readonly SvpDetectionService _svpDetection = svpDetection;

    /// <summary>
    /// Auto-detect all executables and return populated paths
    /// Used for initializing default settings on first run
    /// </summary>
    public DetectedExecutables DetectAll()
    {
        var detected = new DetectedExecutables
        {
            FFmpegPath = DetectFFmpeg(useSvpEncoders: true, customPath: null),
            MeltPath = DetectMelt(customPath: null),
            RifePath = DetectRife(customPath: null)
        };

        Debug.WriteLine("=== Executable Detection ===");
        Debug.WriteLine($"FFmpeg: {detected.FFmpegPath ?? "NOT FOUND"}");
        Debug.WriteLine($"Melt: {detected.MeltPath ?? "NOT FOUND"}");
        Debug.WriteLine($"RIFE: {detected.RifePath ?? "NOT FOUND"}");
        Debug.WriteLine("============================");

        return detected;
    }

    /// <summary>
    /// Detect FFmpeg with priority order
    /// </summary>
    public string? DetectFFmpeg(bool useSvpEncoders, string? customPath)
    {
        // 1. Custom path
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            if (File.Exists(customPath))
            {
                Debug.WriteLine($"[FFmpeg] Using custom path: {customPath}");
                return customPath;
            }

            Debug.WriteLine($"[FFmpeg] Custom path not found: {customPath}");
        }

        // 2. SVP installation (if enabled)
        if (useSvpEncoders)
        {
            var svp = _svpDetection.DetectSvpInstallation();
            if (svp.IsInstalled && File.Exists(svp.FFmpegPath))
            {
                Debug.WriteLine($"[FFmpeg] Using SVP: {svp.FFmpegPath}");
                return svp.FFmpegPath;
            }
        }

        // 3. System PATH
        if (IsExecutableInPath("ffmpeg"))
        {
            var pathLocation = GetExecutablePathFromCommand("ffmpeg");
            Debug.WriteLine($"[FFmpeg] Found in system PATH: {pathLocation ?? "ffmpeg"}");
            return pathLocation ?? "ffmpeg";
        }

        // 4. Shotcut installation
        var shotcutPaths = new[]
        {
            @"C:\Program Files\Shotcut\ffmpeg.exe",
            @"C:\Program Files (x86)\Shotcut\ffmpeg.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Shotcut", "ffmpeg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Shotcut", "ffmpeg.exe")
        };

        foreach (var shotcutPath in shotcutPaths)
        {
            if (File.Exists(shotcutPath))
            {
                Debug.WriteLine($"[FFmpeg] Using Shotcut: {shotcutPath}");
                return shotcutPath;
            }
        }

        Debug.WriteLine("[FFmpeg] NOT FOUND - user must specify path");
        return null;
    }

    /// <summary>
    /// Detect Melt (Shotcut's MLT render engine) with priority order
    /// </summary>
    public string? DetectMelt(string? customPath)
    {
        // 1. Custom path
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            if (File.Exists(customPath))
            {
                Debug.WriteLine($"[Melt] Using custom path: {customPath}");
                return customPath;
            }

            Debug.WriteLine($"[Melt] Custom path not found: {customPath}");
        }

        // 2. System PATH
        if (IsExecutableInPath("melt"))
        {
            var pathLocation = GetExecutablePathFromCommand("melt");
            Debug.WriteLine($"[Melt] Found in system PATH: {pathLocation ?? "melt"}");
            return pathLocation ?? "melt";
        }

        // 3. Shotcut installation
        var shotcutPaths = new[]
        {
            @"C:\Program Files\Shotcut\melt.exe",
            @"C:\Program Files (x86)\Shotcut\melt.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Shotcut", "melt.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Shotcut", "melt.exe")
        };

        foreach (var shotcutPath in shotcutPaths)
        {
            if (File.Exists(shotcutPath))
            {
                Debug.WriteLine($"[Melt] Using Shotcut: {shotcutPath}");
                return shotcutPath;
            }
        }

        // 4. Portable Shotcut (in user folders)
        var portablePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Shotcut", "melt.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shotcut", "melt.exe")
        };

        foreach (var portablePath in portablePaths)
        {
            if (File.Exists(portablePath))
            {
                Debug.WriteLine($"[Melt] Using portable: {portablePath}");
                return portablePath;
            }
        }

        Debug.WriteLine("[Melt] NOT FOUND - user must specify Shotcut installation path");
        return null;
    }

    /// <summary>
    /// Detect RIFE (rife-ncnn-vulkan) with priority order
    /// </summary>
    public string? DetectRife(string? customPath)
    {
        // 1. Custom path
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            if (File.Exists(customPath))
            {
                Debug.WriteLine($"[RIFE] Using custom path: {customPath}");
                return customPath;
            }

            Debug.WriteLine($"[RIFE] Custom path not found: {customPath}");
        }

        // 2. System PATH
        if (IsExecutableInPath("rife-ncnn-vulkan"))
        {
            var pathLocation = GetExecutablePathFromCommand("rife-ncnn-vulkan");
            Debug.WriteLine($"[RIFE] Found in system PATH: {pathLocation ?? "rife-ncnn-vulkan"}");
            return pathLocation ?? "rife-ncnn-vulkan";
        }

        // 3. Common installation locations
        var commonPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "rife-ncnn-vulkan", "rife-ncnn-vulkan.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "rife-ncnn-vulkan", "rife-ncnn-vulkan.exe"),
            Path.Combine(Environment.CurrentDirectory, "rife-ncnn-vulkan.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "rife-ncnn-vulkan", "rife-ncnn-vulkan.exe")
        };

        foreach (var commonPath in commonPaths)
        {
            if (File.Exists(commonPath))
            {
                Debug.WriteLine($"[RIFE] Found at: {commonPath}");
                return commonPath;
            }
        }

        Debug.WriteLine("[RIFE] NOT FOUND - RIFE features will be unavailable");
        return null;
    }

    /// <summary>
    /// Check if executable exists in system PATH
    /// </summary>
    private bool IsExecutableInPath(string executableName)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executableName,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(1000);

            return process.ExitCode == 0 || process.ExitCode == 1; // Some tools return 1 for --version
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get full path of executable from PATH using 'where' command
    /// </summary>
    private string? GetExecutablePathFromCommand(string executableName)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = executableName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                // 'where' returns multiple paths if found in multiple locations
                // Return the first one
                var firstPath = output.Split('\n')[0].Trim();
                return File.Exists(firstPath) ? firstPath : null;
            }
        }
        catch
        {
            // where command not available or error
        }

        return null;
    }
}

/// <summary>
/// Result of executable detection
/// </summary>
public class DetectedExecutables
{
    public string? FFmpegPath { get; set; }
    public string? MeltPath { get; set; }
    public string? RifePath { get; set; }

    public bool AllFound => FFmpegPath != null && MeltPath != null && RifePath != null;
    public bool FFmpegAndMeltFound => FFmpegPath != null && MeltPath != null;
}
