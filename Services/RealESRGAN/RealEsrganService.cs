using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CheapShotcutRandomizer.Models;
using CheapShotcutRandomizer.Services.Utilities;

namespace CheapShotcutRandomizer.Services.RealESRGAN;

/// <summary>
/// Service wrapper for Real-ESRGAN AI upscaling
/// Uses VapourSynth + vsrealesrgan plugin with TensorRT/CUDA acceleration
/// Matches the architecture of RifeInterpolationService for consistency
/// </summary>
public class RealEsrganService
{
    private readonly string _pythonPath;

    public RealEsrganService(string pythonPath = "")
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

        Debug.WriteLine($"RealEsrganService initialized with Python: {_pythonPath}");
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
    /// Validate that vsrealesrgan is installed and available
    /// </summary>
    public async Task<bool> ValidateInstallationAsync()
    {
        try
        {
            // Check if Python can import vsrealesrgan
            var testProcess = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = "-c \"import vsrealesrgan; print('OK')\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(testProcess);
            if (process == null)
            {
                Debug.WriteLine("Failed to start Python process for vsrealesrgan validation");
                return false;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var errorText = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 || !output.Contains("OK"))
            {
                Debug.WriteLine($"vsrealesrgan validation failed: {errorText}");
                return false;
            }

            Debug.WriteLine("vsrealesrgan installation validated successfully");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error validating vsrealesrgan installation: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Upscale video using Real-ESRGAN via VapourSynth pipeline
    /// </summary>
    public async Task<bool> UpscaleVideoAsync(
        string inputVideoPath,
        string outputVideoPath,
        RealEsrganOptions options,
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

        Debug.WriteLine($"Starting Real-ESRGAN upscaling: {inputVideoPath} → {outputVideoPath}");
        Debug.WriteLine($"Model: {options.ModelName}, Scale: {options.ScaleFactor}x, Tile: {options.TileSize}px");

        // Check for vspipe (VapourSynth's command-line tool)
        var vspipePath = FindVsPipe();
        if (string.IsNullOrEmpty(vspipePath))
        {
            throw new FileNotFoundException("vspipe.exe not found. Please install VapourSynth.");
        }

        // Create VapourSynth script for Real-ESRGAN
        var tempScriptPath = Path.Combine(Path.GetTempPath(), $"realesrgan_{Guid.NewGuid()}.vpy");

        try
        {
            // Generate VapourSynth script
            var scriptContent = GenerateRealEsrganScript(inputVideoPath, options);
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
                    var testOutput = await test.StandardOutput.ReadToEndAsync();
                    var testError = await test.StandardError.ReadToEndAsync();

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

                    if (test.ExitCode != 0)
                    {
                        Debug.WriteLine($"VapourSynth script test failed: {testError}");
                        throw new InvalidOperationException($"Failed to load VapourSynth script: {testError}");
                    }

                    Debug.WriteLine($"VapourSynth script validated: {testOutput}");
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
            Debug.WriteLine("Starting Real-ESRGAN processing pipeline...");

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

            // Register cancellation handlers for graceful shutdown
            var vspipeCancellation = cancellationToken.Register(async () =>
            {
                Debug.WriteLine("Real-ESRGAN cancelled - shutting down vspipe...");
                await ProcessManager.GracefulShutdownAsync(vspipe, gracefulTimeoutMs: 3000, processName: "vspipe (Real-ESRGAN)");
            });

            var ffmpegCancellation = cancellationToken.Register(async () =>
            {
                Debug.WriteLine("Real-ESRGAN cancelled - shutting down ffmpeg...");
                await ProcessManager.GracefulShutdownAsync(ffmpeg, gracefulTimeoutMs: 2000, processName: "ffmpeg (Real-ESRGAN)");
            });

            try
            {
                // Pipe vspipe stdout to ffmpeg stdin
                var pipeTask = Task.Run(async () =>
                {
                    try
                    {
                        await vspipe.StandardOutput.BaseStream.CopyToAsync(ffmpeg.StandardInput.BaseStream, cancellationToken);
                        ffmpeg.StandardInput.Close();
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine("[Real-ESRGAN] Pipe operation cancelled");
                    }
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
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Real-ESRGAN processing cancelled");
                throw;
            }
            finally
            {
                vspipeCancellation.Dispose();
                ffmpegCancellation.Dispose();
            }

            var success = vspipe.ExitCode == 0 && ffmpeg.ExitCode == 0;

            if (!success)
            {
                Debug.WriteLine($"Processing failed - vspipe: {vspipe.ExitCode}, ffmpeg: {ffmpeg.ExitCode}");
            }
            else
            {
                Debug.WriteLine($"Real-ESRGAN upscaling completed successfully: {outputVideoPath}");
            }

            return success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Real-ESRGAN upscaling failed: {ex.Message}");
            throw new InvalidOperationException($"Real-ESRGAN processing failed: {ex.Message}", ex);
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
    /// Generate VapourSynth script for Real-ESRGAN upscaling
    /// </summary>
    private string GenerateRealEsrganScript(string inputVideoPath, RealEsrganOptions options)
    {
        // Convert model name to vsrealesrgan RealESRGANModel enum name
        var modelEnumName = options.ModelName switch
        {
            "RealESRGAN_x4plus" => "RealESRGAN_x4plus",
            "RealESRGAN_x4plus_anime_6B" => "RealESRGAN_x4plus_anime_6B",
            "RealESRGAN_x2plus" => "RealESRGAN_x2plus",
            "realesr-general-x4v3" => "realesr_general_x4v3",
            "RealESRGAN_AnimeVideo-v3" => "RealESRGAN_AnimeVideo_v3",
            _ => "RealESRGAN_x4plus" // Default to x4plus
        };

        // Tile size - new API expects [width, height]
        var tileParam = options.TileMode
            ? $"[{options.TileSize}, {options.TileSize}]"
            : "None";

        // FP16 mode is now done via clip format (RGBH = FP16, RGBS = FP32)
        var clipFormat = options.UseFp16 ? "vs.RGBH" : "vs.RGBS";

        return $@"
import vapoursynth as vs
import sys
import os

core = vs.core

# Try to import vsrealesrgan
try:
    from vsrealesrgan import realesrgan, RealESRGANModel
except ImportError as e:
    raise Exception('vsrealesrgan not installed. Run: pip install vsrealesrgan')

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
                    'See REAL_ESRGAN_INSTALLATION.md for details.'
                )

# Get video properties
width = clip.width
height = clip.height
fps = clip.fps

# Apply Real-ESRGAN upscaling
try:
    # Convert to RGB format (required by vsrealesrgan)
    # RGBH = FP16 precision, RGBS = FP32 precision
    clip = core.resize.Bicubic(clip, format={clipFormat}, matrix_in_s='709')

    # New vsrealesrgan API:
    # realesrgan(clip, device_index, model, tile, tile_pad, trt, auto_download)
    # - No 'scale' parameter (scale is built into model name)
    # - 'tile' is now [width, height] list, not single int
    # - 'device_id' renamed to 'device_index'
    # - FP16 mode is done via clip format (RGBH), not parameter
    clip = realesrgan(
        clip,
        device_index={options.GpuId},
        model=RealESRGANModel.{modelEnumName},
        tile={tileParam},
        tile_pad={options.TilePad},
        trt=False,
        auto_download=True
    )

    # Convert back to YUV420P8 for output
    clip = core.resize.Bicubic(clip, format=vs.YUV420P8, matrix_s='709')
except Exception as e:
    import traceback
    error_msg = f'Real-ESRGAN upscaling failed: {{str(e)}}'
    print(error_msg, file=sys.stderr)
    traceback.print_exc()
    raise

# Output the processed clip
clip.set_output()
";
    }

    /// <summary>
    /// Check if Real-ESRGAN is available and properly configured
    /// </summary>
    public async Task<bool> IsRealEsrganAvailableAsync()
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

            // Check if vsrealesrgan is installed
            return await ValidateInstallationAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Real-ESRGAN availability check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get list of available Real-ESRGAN models
    /// </summary>
    public static string[] GetAvailableModels()
    {
        return RealEsrganOptions.GetAvailableModels();
    }
}
