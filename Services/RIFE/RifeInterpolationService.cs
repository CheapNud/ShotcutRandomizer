using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CheapShotcutRandomizer.Services.RIFE;

/// <summary>
/// Service wrapper for rife-ncnn-vulkan executable
/// Handles frame interpolation using RIFE AI models
/// </summary>
public class RifeInterpolationService
{
    private readonly string _rifeExecutablePath;

    public RifeInterpolationService(string rifeExecutablePath = "rife-ncnn-vulkan.exe")
    {
        _rifeExecutablePath = rifeExecutablePath;
    }

    /// <summary>
    /// Interpolate frames in a directory
    /// Example: 1000 input frames → 2000 output frames (2x interpolation)
    /// </summary>
    public async Task<bool> InterpolateFramesAsync(
        string inputFramesFolder,
        string outputFramesFolder,
        RifeOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(inputFramesFolder))
            throw new DirectoryNotFoundException($"Input frames folder not found: {inputFramesFolder}");

        Directory.CreateDirectory(outputFramesFolder);

        var arguments = options.BuildArguments(inputFramesFolder, outputFramesFolder);

        Debug.WriteLine($"Starting RIFE interpolation: {_rifeExecutablePath} {arguments}");

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = _rifeExecutablePath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };

            // Track progress from output
            var progressPattern = new Regex(@"(\d+\.\d+)%");
            var lastProgress = 0.0;

            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;

                Debug.WriteLine($"RIFE: {e.Data}");

                // Try to parse progress percentage
                var match = progressPattern.Match(e.Data);
                if (match.Success && double.TryParse(match.Groups[1].Value, out var percentage))
                {
                    if (percentage > lastProgress)
                    {
                        lastProgress = percentage;
                        progress?.Report(percentage);
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Debug.WriteLine($"RIFE Error: {e.Data}");
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for completion with cancellation support
            await WaitForExitAsync(process, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error killing RIFE process: {ex.Message}");
                }
                return false;
            }

            var success = process.ExitCode == 0;

            if (success)
            {
                progress?.Report(100.0);
                Debug.WriteLine("RIFE interpolation completed successfully");
            }
            else
            {
                Debug.WriteLine($"RIFE interpolation failed with exit code: {process.ExitCode}");
            }

            return success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RIFE interpolation error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Wait for process to exit with cancellation support
    /// </summary>
    private static async Task WaitForExitAsync(Process process, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();

        void ProcessExited(object? sender, EventArgs e) => tcs.TrySetResult(true);

        process.EnableRaisingEvents = true;
        process.Exited += ProcessExited;

        try
        {
            if (process.HasExited)
            {
                return;
            }

            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                await tcs.Task;
            }
        }
        finally
        {
            process.Exited -= ProcessExited;
        }
    }

    /// <summary>
    /// Check if RIFE executable is available
    /// </summary>
    public bool IsRifeAvailable()
    {
        try
        {
            // Try to find rife-ncnn-vulkan in PATH or current directory
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _rifeExecutablePath,
                    Arguments = "-h",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(3000); // 3 second timeout

            return process.ExitCode == 0 || process.ExitCode == 1; // Some versions return 1 for -h
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get available RIFE models by checking model directory
    /// </summary>
    public List<string> GetAvailableModels()
    {
        // Common RIFE models
        return
        [
            "rife-v4.6",
            "rife-v4.22",
            "rife-v4.21",
            "rife-v4.20",
            "rife-v4.18",
            "rife-v4.17",
            "rife-v4.16-lite",
            "rife-v4.15-lite",
            "rife-v4",
            "rife-v3.1",
            "rife-v3.0",
            "rife-v2.4",
            "rife-v2.3",
            "rife-v2",
            "rife-anime",
            "rife-HD",
            "rife-UHD",
            "rife"
        ];
    }
}
