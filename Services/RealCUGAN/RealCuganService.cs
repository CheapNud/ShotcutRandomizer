using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CheapHelpers.Extensions;
using CheapShotcutRandomizer.Models;

namespace CheapShotcutRandomizer.Services.RealCUGAN;

/// <summary>
/// Service wrapper for Real-CUGAN AI upscaling (anime/cartoon optimized)
/// Uses VapourSynth + vs-mlrt plugin with TensorRT/CUDA acceleration
/// Real-CUGAN is 10-13x faster than Real-ESRGAN (~10-20 fps vs ~1 fps on RTX 3080)
/// Architecture matches RealEsrganService for consistency
/// </summary>
public class RealCuganService
{
    private readonly string _pythonPath;

    public RealCuganService(string pythonPath = "")
    {
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

        Debug.WriteLine($"RealCuganService initialized with Python: {_pythonPath}");
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
    /// Validate that vs-mlrt is installed and available
    /// </summary>
    public async Task<bool> ValidateInstallationAsync()
    {
        try
        {
            // Check if Python can import vsmlrt
            var testProcess = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = "-c \"from vsmlrt import CUGAN, Backend; print('OK')\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(testProcess);
            if (process == null)
            {
                Debug.WriteLine("Failed to start Python process for vs-mlrt validation");
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var errorText = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || !output.Contains("OK"))
            {
                Debug.WriteLine($"vs-mlrt validation failed: {errorText}");
                return false;
            }

            Debug.WriteLine("vs-mlrt installation validated successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error validating vs-mlrt installation: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Upscale video using Real-CUGAN via VapourSynth + vs-mlrt pipeline
    /// </summary>
    public async Task<bool> UpscaleVideoAsync(
        string inputVideoPath,
        string outputVideoPath,
        RealCuganOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        string? ffmpegPath = null)
    {
        if (!File.Exists(inputVideoPath))
            throw new FileNotFoundException($"Input video not found: {inputVideoPath}");

        // Validate noise/scale compatibility
        if (!options.IsNoiseScaleCompatible())
            throw new InvalidOperationException($"Noise level {options.Noise} is not compatible with scale {options.Scale}x. Noise levels 1 and 2 only work with 2x scale.");

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(outputVideoPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        Debug.WriteLine($"Starting Real-CUGAN upscaling: {inputVideoPath} → {outputVideoPath}");
        Debug.WriteLine($"Scale: {options.Scale}x, Noise: {options.Noise}, Backend: {RealCuganOptions.GetBackendDisplayName(options.Backend)}, FP16: {options.UseFp16}");

        // Check for vspipe (VapourSynth's command-line tool)
        var vspipePath = FindVsPipe();
        if (string.IsNullOrEmpty(vspipePath))
        {
            throw new FileNotFoundException("vspipe.exe not found. Please install VapourSynth.");
        }

        // Create VapourSynth script for Real-CUGAN
        var tempScriptPath = Path.Combine(Path.GetTempPath(), $"realcugan_{Guid.NewGuid()}.vpy");

        try
        {
            // Generate VapourSynth script
            var scriptContent = GenerateRealCuganScript(inputVideoPath, options);
            await File.WriteAllTextAsync(tempScriptPath, scriptContent, cancellationToken);

            Debug.WriteLine($"Created VapourSynth script: {tempScriptPath}");

            // Test if the script loads properly (important for first-time model downloads)
            Debug.WriteLine("Testing VapourSynth script (may download model on first run)...");

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
                    // Use async event-based output reading to prevent deadlock
                    var testOutputBuilder = new System.Text.StringBuilder();
                    var testErrorBuilder = new System.Text.StringBuilder();

                    test.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            testOutputBuilder.AppendLine(e.Data);
                            Debug.WriteLine($"[VapourSynth] {e.Data}");
                        }
                    };

                    test.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            testErrorBuilder.AppendLine(e.Data);
                            Debug.WriteLine($"[VapourSynth Error] {e.Data}");
                        }
                    };

                    test.BeginOutputReadLine();
                    test.BeginErrorReadLine();

                    // Wait up to 10 minutes for model download on first run
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                    try
                    {
                        await test.WaitForExitAsync(timeoutCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine("VapourSynth script test timed out after 10 minutes");
                        try { test.Kill(); } catch { }
                        throw new TimeoutException("VapourSynth script test timed out. Model download may have failed.");
                    }

                    var testOutput = testOutputBuilder.ToString();
                    var testError = testErrorBuilder.ToString();

                    // Check if we got valid video information (ignore warnings)
                    // VapourSynth often outputs warnings to stderr that don't prevent operation
                    bool scriptValid = testOutput.Contains("Width:") &&
                                      testOutput.Contains("Height:") &&
                                      testOutput.Contains("Frames:");

                    // Log stdout output for debugging
                    if (!string.IsNullOrWhiteSpace(testOutput))
                    {
                        Debug.WriteLine($"VapourSynth stdout output: {testOutput}");
                    }
                    else
                    {
                        Debug.WriteLine("VapourSynth stdout is empty");
                    }

                    // Log any warnings/errors from stderr
                    if (!string.IsNullOrWhiteSpace(testError))
                    {
                        Debug.WriteLine($"VapourSynth stderr output: {testError}");
                    }

                    if (!scriptValid)
                    {
                        Debug.WriteLine($"VapourSynth script validation failed - no valid video info in output");
                        Debug.WriteLine($"Script valid check: Width={testOutput.Contains("Width:")}, Height={testOutput.Contains("Height:")}, Frames={testOutput.Contains("Frames:")}");

                        // Check for Python errors (import errors, exceptions, etc.)
                        bool hasPythonError = testError.Contains("Error:") ||
                                             testError.Contains("Exception") ||
                                             testError.Contains("Traceback") ||
                                             testError.Contains("ModuleNotFoundError") ||
                                             testError.Contains("ImportError");

                        if (hasPythonError)
                        {
                            Debug.WriteLine($"❌ Python error detected in VapourSynth script");
                            throw new InvalidOperationException($"VapourSynth script failed with Python error:\n{testError}");
                        }

                        // Check if TensorRT failed - automatically fall back to CUDA backend
                        if (options.Backend == 0 && // TensorRT backend
                            (testError.Contains("TensorRT failed to load") ||
                             testError.Contains("nvinfer") ||
                             testError.Contains("errno 126")))
                        {
                            Debug.WriteLine("⚠️ TensorRT not available - falling back to CUDA backend (ORT_CUDA)");
                            Debug.WriteLine("💡 For best performance, install TensorRT from: https://developer.nvidia.com/tensorrt");

                            // Retry with CUDA backend
                            var fallbackOptions = new RealCuganOptions
                            {
                                Noise = options.Noise,
                                Scale = options.Scale,
                                Backend = 1, // ORT_CUDA
                                UseFp16 = options.UseFp16,
                                GpuId = options.GpuId,
                                NumStreams = options.NumStreams
                            };

                            // Regenerate script with CUDA backend
                            var fallbackScript = GenerateRealCuganScript(inputVideoPath, fallbackOptions);
                            await File.WriteAllTextAsync(tempScriptPath, fallbackScript, cancellationToken);
                            Debug.WriteLine("Regenerated VapourSynth script with CUDA backend");

                            // Test again with CUDA backend
                            var retryTest = new ProcessStartInfo
                            {
                                FileName = vspipePath,
                                Arguments = $"--info \"{tempScriptPath}\"",
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };

                            using var retryProcess = Process.Start(retryTest);
                            if (retryProcess != null)
                            {
                                // Use async event-based output reading to prevent deadlock
                                var retryOutputBuilder = new System.Text.StringBuilder();
                                var retryErrorBuilder = new System.Text.StringBuilder();

                                retryProcess.OutputDataReceived += (sender, e) =>
                                {
                                    if (!string.IsNullOrEmpty(e.Data))
                                    {
                                        retryOutputBuilder.AppendLine(e.Data);
                                        Debug.WriteLine($"[VapourSynth CUDA] {e.Data}");
                                    }
                                };

                                retryProcess.ErrorDataReceived += (sender, e) =>
                                {
                                    if (!string.IsNullOrEmpty(e.Data))
                                    {
                                        retryErrorBuilder.AppendLine(e.Data);
                                        Debug.WriteLine($"[VapourSynth CUDA Error] {e.Data}");
                                    }
                                };

                                retryProcess.BeginOutputReadLine();
                                retryProcess.BeginErrorReadLine();

                                using var retryCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                                try
                                {
                                    await retryProcess.WaitForExitAsync(retryCts.Token);
                                }
                                catch (OperationCanceledException)
                                {
                                    Debug.WriteLine("CUDA backend test timed out");
                                    try { retryProcess.Kill(); } catch { }
                                    throw new TimeoutException("CUDA backend test timed out. Model download may have failed.");
                                }

                                var retryOutput = retryOutputBuilder.ToString();
                                var retryError = retryErrorBuilder.ToString();

                                // Check if CUDA backend produced valid video info
                                bool cudaValid = retryOutput.Contains("Width:") &&
                                                retryOutput.Contains("Height:") &&
                                                retryOutput.Contains("Frames:");

                                if (!string.IsNullOrWhiteSpace(retryError))
                                {
                                    Debug.WriteLine($"CUDA backend stderr: {retryError}");
                                }

                                if (!cudaValid)
                                {
                                    Debug.WriteLine($"CUDA backend validation failed - no valid video info");
                                    throw new InvalidOperationException($"Failed to load VapourSynth script with both TensorRT and CUDA backends:\n\nTensorRT error:\n{testError}\n\nCUDA error:\n{retryError}");
                                }

                                Debug.WriteLine($"✅ CUDA backend validated successfully: {retryOutput}");

                                // Update options to use CUDA for the actual processing
                                options.Backend = 1;
                            }
                        }
                        else
                        {
                            // Not a TensorRT error, or already using non-TensorRT backend
                            throw new InvalidOperationException($"Failed to load VapourSynth script: {testError}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"✅ VapourSynth script validated successfully");
                        if (options.Backend == 0)
                        {
                            Debug.WriteLine($"✅ TensorRT backend is working - maximum performance enabled!");
                        }
                        Debug.WriteLine($"Video info: {testOutput.Substring(0, Math.Min(200, testOutput.Length))}...");
                    }
                }
            }

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

            // Run vspipe → FFmpeg pipeline
            Debug.WriteLine("Starting Real-CUGAN processing pipeline...");

            var vspipeProcess = new ProcessStartInfo
            {
                FileName = vspipePath,
                Arguments = $"\"{tempScriptPath}\" - -c y4m",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Use high-quality encoding settings for upscaled output
            var ffmpegProcess = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                Arguments = $"-i - -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -y \"{outputVideoPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Debug.WriteLine($"Pipeline: {vspipeProcess.FileName} {vspipeProcess.Arguments} | {ffmpegProcess.FileName} {ffmpegProcess.Arguments}");

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
            }, cancellationToken);

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
            }, cancellationToken);

            // Monitor FFmpeg output
            var ffmpegMonitorTask = Task.Run(async () =>
            {
                string? line;
                while ((line = await ffmpeg.StandardError.ReadLineAsync()) != null)
                {
                    Debug.WriteLine($"[ffmpeg] {line}");
                }
            }, cancellationToken);

            // Wait for all tasks to complete
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
            else
            {
                Debug.WriteLine($"Real-CUGAN upscaling completed successfully: {outputVideoPath}");
            }

            return success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Real-CUGAN upscaling failed: {ex.Message}");
            throw new InvalidOperationException($"Real-CUGAN processing failed: {ex.Message}", ex);
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
    /// Generate VapourSynth script for Real-CUGAN upscaling
    /// Uses vs-mlrt Python wrapper API
    /// </summary>
    private string GenerateRealCuganScript(string inputVideoPath, RealCuganOptions options)
    {
        // Backend configuration
        // Note: Python uses capitalized True/False, not lowercase true/false
        var backendCode = options.Backend switch
        {
            0 => $"Backend.TRT(fp16={options.UseFp16.ToString().Capitalize()}, device_id={options.GpuId}, num_streams={options.NumStreams})",
            1 => $"Backend.ORT_CUDA(device_id={options.GpuId}, cudnn_benchmark=True, num_streams={options.NumStreams})",
            2 => "Backend.OV_CPU()",
            _ => $"Backend.TRT(fp16={options.UseFp16.ToString().Capitalize()}, device_id={options.GpuId}, num_streams={options.NumStreams})"
        };

        return $@"
import vapoursynth as vs
import sys
import os

core = vs.core

# Add VapourSynth scripts folder to Python path for vsmlrt import
scripts_path = os.path.join(os.environ['APPDATA'], 'VapourSynth', 'scripts')
if scripts_path not in sys.path:
    sys.path.insert(0, scripts_path)

# Try to import vs-mlrt (vsmlrt Python wrapper)
try:
    from vsmlrt import CUGAN, Backend
except ImportError as e:
    raise Exception('vsmlrt not installed. Run: pip install vsmlrt')

# Load video - try multiple source filters
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
                    'See REALCUGAN_INSTALLATION.md for details.'
                )

# Get video properties
width = clip.width
height = clip.height
fps = clip.fps

# Apply Real-CUGAN upscaling via vs-mlrt
try:
    # CUGAN requires RGBS (RGB float32) or RGBH (RGB float16) format
    # Convert the clip to the appropriate format
    # matrix_in_s='709' for HD content (Rec. 709 color space)
    clip = core.resize.Bicubic(clip, format=vs.RGBS, matrix_in_s='709')

    # Configure backend for processing
    backend = {backendCode}

    # Apply Real-CUGAN upscaling
    # CUGAN(clip, noise, scale, backend)
    # - noise: -1 (none), 0 (conservative), 1 (light), 2 (medium), 3 (aggressive)
    # - scale: 2, 3, or 4 (note: noise 1/2 only work with scale=2)
    clip = CUGAN(
        clip,
        noise={options.Noise},
        scale={options.Scale},
        backend=backend
    )

except Exception as e:
    import traceback
    error_msg = f'Real-CUGAN upscaling failed: {{str(e)}}'
    print(error_msg, file=sys.stderr)
    traceback.print_exc()
    raise

# Output the processed clip
clip.set_output()
";
    }

    /// <summary>
    /// Check if Real-CUGAN is available and properly configured
    /// </summary>
    public async Task<bool> IsRealCuganAvailableAsync()
    {
        try
        {
            // Check if Python is available
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
            await pythonCheck.WaitForExitAsync();

            if (pythonCheck.ExitCode != 0)
            {
                Debug.WriteLine("Python not found or not working");
                return false;
            }

            // Check if vs-mlrt is installed
            return await ValidateInstallationAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Real-CUGAN availability check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get list of available noise levels with descriptions
    /// </summary>
    public static (int Value, string Description)[] GetAvailableNoiseLevels()
    {
        return RealCuganOptions.GetAvailableNoiseLevels()
            .Select(n => (n, RealCuganOptions.GetNoiseDisplayName(n)))
            .ToArray();
    }

    /// <summary>
    /// Get list of available scale factors with descriptions
    /// </summary>
    public static (int Value, string Description)[] GetAvailableScales()
    {
        return RealCuganOptions.GetAvailableScaleFactors()
            .Select(s => (s, RealCuganOptions.GetScaleDisplayName(s)))
            .ToArray();
    }
}
