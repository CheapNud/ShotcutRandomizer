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
            FFprobePath = DetectFFprobe(useSvpEncoders: true, customPath: null),
            MeltPath = DetectMelt(customPath: null),
            RifePath = DetectRife(customPath: null)
        };

        Debug.WriteLine("=== Executable Detection ===");
        Debug.WriteLine($"FFmpeg: {detected.FFmpegPath ?? "NOT FOUND"}");
        Debug.WriteLine($"FFprobe: {detected.FFprobePath ?? "NOT FOUND"}");
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
    /// Detect FFprobe with priority order (usually in same directory as FFmpeg)
    /// Note: SVP installation does NOT include ffprobe, so we skip SVP and go to Shotcut
    /// </summary>
    public string? DetectFFprobe(bool useSvpEncoders, string? customPath)
    {
        // 1. Custom path
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            if (File.Exists(customPath))
            {
                Debug.WriteLine($"[FFprobe] Using custom path: {customPath}");
                return customPath;
            }

            Debug.WriteLine($"[FFprobe] Custom path not found: {customPath}");
        }

        // 2. System PATH
        if (IsExecutableInPath("ffprobe"))
        {
            var pathLocation = GetExecutablePathFromCommand("ffprobe");
            Debug.WriteLine($"[FFprobe] Found in system PATH: {pathLocation ?? "ffprobe"}");
            return pathLocation ?? "ffprobe";
        }

        // 3. Shotcut installation (SVP doesn't include ffprobe)
        var shotcutPaths = new[]
        {
            @"C:\Program Files\Shotcut\ffprobe.exe",
            @"C:\Program Files (x86)\Shotcut\ffprobe.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Shotcut", "ffprobe.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Shotcut", "ffprobe.exe")
        };

        foreach (var shotcutPath in shotcutPaths)
        {
            if (File.Exists(shotcutPath))
            {
                Debug.WriteLine($"[FFprobe] Using Shotcut: {shotcutPath}");
                return shotcutPath;
            }
        }

        Debug.WriteLine("[FFprobe] NOT FOUND - RIFE video validation will be unavailable");
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
    /// Detect RIFE folder (Python project)
    /// RIFE is a Python project, not a standalone executable
    /// Supports:
    /// - SVP's integrated RIFE (with VapourSynth/TensorRT)
    /// - Practical-RIFE standalone (https://github.com/hzwer/Practical-RIFE)
    /// </summary>
    public string? DetectRife(string? customPath)
    {
        // 1. Custom path (folder)
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            if (Directory.Exists(customPath))
            {
                // Check if it's a valid RIFE folder
                // For Practical-RIFE: has inference_video.py
                // For SVP RIFE: has rife.dll or rife_vs.dll
                if (File.Exists(Path.Combine(customPath, "inference_video.py")) ||
                    File.Exists(Path.Combine(customPath, "inference_img.py")) ||
                    File.Exists(Path.Combine(customPath, "rife.dll")) ||
                    File.Exists(Path.Combine(customPath, "rife_vs.dll")))
                {
                    Debug.WriteLine($"[RIFE] Using custom path: {customPath}");
                    return customPath;
                }
                Debug.WriteLine($"[RIFE] Custom path exists but doesn't appear to be a RIFE folder: {customPath}");
            }
            else
            {
                Debug.WriteLine($"[RIFE] Custom path not found: {customPath}");
            }
        }

        // 2. SVP's RIFE installation (in root SVP folder, NOT in utils)
        var svp = _svpDetection.DetectSvpInstallation();
        if (svp.IsInstalled && !string.IsNullOrEmpty(svp.InstallPath))
        {
            var svpRifePath = Path.Combine(svp.InstallPath, "rife");
            if (Directory.Exists(svpRifePath))
            {
                // Check for SVP's RIFE files (rife.dll, rife_vs.dll, etc.)
                if (File.Exists(Path.Combine(svpRifePath, "rife.dll")) ||
                    File.Exists(Path.Combine(svpRifePath, "rife_vs.dll")))
                {
                    Debug.WriteLine($"[RIFE] Found SVP's RIFE installation: {svpRifePath}");
                    return svpRifePath;
                }
            }
        }

        // 3. Check for cloned RIFE repos in common locations
        var commonPaths = new[]
        {
            // User's home directory
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Practical-RIFE"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "RIFE"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ECCV2022-RIFE"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "rife"),

            // Documents folder
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Practical-RIFE"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RIFE"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ECCV2022-RIFE"),

            // Current directory
            Path.Combine(Environment.CurrentDirectory, "Practical-RIFE"),
            Path.Combine(Environment.CurrentDirectory, "RIFE"),
            Path.Combine(Environment.CurrentDirectory, "ECCV2022-RIFE"),

            // Common development folders
            @"C:\Practical-RIFE",
            @"C:\RIFE",
            @"C:\ECCV2022-RIFE",
            @"C:\Development\Practical-RIFE",
            @"C:\Development\RIFE",
            @"C:\Projects\Practical-RIFE",
            @"C:\Projects\RIFE",
            @"D:\Practical-RIFE",
            @"D:\RIFE"
        };

        foreach (var rifePath in commonPaths)
        {
            if (Directory.Exists(rifePath))
            {
                // Check for inference_video.py (main RIFE script)
                if (File.Exists(Path.Combine(rifePath, "inference_video.py")))
                {
                    Debug.WriteLine($"[RIFE] Found RIFE repository at: {rifePath}");
                    return rifePath;
                }
            }
        }

        Debug.WriteLine("[RIFE] NOT FOUND - RIFE features will be unavailable");
        Debug.WriteLine("[RIFE] Please install RIFE using one of these methods:");
        Debug.WriteLine("  1. SVP 4 Pro (includes RIFE with TensorRT): https://www.svp-team.com");
        Debug.WriteLine("  2. Clone Practical-RIFE: git clone https://github.com/hzwer/Practical-RIFE.git");
        Debug.WriteLine("  3. After cloning, install dependencies: pip install torch torchvision opencv-python");
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
    public string? FFprobePath { get; set; }
    public string? MeltPath { get; set; }
    public string? RifePath { get; set; }

    public bool AllFound => FFmpegPath != null && FFprobePath != null && MeltPath != null && RifePath != null;
    public bool FFmpegAndMeltFound => FFmpegPath != null && MeltPath != null;
    public bool EssentialsFound => FFmpegPath != null && FFprobePath != null && MeltPath != null;
}
