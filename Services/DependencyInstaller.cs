using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Principal;
using CheapShotcutRandomizer.Models;

namespace CheapShotcutRandomizer.Services;

/// <summary>
/// Handles automated installation of dependencies using various strategies
/// Supports Chocolatey, portable installations, and guided manual installation
/// </summary>
public class DependencyInstaller
{
    private readonly HttpClient _httpClient;
    private readonly string _portableToolsDirectory;

    public DependencyInstaller()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10) // Long timeout for large downloads
        };

        // Portable tools go in AppData\Local\ShotcutRandomizer\Tools
        _portableToolsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShotcutRandomizer",
            "Tools"
        );

        Directory.CreateDirectory(_portableToolsDirectory);
    }

    /// <summary>
    /// Event for reporting installation progress
    /// </summary>
    public event Action<int, string>? ProgressChanged;

    /// <summary>
    /// Install a dependency using the best available strategy
    /// </summary>
    public async Task<InstallationResult> InstallDependencyAsync(
        DependencyType dependencyType,
        InstallationStrategy? preferredStrategy = null,
        CancellationToken cancellationToken = default)
    {
        ReportProgress(0, $"Starting installation of {dependencyType}...");

        // Determine best strategy if not specified
        var strategy = preferredStrategy ?? await DetermineBestStrategyAsync(dependencyType);

        Debug.WriteLine($"Installing {dependencyType} using {strategy} strategy");

        return strategy switch
        {
            InstallationStrategy.Chocolatey => await InstallViaChocolateyAsync(dependencyType, cancellationToken),
            InstallationStrategy.Portable => await InstallPortableAsync(dependencyType, cancellationToken),
            InstallationStrategy.Installer => await InstallViaInstallerAsync(dependencyType, cancellationToken),
            InstallationStrategy.Manual => ProvideManualInstructions(dependencyType),
            _ => InstallationResult.CreateFailure(strategy, $"Unknown installation strategy: {strategy}")
        };
    }

    /// <summary>
    /// Determine the best installation strategy for a dependency
    /// </summary>
    private async Task<InstallationStrategy> DetermineBestStrategyAsync(DependencyType dependencyType)
    {
        // Check if user has admin rights
        var hasAdminRights = IsAdministrator();

        // Check if Chocolatey is installed
        var hasChocolatey = await IsChocolateyInstalledAsync();

        // Strategy priority:
        // 1. Portable (no admin needed, works everywhere)
        // 2. Chocolatey (if available and has admin)
        // 3. Installer (if has admin)
        // 4. Manual (fallback)

        return dependencyType switch
        {
            DependencyType.FFmpeg => hasChocolatey && hasAdminRights
                ? InstallationStrategy.Chocolatey
                : InstallationStrategy.Portable,

            DependencyType.FFprobe => hasChocolatey && hasAdminRights
                ? InstallationStrategy.Chocolatey
                : InstallationStrategy.Portable,

            DependencyType.Python => hasChocolatey && hasAdminRights
                ? InstallationStrategy.Chocolatey
                : InstallationStrategy.Installer,

            DependencyType.VapourSynth => hasAdminRights
                ? InstallationStrategy.Installer
                : InstallationStrategy.Manual,

            // These require manual installation
            DependencyType.Melt => InstallationStrategy.Manual,
            DependencyType.SvpRife => InstallationStrategy.Manual,
            DependencyType.VapourSynthSourcePlugin => InstallationStrategy.Manual,
            DependencyType.PracticalRife => InstallationStrategy.Manual,

            _ => InstallationStrategy.Manual
        };
    }

    /// <summary>
    /// Install via Chocolatey package manager
    /// </summary>
    private async Task<InstallationResult> InstallViaChocolateyAsync(
        DependencyType dependencyType,
        CancellationToken cancellationToken)
    {
        if (!await IsChocolateyInstalledAsync())
        {
            return InstallationResult.CreateFailure(
                InstallationStrategy.Chocolatey,
                "Chocolatey is not installed",
                "Install Chocolatey from https://chocolatey.org/install"
            );
        }

        if (!IsAdministrator())
        {
            return InstallationResult.CreateFailure(
                InstallationStrategy.Chocolatey,
                "Administrator rights required",
                "Chocolatey installation requires administrator privileges"
            );
        }

        var packageName = GetChocolateyPackageName(dependencyType);
        if (packageName == null)
        {
            return InstallationResult.CreateFailure(
                InstallationStrategy.Chocolatey,
                $"No Chocolatey package available for {dependencyType}"
            );
        }

        ReportProgress(10, $"Installing {packageName} via Chocolatey...");

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "choco",
                    Arguments = $"install {packageName} -y",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas" // Request elevation
                }
            };

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    Debug.WriteLine($"[Choco] {args.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                ReportProgress(100, $"Successfully installed {packageName}");
                return InstallationResult.CreateSuccess(
                    InstallationStrategy.Chocolatey,
                    $"{packageName} installed successfully via Chocolatey",
                    null
                );
            }
            else
            {
                var errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken);
                return InstallationResult.CreateFailure(
                    InstallationStrategy.Chocolatey,
                    $"Chocolatey installation failed with exit code {process.ExitCode}",
                    errorOutput
                );
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Chocolatey installation failed: {ex.Message}");
            return InstallationResult.CreateFailure(
                InstallationStrategy.Chocolatey,
                "Failed to run Chocolatey",
                ex.Message
            );
        }
    }

    /// <summary>
    /// Install portable version (download and extract)
    /// </summary>
    private async Task<InstallationResult> InstallPortableAsync(
        DependencyType dependencyType,
        CancellationToken cancellationToken)
    {
        var downloadUrl = GetPortableDownloadUrl(dependencyType);
        if (downloadUrl == null)
        {
            return InstallationResult.CreateFailure(
                InstallationStrategy.Portable,
                $"No portable version available for {dependencyType}"
            );
        }

        try
        {
            // Create subdirectory for this tool
            var toolName = dependencyType.ToString().ToLowerInvariant();
            var installPath = Path.Combine(_portableToolsDirectory, toolName);
            Directory.CreateDirectory(installPath);

            ReportProgress(10, "Downloading portable version...");

            // Download file
            var downloadFileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
            var downloadPath = Path.Combine(Path.GetTempPath(), downloadFileName);

            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progress = (int)(10 + (downloadedBytes * 60.0 / totalBytes));
                        ReportProgress(progress, $"Downloading... {downloadedBytes / 1024 / 1024} MB / {totalBytes / 1024 / 1024} MB");
                    }
                }
            }

            ReportProgress(70, "Extracting files...");

            // Extract archive
            if (downloadPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(downloadPath, installPath, overwriteFiles: true);
            }
            else if (downloadPath.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
            {
                return InstallationResult.CreateFailure(
                    InstallationStrategy.Portable,
                    "7z extraction not yet implemented",
                    "Please extract manually and point app to extracted location"
                );
            }

            // Clean up download
            File.Delete(downloadPath);

            ReportProgress(90, "Locating executables...");

            // Find the actual executable path
            var executablePath = FindExecutableInDirectory(installPath, dependencyType);

            ReportProgress(100, $"Successfully installed to {installPath}");

            return InstallationResult.CreateSuccess(
                InstallationStrategy.Portable,
                $"Installed portable version to {installPath}",
                executablePath ?? installPath
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Portable installation failed: {ex.Message}");
            return InstallationResult.CreateFailure(
                InstallationStrategy.Portable,
                "Failed to install portable version",
                ex.Message
            );
        }
    }

    /// <summary>
    /// Install via downloaded installer
    /// </summary>
    private async Task<InstallationResult> InstallViaInstallerAsync(
        DependencyType dependencyType,
        CancellationToken cancellationToken)
    {
        var installerUrl = GetInstallerDownloadUrl(dependencyType);
        if (installerUrl == null)
        {
            return InstallationResult.CreateFailure(
                InstallationStrategy.Installer,
                $"No installer available for {dependencyType}"
            );
        }

        try
        {
            ReportProgress(10, "Downloading installer...");

            var installerFileName = Path.GetFileName(new Uri(installerUrl).LocalPath);
            var installerPath = Path.Combine(Path.GetTempPath(), installerFileName);

            // Download installer
            using (var response = await _httpClient.GetAsync(installerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progress = (int)(10 + (downloadedBytes * 60.0 / totalBytes));
                        ReportProgress(progress, $"Downloading installer... {downloadedBytes / 1024 / 1024} MB / {totalBytes / 1024 / 1024} MB");
                    }
                }
            }

            ReportProgress(70, "Running installer...");

            // Run installer with silent flags
            var silentArgs = GetInstallerSilentArgs(dependencyType);
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = silentArgs,
                    UseShellExecute = true,
                    Verb = "runas" // Request elevation
                }
            };

            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
            {
                ReportProgress(100, "Installation completed");
                var result = InstallationResult.CreateSuccess(
                    InstallationStrategy.Installer,
                    $"{dependencyType} installed successfully",
                    null
                );
                result.RequiresRestart = true;
                return result;
            }
            else
            {
                return InstallationResult.CreateFailure(
                    InstallationStrategy.Installer,
                    $"Installer exited with code {process.ExitCode}"
                );
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Installer installation failed: {ex.Message}");
            return InstallationResult.CreateFailure(
                InstallationStrategy.Installer,
                "Failed to run installer",
                ex.Message
            );
        }
    }

    /// <summary>
    /// Provide manual installation instructions
    /// </summary>
    private InstallationResult ProvideManualInstructions(DependencyType dependencyType)
    {
        var instructions = GetManualInstructions(dependencyType);
        var downloadUrl = GetDownloadUrl(dependencyType);

        return InstallationResult.CreateFailure(
            InstallationStrategy.Manual,
            $"Manual installation required for {dependencyType}",
            $"Please follow these instructions:\n\n{instructions}\n\nDownload from: {downloadUrl}"
        );
    }

    // Helper methods

    private bool IsAdministrator()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> IsChocolateyInstalledAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "choco",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private string? GetChocolateyPackageName(DependencyType dependencyType)
    {
        return dependencyType switch
        {
            DependencyType.FFmpeg => "ffmpeg",
            DependencyType.FFprobe => "ffmpeg", // FFprobe comes with FFmpeg
            DependencyType.Python => "python",
            _ => null
        };
    }

    private string? GetPortableDownloadUrl(DependencyType dependencyType)
    {
        return dependencyType switch
        {
            DependencyType.FFmpeg => "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
            DependencyType.FFprobe => "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
            _ => null
        };
    }

    private string? GetInstallerDownloadUrl(DependencyType dependencyType)
    {
        return dependencyType switch
        {
            DependencyType.Python => "https://www.python.org/ftp/python/3.11.0/python-3.11.0-amd64.exe",
            DependencyType.VapourSynth => "https://github.com/vapoursynth/vapoursynth/releases/latest", // Needs to be parsed
            _ => null
        };
    }

    private string GetInstallerSilentArgs(DependencyType dependencyType)
    {
        return dependencyType switch
        {
            DependencyType.Python => "/quiet InstallAllUsers=1 PrependPath=1",
            DependencyType.VapourSynth => "/S", // NSIS silent install
            _ => "/S"
        };
    }

    private string? GetDownloadUrl(DependencyType dependencyType)
    {
        return dependencyType switch
        {
            DependencyType.FFmpeg => "https://www.gyan.dev/ffmpeg/builds/",
            DependencyType.FFprobe => "https://www.gyan.dev/ffmpeg/builds/",
            DependencyType.Melt => "https://shotcut.org/download/",
            DependencyType.VapourSynth => "https://github.com/vapoursynth/vapoursynth/releases",
            DependencyType.VapourSynthSourcePlugin => "https://github.com/vapoursynth/bestsource/releases",
            DependencyType.SvpRife => "https://www.svp-team.com/get/",
            DependencyType.Python => "https://www.python.org/downloads/",
            DependencyType.PracticalRife => "https://github.com/hzwer/Practical-RIFE",
            _ => null
        };
    }

    private string GetManualInstructions(DependencyType dependencyType)
    {
        return dependencyType switch
        {
            DependencyType.Melt => "Install Shotcut from https://shotcut.org/download/\nShotcut includes the 'melt' executable required for rendering projects.",
            DependencyType.VapourSynth => "Download and run the VapourSynth installer.\nIt will add 'vspipe' to your system PATH automatically.",
            DependencyType.VapourSynthSourcePlugin => "Download BestSource.dll from the releases page.\nCopy it to: C:\\Program Files\\VapourSynth\\plugins\\",
            DependencyType.SvpRife => "Install SVP 4 Pro and select the 'RIFE AI engine' component during installation.\nAlso install VapourSynth separately.",
            DependencyType.PracticalRife => "Clone the repository: git clone https://github.com/hzwer/Practical-RIFE.git\nInstall dependencies: pip install torch torchvision opencv-python numpy",
            _ => $"Please install {dependencyType} manually."
        };
    }

    private string? FindExecutableInDirectory(string directory, DependencyType dependencyType)
    {
        var executableNames = dependencyType switch
        {
            DependencyType.FFmpeg => new[] { "ffmpeg.exe" },
            DependencyType.FFprobe => new[] { "ffprobe.exe" },
            _ => Array.Empty<string>()
        };

        foreach (var execName in executableNames)
        {
            var files = Directory.GetFiles(directory, execName, SearchOption.AllDirectories);
            if (files.Length > 0)
            {
                return files[0];
            }
        }

        return null;
    }

    private void ReportProgress(int percentage, string message)
    {
        Debug.WriteLine($"[Install Progress {percentage}%] {message}");
        ProgressChanged?.Invoke(percentage, message);
    }
}
