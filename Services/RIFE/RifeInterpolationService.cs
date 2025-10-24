using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace CheapShotcutRandomizer.Services.RIFE;

/// <summary>
/// Service wrapper for RIFE (Real-Time Intermediate Flow Estimation)
/// Supports:
/// - SVP's integrated RIFE (VapourSynth-based with TensorRT)
/// - Practical-RIFE standalone (https://github.com/hzwer/Practical-RIFE)
/// </summary>
public class RifeInterpolationService
{
    private readonly string _rifeFolderPath;
    private readonly bool _isSvpRife;
    private readonly string _pythonPath;

    public RifeInterpolationService(string rifeFolderPath = "", string pythonPath = "")
    {
        _rifeFolderPath = rifeFolderPath;

        // Auto-detect Python path if not specified
        if (string.IsNullOrEmpty(pythonPath))
        {
            // On Windows, try "python" first, then "python3"
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _pythonPath = IsPythonAvailable("python") ? "python" :
                              IsPythonAvailable("python3") ? "python3" : "python";
            }
            else
            {
                _pythonPath = "python3";
            }
        }
        else
        {
            _pythonPath = pythonPath;
        }

        // Detect if this is SVP's RIFE or a GitHub clone
        _isSvpRife = DetectRifeType();

        ValidateFolderPath();
    }

    /// <summary>
    /// Check if Python is available in PATH
    /// </summary>
    private bool IsPythonAvailable(string pythonCommand)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonCommand,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(2000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Detect if this is SVP's RIFE (has rife.dll) or GitHub RIFE (has inference_video.py)
    /// </summary>
    private bool DetectRifeType()
    {
        if (string.IsNullOrEmpty(_rifeFolderPath))
            return false;

        // Check for SVP's RIFE files
        if (File.Exists(Path.Combine(_rifeFolderPath, "rife.dll")) ||
            File.Exists(Path.Combine(_rifeFolderPath, "rife_vs.dll")))
        {
            Debug.WriteLine("Detected SVP's RIFE installation");
            return true;
        }

        // Check for GitHub RIFE
        if (File.Exists(Path.Combine(_rifeFolderPath, "inference_video.py")))
        {
            Debug.WriteLine("Detected GitHub RIFE repository");
            return false;
        }

        Debug.WriteLine("Unknown RIFE installation type");
        return false;
    }

    /// <summary>
    /// Validate that RIFE folder exists and contains required files
    /// </summary>
    private void ValidateFolderPath()
    {
        if (string.IsNullOrEmpty(_rifeFolderPath))
        {
            Debug.WriteLine("WARNING: RIFE folder path not configured");
            throw new DirectoryNotFoundException("RIFE folder path not configured");
        }

        if (!Directory.Exists(_rifeFolderPath))
        {
            Debug.WriteLine($"WARNING: RIFE folder not found at: {_rifeFolderPath}");
            throw new DirectoryNotFoundException($"RIFE folder not found: {_rifeFolderPath}");
        }

        // Validate based on type
        if (_isSvpRife)
        {
            // Check for SVP RIFE files
            var requiredFiles = new[] { "rife.dll", "rife_vs.dll", "vsmirt.py", "vstrt.dll" };
            var foundAny = requiredFiles.Any(f => File.Exists(Path.Combine(_rifeFolderPath, f)));

            if (!foundAny)
            {
                Debug.WriteLine($"WARNING: SVP RIFE files not found in: {_rifeFolderPath}");
                throw new FileNotFoundException($"SVP RIFE files not found in: {_rifeFolderPath}");
            }
        }
        else
        {
            // Check for GitHub RIFE files
            var scriptPath = Path.Combine(_rifeFolderPath, "inference_video.py");
            if (!File.Exists(scriptPath))
            {
                Debug.WriteLine($"WARNING: inference_video.py not found in: {_rifeFolderPath}");
                throw new FileNotFoundException($"inference_video.py not found in: {_rifeFolderPath}");
            }
        }
    }

    /// <summary>
    /// Interpolate video using RIFE (direct video-to-video)
    /// </summary>
    public async Task<bool> InterpolateVideoAsync(
        string inputVideoPath,
        string outputVideoPath,
        RifeOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        string? ffmpegPath = null)
    {
        if (!File.Exists(inputVideoPath))
            throw new FileNotFoundException($"Input video not found: {inputVideoPath}");

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputVideoPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        string arguments;
        string pythonScript;

        if (_isSvpRife)
        {
            // SVP's RIFE uses VapourSynth integration
            Debug.WriteLine("Attempting SVP RIFE interpolation via VapourSynth...");

            // Check for vspipe (VapourSynth's command-line tool)
            var vspipePath = FindVsPipe();
            if (string.IsNullOrEmpty(vspipePath))
            {
                throw new FileNotFoundException("vspipe.exe not found. Please install VapourSynth or ensure it's in PATH.");
            }

            // Create a VapourSynth script for SVP RIFE
            var tempScriptPath = Path.Combine(Path.GetTempPath(), $"svp_rife_{Guid.NewGuid()}.vpy");
            var tempOutputPath = Path.ChangeExtension(tempScriptPath, ".y4m");

            try
            {
                // Generate VapourSynth script for SVP RIFE
                var scriptContent = GenerateSvpRifeScript(inputVideoPath, options);
                await File.WriteAllTextAsync(tempScriptPath, scriptContent);

                Debug.WriteLine($"Created VapourSynth script: {tempScriptPath}");

                // First, test if the script loads properly
                // NOTE: This can take 5-15 minutes on first run as TensorRT compiles CUDA kernels
                var testProcess = new ProcessStartInfo
                {
                    FileName = vspipePath,
                    Arguments = $"--info \"{tempScriptPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var test = Process.Start(testProcess))
                {
                    if (test != null)
                    {
                        Debug.WriteLine("Testing VapourSynth script (TensorRT initialization may take 5-15 minutes on first run)...");

                        var testOutput = await test.StandardOutput.ReadToEndAsync();
                        var testError = await test.StandardError.ReadToEndAsync();

                        // Wait up to 20 minutes for TensorRT to compile on first run
                        var timeoutMs = 20 * 60 * 1000; // 20 minutes
                        var completed = test.WaitForExit(timeoutMs);

                        if (!completed)
                        {
                            Debug.WriteLine("VapourSynth script test timed out after 20 minutes");
                            try { test.Kill(); } catch { }
                            throw new TimeoutException("VapourSynth script test timed out. TensorRT initialization may have failed.");
                        }

                        if (test.ExitCode != 0)
                        {
                            Debug.WriteLine($"VapourSynth script test failed: {testError}");
                            throw new InvalidOperationException($"Failed to load VapourSynth script: {testError}");
                        }

                        Debug.WriteLine($"VapourSynth script info: {testOutput}");
                    }
                }

                // Now run the actual processing with vspipe piped to FFmpeg
                // vspipe script.vpy - -c y4m | ffmpeg -i - -c:v libx264 output.mp4
                var vspipeProcess = new ProcessStartInfo
                {
                    FileName = vspipePath,
                    Arguments = $"\"{tempScriptPath}\" - -c y4m",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Determine FFmpeg path to use
                var ffmpegExe = ffmpegPath ?? "ffmpeg";

                // Try to find SVP's FFmpeg if not provided
                if (string.IsNullOrEmpty(ffmpegPath) || ffmpegPath == "ffmpeg")
                {
                    var svpFFmpeg = @"C:\Program Files (x86)\SVP 4\utils\ffmpeg.exe";
                    if (File.Exists(svpFFmpeg))
                    {
                        ffmpegExe = svpFFmpeg;
                    }
                }

                var ffmpegProcess = new ProcessStartInfo
                {
                    FileName = ffmpegExe,
                    Arguments = $"-i - -c:v libx264 -preset fast -crf 18 -y \"{outputVideoPath}\"",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Debug.WriteLine($"Running: {vspipeProcess.FileName} {vspipeProcess.Arguments} | {ffmpegProcess.FileName} {ffmpegProcess.Arguments}");

                // Start both processes and pipe vspipe output to ffmpeg input
                using var vspipe = Process.Start(vspipeProcess);
                using var ffmpeg = Process.Start(ffmpegProcess);

                if (vspipe == null || ffmpeg == null)
                {
                    throw new InvalidOperationException("Failed to start vspipe or ffmpeg process");
                }

                // Pipe vspipe stdout to ffmpeg stdin
                var pipeTask = Task.Run(async () =>
                {
                    await vspipe.StandardOutput.BaseStream.CopyToAsync(ffmpeg.StandardInput.BaseStream, cancellationToken);
                    ffmpeg.StandardInput.Close();
                });

                // Monitor progress from vspipe stderr
                var progressTask = Task.Run(async () =>
                {
                    string? line;
                    var framePattern = new Regex(@"Frame:\s*(\d+)/(\d+)");

                    while ((line = await vspipe.StandardError.ReadLineAsync()) != null)
                    {
                        Debug.WriteLine($"[vspipe] {line}");

                        var match = framePattern.Match(line);
                        if (match.Success &&
                            int.TryParse(match.Groups[1].Value, out var current) &&
                            int.TryParse(match.Groups[2].Value, out var total) &&
                            total > 0)
                        {
                            progress?.Report((double)current / total * 100);
                        }
                    }
                });

                // Monitor FFmpeg output
                var ffmpegMonitorTask = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await ffmpeg.StandardError.ReadLineAsync()) != null)
                    {
                        Debug.WriteLine($"[ffmpeg] {line}");
                    }
                });

                // Wait for both processes
                await Task.WhenAll(
                    vspipe.WaitForExitAsync(cancellationToken),
                    ffmpeg.WaitForExitAsync(cancellationToken),
                    pipeTask,
                    progressTask,
                    ffmpegMonitorTask
                );

                var success = vspipe.ExitCode == 0 && ffmpeg.ExitCode == 0;

                if (!success)
                {
                    Debug.WriteLine($"Processing failed - vspipe: {vspipe.ExitCode}, ffmpeg: {ffmpeg.ExitCode}");
                }

                return success;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SVP RIFE VapourSynth processing failed: {ex.Message}");
                throw new InvalidOperationException($"SVP RIFE processing failed: {ex.Message}", ex);
            }
            finally
            {
                // Clean up temp script
                if (File.Exists(tempScriptPath))
                {
                    try { File.Delete(tempScriptPath); } catch { }
                }
            }
        }
        else
        {
            // GitHub Practical-RIFE uses inference_video.py
            pythonScript = Path.Combine(_rifeFolderPath, "inference_video.py");

            // Build arguments for inference_video.py (Practical-RIFE)
            // python3 inference_video.py --video=input.mp4 --output=output.mp4 --multi=2
            arguments = $"\"{pythonScript}\" --video=\"{inputVideoPath}\" --output=\"{outputVideoPath}\" --multi={options.InterpolationMultiplier}";

            // Add optional parameters for Practical-RIFE
            if (!string.IsNullOrEmpty(options.ModelName))
            {
                // Extract version from model name (e.g., "rife-v4.22" → "4.22")
                var modelVersion = options.ModelName.Replace("rife-v", "").Replace("rife-", "");

                // Practical-RIFE uses model parameter directly
                arguments += $" --model={modelVersion}";
            }

            if (options.Scale > 0 && options.Scale != 1.0)
            {
                arguments += $" --scale={options.Scale:F1}";
            }

            // Practical-RIFE uses --uhd (lowercase) instead of --UHD
            if (options.UhdMode)
            {
                arguments += " --uhd";
            }

            // GPU selection for Practical-RIFE
            if (options.GpuId >= 0)
            {
                arguments += $" --gpu={options.GpuId}";
            }
        }

        Debug.WriteLine($"Starting RIFE interpolation: {_pythonPath} {arguments}");

        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = arguments,
                WorkingDirectory = _rifeFolderPath,  // Important: run from RIFE folder
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };

            // Track progress from output
            var progressPattern = new Regex(@"(\d+)/(\d+)");
            var percentPattern = new Regex(@"(\d+)%");

            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrEmpty(e.Data))
                    return;

                Debug.WriteLine($"[RIFE] {e.Data}");

                // Try to extract progress
                var percentMatch = percentPattern.Match(e.Data);
                if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, out var percent))
                {
                    progress?.Report(percent);
                }
                else
                {
                    var progressMatch = progressPattern.Match(e.Data);
                    if (progressMatch.Success &&
                        int.TryParse(progressMatch.Groups[1].Value, out var current) &&
                        int.TryParse(progressMatch.Groups[2].Value, out var total) &&
                        total > 0)
                    {
                        progress?.Report((double)current / total * 100);
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Debug.WriteLine($"[RIFE ERROR] {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for completion with cancellation support
            while (!process.HasExited)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                    return false;
                }

                await Task.Delay(100, cancellationToken);
            }

            var success = process.ExitCode == 0;

            if (success && !_isSvpRife)
            {
                // Practical-RIFE outputs with --output parameter, so file should be at outputVideoPath
                // But if not specified, it creates a file with suffix pattern
                if (!File.Exists(outputVideoPath))
                {
                    // Check for default output pattern
                    var expectedOutput = Path.Combine(
                        Path.GetDirectoryName(inputVideoPath) ?? "",
                        Path.GetFileNameWithoutExtension(inputVideoPath) + $"_{options.InterpolationMultiplier}X_" +
                        $"{options.TargetFps}fps.mp4"
                    );

                    if (File.Exists(expectedOutput))
                    {
                        File.Move(expectedOutput, outputVideoPath, overwrite: true);
                        Debug.WriteLine($"Moved RIFE output from {expectedOutput} to {outputVideoPath}");
                    }
                    else
                    {
                        Debug.WriteLine($"Warning: RIFE output file not found at expected locations");
                        success = false;
                    }
                }
            }

            return success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RIFE interpolation failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Interpolate frames in a directory (for pipeline usage)
    /// This is used when processing frames separately from video
    /// </summary>
    public async Task<bool> InterpolateFramesAsync(
        string inputFramesFolder,
        string outputFramesFolder,
        RifeOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        string? ffmpegPath = null)
    {
        if (!Directory.Exists(inputFramesFolder))
            throw new DirectoryNotFoundException($"Input frames folder not found: {inputFramesFolder}");

        Directory.CreateDirectory(outputFramesFolder);

        // Get frame files
        var frameFiles = Directory.GetFiles(inputFramesFolder, "*.png")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (frameFiles.Length == 0)
        {
            Debug.WriteLine("No PNG frames found in input folder");
            return false;
        }

        Debug.WriteLine($"Found {frameFiles.Length} frames to interpolate");

        // Use provided FFmpeg path or try to find it
        if (string.IsNullOrEmpty(ffmpegPath))
        {
            // Try common locations
            ffmpegPath = "ffmpeg"; // Fallback to PATH

            // Check SVP location
            var svpFFmpeg = @"C:\Program Files (x86)\SVP 4\utils\ffmpeg.exe";
            if (File.Exists(svpFFmpeg))
            {
                ffmpegPath = svpFFmpeg;
            }
        }

        // Create temporary video from frames
        var tempVideoIn = Path.Combine(Path.GetTempPath(), $"rife_temp_{Guid.NewGuid()}.mp4");
        var tempVideoOut = Path.Combine(Path.GetTempPath(), $"rife_out_{Guid.NewGuid()}.mp4");

        try
        {
            // Step 1: Convert frames to video using FFmpeg
            // Assuming frames are named frame_000001.png, frame_000002.png, etc.
            var framePath = Path.Combine(inputFramesFolder, "frame_%06d.png");
            // Add -y to force overwrite without prompting
            var ffmpegArgs = $"-y -framerate 30 -i \"{framePath}\" -c:v libx264 -preset fast -crf 0 -pix_fmt yuv420p \"{tempVideoIn}\"";

            Debug.WriteLine($"Creating temp video from frames: {ffmpegPath} {ffmpegArgs}");

            var ffmpegProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = ffmpegArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            // Capture stderr for debugging
            var errorOutput = new System.Text.StringBuilder();
            ffmpegProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorOutput.AppendLine(e.Data);
                    Debug.WriteLine($"[FFmpeg] {e.Data}");
                }
            };

            ffmpegProcess.Start();
            ffmpegProcess.BeginErrorReadLine();

            // Add timeout of 5 minutes for frame conversion
            var completed = ffmpegProcess.WaitForExit(300000); // 5 minutes

            if (!completed)
            {
                Debug.WriteLine("FFmpeg process timed out after 5 minutes");
                try { ffmpegProcess.Kill(); } catch { }
                return false;
            }

            if (ffmpegProcess.ExitCode != 0 || !File.Exists(tempVideoIn))
            {
                Debug.WriteLine($"FFmpeg exit code: {ffmpegProcess.ExitCode}");
                Debug.WriteLine($"FFmpeg error output: {errorOutput}");
                return false;
            }

            // Step 2: Run RIFE interpolation on the video
            progress?.Report(30); // 30% after video creation

            var interpolationSuccess = await InterpolateVideoAsync(
                tempVideoIn,
                tempVideoOut,
                options,
                new Progress<double>(p => progress?.Report(30 + p * 0.4)), // 30-70%
                cancellationToken,
                ffmpegPath);

            if (!interpolationSuccess || !File.Exists(tempVideoOut))
            {
                Debug.WriteLine("RIFE interpolation failed");
                return false;
            }

            // Step 3: Extract frames from interpolated video
            progress?.Report(70); // 70% after interpolation

            var outputFramePath = Path.Combine(outputFramesFolder, "frame_%06d.png");
            // Add -y to force overwrite
            var extractArgs = $"-y -i \"{tempVideoOut}\" \"{outputFramePath}\"";

            Debug.WriteLine($"Extracting interpolated frames: {ffmpegPath} {extractArgs}");

            var extractErrorOutput = new System.Text.StringBuilder();
            var extractProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = extractArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            extractProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    extractErrorOutput.AppendLine(e.Data);
                    Debug.WriteLine($"[FFmpeg Extract] {e.Data}");
                }
            };

            extractProcess.Start();
            extractProcess.BeginErrorReadLine();

            // Add timeout for extraction
            var extractCompleted = extractProcess.WaitForExit(300000); // 5 minutes

            if (!extractCompleted)
            {
                Debug.WriteLine("FFmpeg extraction timed out after 5 minutes");
                try { extractProcess.Kill(); } catch { }
                return false;
            }

            if (extractProcess.ExitCode != 0)
            {
                Debug.WriteLine($"FFmpeg extract exit code: {extractProcess.ExitCode}");
                Debug.WriteLine($"FFmpeg extract error: {extractErrorOutput}");
                return false;
            }

            progress?.Report(100);

            // Verify output frames were created
            var outputFrames = Directory.GetFiles(outputFramesFolder, "*.png");
            Debug.WriteLine($"Extracted {outputFrames.Length} interpolated frames");

            return outputFrames.Length > 0;
        }
        finally
        {
            // Clean up temp files
            if (File.Exists(tempVideoIn))
            {
                try { File.Delete(tempVideoIn); } catch { }
            }
            if (File.Exists(tempVideoOut))
            {
                try { File.Delete(tempVideoOut); } catch { }
            }
        }
    }

    /// <summary>
    /// Get available RIFE models (for compatibility)
    /// </summary>
    public static string[] GetAvailableModels()
    {
        return new[]
        {
            "rife-v4.6",
            "rife-v4.14",
            "rife-v4.15",
            "rife-v4.16-lite",
            "rife-v4.17",
            "rife-v4.18",
            "rife-v4.20",
            "rife-v4.21",
            "rife-v4.22",
            "rife-v4.22-lite",
            "rife-v4.25",
            "rife-v4.25-lite",
            "rife-v4.26",
            "rife-anime",
            "rife-UHD"
        };
    }

    /// <summary>
    /// Find vspipe executable
    /// </summary>
    private string? FindVsPipe()
    {
        // Check common VapourSynth installation paths
        var possiblePaths = new[]
        {
            @"C:\Program Files\VapourSynth\core\vspipe.exe",
            @"C:\Program Files (x86)\VapourSynth\core\vspipe.exe",
            @"C:\Python311\Scripts\vspipe.exe",
            @"C:\Python310\Scripts\vspipe.exe",
            @"C:\Python39\Scripts\vspipe.exe",
            @"C:\Python38\Scripts\vspipe.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"VapourSynth\core\vspipe.exe"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                Debug.WriteLine($"Found vspipe at: {path}");
                return path;
            }
        }

        // Try to find in PATH
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "vspipe",
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(2000);

            if (process.ExitCode == 0)
            {
                Debug.WriteLine("Found vspipe in PATH");
                return "vspipe";
            }
        }
        catch
        {
            // vspipe not in PATH
        }

        Debug.WriteLine("vspipe not found");
        return null;
    }

    /// <summary>
    /// Generate a VapourSynth script for SVP RIFE processing
    /// </summary>
    private string GenerateSvpRifeScript(string inputVideoPath, RifeOptions options)
    {
        // Create VapourSynth script that uses SVP's RIFE plugin
        var multiplier = options.GetFrameMultiplier();

        // SVP's RIFE uses the rife_vs.dll plugin
        // The plugin path needs to be loaded
        var pluginPath = Path.Combine(_rifeFolderPath, "rife_vs.dll");
        var modelsPath = Path.Combine(_rifeFolderPath, "models");

        // Map our model names to SVP's expected format
        var modelId = options.ModelName switch
        {
            "rife-v4.6" => 46,
            "rife-v4.14" => 414,
            "rife-v4.15" => 415,
            "rife-v4.16-lite" => 416,
            "rife-v4.17" => 417,
            "rife-v4.18" => 418,
            "rife-v4.20" => 420,
            "rife-v4.21" => 421,
            "rife-v4.22" => 422,
            "rife-v4.22-lite" => 422,
            "rife-v4.25" => 425,
            "rife-v4.25-lite" => 425,
            "rife-v4.26" => 426,
            "rife-UHD" => 49,  // UHD model
            "rife-anime" => 48,  // Anime model
            _ => 422  // Default to 4.22 (latest stable)
        };

        return $@"
import vapoursynth as vs
import sys
import os

core = vs.core

# Add SVP RIFE folder to Python path
sys.path.insert(0, r'{Path.GetDirectoryName(pluginPath)}')

# Try to load BestSource plugin explicitly if available
try:
    bs_plugin = r'C:\Program Files\VapourSynth\plugins\BestSource.dll'
    if os.path.exists(bs_plugin):
        core.std.LoadPlugin(bs_plugin)
except:
    pass

# Load SVP's RIFE plugins
try:
    core.std.LoadPlugin(r'{pluginPath}')
    core.std.LoadPlugin(r'{Path.Combine(Path.GetDirectoryName(pluginPath), "vstrt.dll")}')
    core.std.LoadPlugin(r'{Path.Combine(Path.GetDirectoryName(pluginPath), "akarin.dll")}')
except:
    pass

# Import vsmlrt module for RIFE
try:
    from vsmlrt import RIFE, Backend
except ImportError as e:
    raise Exception(f'Failed to import vsmlrt module: {{e}}. Ensure SVP RIFE is properly installed.')

# Load the video - try multiple source filters
try:
    clip = core.bs.VideoSource(source=r'{inputVideoPath}')
except:
    try:
        clip = core.ffms2.Source(r'{inputVideoPath}')
    except:
        try:
            clip = core.lsmas.LWLibavSource(r'{inputVideoPath}')
        except:
            try:
                clip = core.avisource.AVISource(r'{inputVideoPath}')
            except Exception as e:
                raise Exception(
                    'No VapourSynth source plugin found. Please install one of: '
                    'BestSource (recommended), ffms2, L-SMASH Source, or AviSource. '
                    'See RIFE_INSTALLATION.md for details.'
                )

# Get video properties
width = clip.width
height = clip.height
fps_num = clip.fps.numerator
fps_den = clip.fps.denominator

# Convert to RGB 32-bit float (RGBS) - required by vsmlrt.RIFE
clip = core.resize.Bicubic(clip, format=vs.RGBS, matrix_in_s='709')

# Pad dimensions to be divisible by 32 (required by vsmlrt)
def pad_to_multiple(dimension, multiple=32):
    remainder = dimension % multiple
    if remainder == 0:
        return dimension
    return dimension + (multiple - remainder)

padded_width = pad_to_multiple(width)
padded_height = pad_to_multiple(height)

# Pad if needed
if padded_width != width or padded_height != height:
    clip = core.resize.Bicubic(clip, width=padded_width, height=padded_height)

# Apply RIFE interpolation using SVP's vsmlrt module
try:
    # Configure TensorRT backend
    backend = Backend.TRT(
        num_streams=2,
        force_fp16=True,
        output_format=1,
        workspace=None,
        use_cuda_graph=True,
        tf32=True
    )

    # Apply RIFE using vsmlrt - use positional args like SVP's helpers.py
    clip = RIFE(clip, {multiplier}, 1.0, None, None, None, {modelId}, backend, {(options.TtaMode ? "True" : "False")}, False, None)

except Exception as e:
    # If RIFE fails, raise error so we know what happened
    import traceback
    error_msg = f'RIFE interpolation failed: {{str(e)}}'
    print(error_msg, file=sys.stderr)
    traceback.print_exc()
    raise

# Crop back to original dimensions if we padded
if padded_width != width or padded_height != height:
    clip = core.resize.Bicubic(clip, width=width, height=height)

# Convert back to YUV420P8 for output
clip = core.resize.Bicubic(clip, format=vs.YUV420P8, matrix_s='709')

# Output the processed clip
clip.set_output()
";
    }

    /// <summary>
    /// Check if RIFE is available and properly configured
    /// </summary>
    public bool IsRifeAvailable()
    {
        try
        {
            ValidateFolderPath();

            // Also check if Python is available
            var pythonCheck = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            pythonCheck.Start();
            pythonCheck.WaitForExit(5000);

            if (pythonCheck.ExitCode != 0)
            {
                Debug.WriteLine("Python not found or not working");
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}