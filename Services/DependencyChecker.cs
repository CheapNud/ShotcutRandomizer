using System.Diagnostics;
using CheapShotcutRandomizer.Models;
using CheapShotcutRandomizer.Services.VapourSynth;

namespace CheapShotcutRandomizer.Services;

/// <summary>
/// Detects and validates external dependencies required by the application
/// Integrates with existing ExecutableDetectionService and SvpDetectionService
/// Reports on actually-used Python and VapourSynth (not just PATH detection)
/// </summary>
public class DependencyChecker(
    ExecutableDetectionService executableDetection,
    SvpDetectionService svpDetection,
    IVapourSynthEnvironment vapourSynthEnvironment)
{
    private readonly ExecutableDetectionService _executableDetection = executableDetection;
    private readonly SvpDetectionService _svpDetection = svpDetection;
    private readonly IVapourSynthEnvironment _vapourSynthEnvironment = vapourSynthEnvironment;

    /// <summary>
    /// Check all dependencies and return comprehensive status
    /// </summary>
    public async Task<DependencyStatus> CheckAllDependenciesAsync()
    {
        var allDependencies = new List<DependencyInfo>();

        // Check all dependency types
        allDependencies.Add(await CheckDependencyAsync(DependencyType.FFmpeg));
        allDependencies.Add(await CheckDependencyAsync(DependencyType.FFprobe));
        allDependencies.Add(await CheckDependencyAsync(DependencyType.Melt));
        allDependencies.Add(await CheckDependencyAsync(DependencyType.VapourSynth));
        allDependencies.Add(await CheckDependencyAsync(DependencyType.VapourSynthSourcePlugin));
        allDependencies.Add(await CheckDependencyAsync(DependencyType.SvpRife));
        allDependencies.Add(await CheckDependencyAsync(DependencyType.Python));
        allDependencies.Add(await CheckDependencyAsync(DependencyType.PracticalRife));

        Debug.WriteLine("=== Dependency Check Complete ===");
        Debug.WriteLine($"Total dependencies: {allDependencies.Count}");
        Debug.WriteLine($"Required installed: {allDependencies.Count(d => d.IsRequired && d.IsInstalled)}/{allDependencies.Count(d => d.IsRequired)}");
        Debug.WriteLine($"Optional installed: {allDependencies.Count(d => !d.IsRequired && d.IsInstalled)}/{allDependencies.Count(d => !d.IsRequired)}");
        Debug.WriteLine("=================================");

        return new DependencyStatus
        {
            AllDependencies = allDependencies
        };
    }

    /// <summary>
    /// Check a specific dependency type
    /// </summary>
    public async Task<DependencyInfo> CheckDependencyAsync(DependencyType type)
    {
        return type switch
        {
            DependencyType.FFmpeg => await CheckFFmpegAsync(),
            DependencyType.FFprobe => await CheckFFprobeAsync(),
            DependencyType.Melt => await CheckMeltAsync(),
            DependencyType.VapourSynth => await CheckVapourSynthAsync(),
            DependencyType.VapourSynthSourcePlugin => await CheckVapourSynthSourcePluginAsync(),
            DependencyType.SvpRife => await CheckSvpRifeAsync(),
            DependencyType.Python => await CheckPythonAsync(),
            DependencyType.PracticalRife => await CheckPracticalRifeAsync(),
            _ => throw new ArgumentException($"Unknown dependency type: {type}", nameof(type))
        };
    }

    private async Task<DependencyInfo> CheckFFmpegAsync()
    {
        var ffmpegPath = _executableDetection.DetectFFmpeg(useSvpEncoders: true, customPath: null);
        var isInstalled = ffmpegPath != null;
        string? version = null;

        if (isInstalled)
        {
            version = await GetFFmpegVersionAsync(ffmpegPath!);
        }

        return new DependencyInfo
        {
            Type = DependencyType.FFmpeg,
            Name = "FFmpeg",
            Description = "Video encoding and decoding tool. Required for all video processing operations.",
            IsInstalled = isInstalled,
            IsRequired = true,
            InstalledVersion = version,
            InstalledPath = ffmpegPath,
            DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/",
            ChocolateyPackage = "ffmpeg",
            SupportsAutomatedInstall = true,
            SupportsPortableInstall = true,
            PortableDownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
            InstallInstructions = @"**Option 1: Via Chocolatey**
```
choco install ffmpeg
```

**Option 2: Via Shotcut**
Install Shotcut, which includes FFmpeg: https://shotcut.org/download/

**Option 3: Via SVP 4 Pro**
Install SVP 4 Pro, which includes optimized FFmpeg: https://www.svp-team.com/get/

**Option 4: Portable**
Download FFmpeg essentials from https://www.gyan.dev/ffmpeg/builds/
Extract to a folder and point the app to ffmpeg.exe",
            DetectionMessage = isInstalled
                ? $"FFmpeg found at: {ffmpegPath}"
                : "FFmpeg not found. Install Shotcut, SVP, or download standalone FFmpeg."
        };
    }

    private async Task<DependencyInfo> CheckFFprobeAsync()
    {
        var ffprobePath = _executableDetection.DetectFFprobe(useSvpEncoders: true, customPath: null);
        var isInstalled = ffprobePath != null;
        string? version = null;

        if (isInstalled)
        {
            version = await GetFFprobeVersionAsync(ffprobePath!);
        }

        return new DependencyInfo
        {
            Type = DependencyType.FFprobe,
            Name = "FFprobe",
            Description = "Video analysis and metadata extraction tool. Required for video validation and info retrieval.",
            IsInstalled = isInstalled,
            IsRequired = true,
            InstalledVersion = version,
            InstalledPath = ffprobePath,
            DownloadUrl = "https://www.gyan.dev/ffmpeg/builds/",
            ChocolateyPackage = "ffmpeg", // FFprobe comes with FFmpeg
            SupportsAutomatedInstall = true,
            SupportsPortableInstall = true,
            PortableDownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
            InstallInstructions = "FFprobe is included with FFmpeg. Install FFmpeg to get FFprobe.",
            DetectionMessage = isInstalled
                ? $"FFprobe found at: {ffprobePath}"
                : "FFprobe not found. Usually installed with FFmpeg."
        };
    }

    private async Task<DependencyInfo> CheckMeltAsync()
    {
        var meltPath = _executableDetection.DetectMelt(customPath: null);
        var isInstalled = meltPath != null;
        string? version = null;

        if (isInstalled)
        {
            version = await GetMeltVersionAsync(meltPath!);
        }

        return new DependencyInfo
        {
            Type = DependencyType.Melt,
            Name = "Shotcut Melt",
            Description = "MLT Framework renderer for Shotcut projects. Required to render Shotcut .mlt project files.",
            IsInstalled = isInstalled,
            IsRequired = true,
            InstalledVersion = version,
            InstalledPath = meltPath,
            DownloadUrl = "https://shotcut.org/download/",
            ChocolateyPackage = null, // Shotcut doesn't have a reliable choco package
            SupportsAutomatedInstall = false,
            SupportsPortableInstall = true,
            PortableDownloadUrl = "https://shotcut.org/download/",
            InstallInstructions = @"**Install Shotcut**
Download and install Shotcut from: https://shotcut.org/download/

Shotcut includes the 'melt' executable required for rendering projects.
After installation, melt.exe will be in the Shotcut installation folder.",
            DetectionMessage = isInstalled
                ? $"Melt found at: {meltPath}"
                : "Melt not found. Please install Shotcut."
        };
    }

    private async Task<DependencyInfo> CheckVapourSynthAsync()
    {
        var isInstalled = await _vapourSynthEnvironment.IsVapourSynthAvailableAsync();
        var vspipePath = _vapourSynthEnvironment.VsPipePath;
        var version = _vapourSynthEnvironment.VapourSynthVersion;

        return new DependencyInfo
        {
            Type = DependencyType.VapourSynth,
            Name = "VapourSynth",
            Description = "Video processing framework. Required for AI upscaling (Real-CUGAN, Real-ESRGAN) and SVP RIFE.",
            IsInstalled = isInstalled,
            IsRequired = false,
            InstalledVersion = version,
            InstalledPath = vspipePath,
            DownloadUrl = "https://github.com/vapoursynth/vapoursynth/releases",
            ChocolateyPackage = null, // No official choco package
            SupportsAutomatedInstall = false,
            SupportsPortableInstall = false,
            InstallInstructions = @"**Install VapourSynth**
1. Download installer from: https://github.com/vapoursynth/vapoursynth/releases
2. Run the installer (adds vspipe to PATH)
3. Restart your computer or refresh PATH
4. Verify installation: `vspipe --version`",
            DetectionMessage = isInstalled
                ? $"VapourSynth found at: {vspipePath}"
                : "VapourSynth not found. Required for AI upscaling services."
        };
    }

    private async Task<DependencyInfo> CheckVapourSynthSourcePluginAsync()
    {
        var (isInstalled, pluginName, pluginPath) = await DetectVapourSynthSourcePluginAsync();

        return new DependencyInfo
        {
            Type = DependencyType.VapourSynthSourcePlugin,
            Name = "VapourSynth Source Plugin",
            Description = "Video loading plugin for VapourSynth. One of: BestSource (recommended), L-SMASH, or FFMS2.",
            IsInstalled = isInstalled,
            IsRequired = false,
            InstalledVersion = pluginName,
            InstalledPath = pluginPath,
            DownloadUrl = "https://github.com/vapoursynth/bestsource/releases",
            ChocolateyPackage = null,
            SupportsAutomatedInstall = false,
            SupportsPortableInstall = true,
            PortableDownloadUrl = "https://github.com/vapoursynth/bestsource/releases",
            Alternatives = ["BestSource", "L-SMASH Source", "FFMS2"],
            InstallInstructions = @"**Install BestSource (Recommended)**
1. Download from: https://github.com/vapoursynth/bestsource/releases
2. Extract BestSource.dll
3. Copy to: `C:\Program Files\VapourSynth\plugins\`
4. Or copy to: `%APPDATA%\VapourSynth\plugins\`

**Alternative: L-SMASH Source**
Download from: https://github.com/AkarinVS/L-SMASH-Works/releases

**Alternative: FFMS2**
Download from: https://github.com/FFMS/ffms2/releases",
            DetectionMessage = isInstalled
                ? $"Source plugin found: {pluginName} at {pluginPath}"
                : "No VapourSynth source plugin found. Install BestSource, L-SMASH, or FFMS2."
        };
    }

    private async Task<DependencyInfo> CheckSvpRifeAsync()
    {
        var svp = _svpDetection.DetectSvpInstallation();
        var isInstalled = svp.IsInstalled;
        var rifePath = isInstalled ? Path.Combine(svp.InstallPath, "rife") : null;
        var rifeExists = rifePath != null && Directory.Exists(rifePath);

        return new DependencyInfo
        {
            Type = DependencyType.SvpRife,
            Name = "SVP 4 Pro (RIFE TensorRT)",
            Description = "Smooth Video Project with RIFE AI frame interpolation. Provides TensorRT-accelerated RIFE and high-quality FFmpeg builds.",
            IsInstalled = isInstalled && rifeExists,
            IsRequired = false,
            InstalledVersion = svp.Version,
            InstalledPath = rifePath,
            DownloadUrl = "https://www.svp-team.com/get/",
            ChocolateyPackage = null,
            SupportsAutomatedInstall = false,
            SupportsPortableInstall = false,
            InstallInstructions = @"**Install SVP 4 Pro**
1. Download from: https://www.svp-team.com/get/
2. During installation, select 'RIFE AI engine' component
3. RIFE files will be installed to: `C:\Program Files (x86)\SVP 4\rife\`
4. Also install VapourSynth (see VapourSynth instructions)",
            DetectionMessage = isInstalled && rifeExists
                ? $"SVP RIFE found at: {rifePath}"
                : isInstalled
                    ? "SVP installed but RIFE component not found"
                    : "SVP 4 Pro not installed. Recommended for NVIDIA RTX GPU users."
        };
    }

    private async Task<DependencyInfo> CheckPythonAsync()
    {
        var isInstalled = await _vapourSynthEnvironment.IsPythonAvailableAsync();
        var pythonPath = await _vapourSynthEnvironment.GetPythonFullPathAsync();
        var version = _vapourSynthEnvironment.PythonVersion;
        var isCompatibleVersion = IsCompatiblePythonVersion(version);
        var usingSvp = _vapourSynthEnvironment.IsUsingSvpPython;

        var sourceInfo = usingSvp ? " (SVP's Python)" : " (System PATH)";

        return new DependencyInfo
        {
            Type = DependencyType.Python,
            Name = "Python 3.8-3.11",
            Description = "Python interpreter. Required for AI upscaling services.",
            IsInstalled = isInstalled && isCompatibleVersion,
            IsRequired = false,
            InstalledVersion = version + sourceInfo,
            InstalledPath = pythonPath,
            DownloadUrl = "https://www.python.org/downloads/",
            ChocolateyPackage = "python",
            SupportsAutomatedInstall = true,
            SupportsPortableInstall = false,
            InstallInstructions = @"**Install Python 3.11**
1. Download from: https://www.python.org/downloads/
2. During installation, check 'Add Python to PATH'
3. Verify installation: `python --version`
4. Should show Python 3.8.x - 3.11.x (3.12+ not yet supported by PyTorch)

**Via Chocolatey:**
```
choco install python --version=3.11.0
```",
            DetectionMessage = isInstalled
                ? isCompatibleVersion
                    ? $"Compatible Python found: {version} at {pythonPath}"
                    : $"Python found but incompatible version: {version}. Need 3.8-3.11"
                : "Python not found. Required for Practical-RIFE."
        };
    }

    private async Task<DependencyInfo> CheckPracticalRifeAsync()
    {
        var rifePath = _executableDetection.DetectRife(customPath: null);
        var isInstalled = rifePath != null && Directory.Exists(rifePath);
        var isPracticalRife = isInstalled && File.Exists(Path.Combine(rifePath!, "inference_video.py"));

        return new DependencyInfo
        {
            Type = DependencyType.PracticalRife,
            Name = "Practical-RIFE",
            Description = "Standalone Python implementation of RIFE. Alternative to SVP RIFE, supports more GPUs.",
            IsInstalled = isInstalled && isPracticalRife,
            IsRequired = false,
            InstalledVersion = null,
            InstalledPath = rifePath,
            DownloadUrl = "https://github.com/hzwer/Practical-RIFE",
            ChocolateyPackage = null,
            SupportsAutomatedInstall = false,
            SupportsPortableInstall = true,
            InstallInstructions = @"**Install Practical-RIFE**
1. Ensure Python 3.8-3.11 is installed
2. Clone repository:
   ```
   git clone https://github.com/hzwer/Practical-RIFE.git
   cd Practical-RIFE
   ```
3. Install dependencies (NVIDIA GPU):
   ```
   pip install torch torchvision --index-url https://download.pytorch.org/whl/cu118
   pip install opencv-python numpy
   ```
4. Test installation:
   ```
   python inference_video.py --video input.mp4 --multi 2
   ```",
            DetectionMessage = isInstalled && isPracticalRife
                ? $"Practical-RIFE found at: {rifePath}"
                : isInstalled
                    ? $"RIFE folder found but not Practical-RIFE: {rifePath}"
                    : "Practical-RIFE not found. Alternative to SVP RIFE."
        };
    }

    // Helper methods for version detection

    private async Task<string?> GetFFmpegVersionAsync(string ffmpegPath)
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
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var firstLine = output.Split('\n')[0];
                var versionMatch = System.Text.RegularExpressions.Regex.Match(firstLine, @"ffmpeg version ([\d\.]+)");
                if (versionMatch.Success)
                {
                    return versionMatch.Groups[1].Value;
                }
                return firstLine.Trim();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get FFmpeg version: {ex.Message}");
        }

        return null;
    }

    private async Task<string?> GetFFprobeVersionAsync(string ffprobePath)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var firstLine = output.Split('\n')[0];
                var versionMatch = System.Text.RegularExpressions.Regex.Match(firstLine, @"ffprobe version ([\d\.]+)");
                if (versionMatch.Success)
                {
                    return versionMatch.Groups[1].Value;
                }
                return firstLine.Trim();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get FFprobe version: {ex.Message}");
        }

        return null;
    }

    private async Task<string?> GetMeltVersionAsync(string meltPath)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = meltPath,
                    Arguments = "-version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(output))
            {
                var firstLine = output.Split('\n')[0];
                return firstLine.Trim();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get Melt version: {ex.Message}");
        }

        return null;
    }


    private async Task<(bool isInstalled, string? pluginName, string? pluginPath)> DetectVapourSynthSourcePluginAsync()
    {
        var pluginPaths = new[]
        {
            @"C:\Program Files\VapourSynth\plugins",
            @"C:\Program Files (x86)\VapourSynth\plugins",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VapourSynth", "plugins")
        };

        var pluginFiles = new Dictionary<string, string>
        {
            ["BestSource"] = "BestSource.dll",
            ["L-SMASH Source"] = "LSMASHSource.dll",
            ["FFMS2"] = "FFMS2.dll"
        };

        foreach (var pluginDir in pluginPaths)
        {
            if (!Directory.Exists(pluginDir))
                continue;

            foreach (var plugin in pluginFiles)
            {
                var pluginPath = Path.Combine(pluginDir, plugin.Value);
                if (File.Exists(pluginPath))
                {
                    return (true, plugin.Key, pluginPath);
                }
            }
        }

        return (false, null, null);
    }


    private bool IsCompatiblePythonVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        var versionMatch = System.Text.RegularExpressions.Regex.Match(version, @"^(\d+)\.(\d+)");
        if (!versionMatch.Success)
            return false;

        if (int.TryParse(versionMatch.Groups[1].Value, out var major) &&
            int.TryParse(versionMatch.Groups[2].Value, out var minor))
        {
            // Python 3.8-3.11 supported (3.12+ not yet supported by PyTorch)
            return major == 3 && minor >= 8 && minor <= 11;
        }

        return false;
    }

    private async Task<string?> GetExecutablePathAsync(string executableName)
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
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
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
