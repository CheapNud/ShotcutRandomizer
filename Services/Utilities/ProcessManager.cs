using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CheapShotcutRandomizer.Services.Utilities;

/// <summary>
/// Helper class for managing external processes with graceful shutdown support
/// Handles process tree cleanup for complex processes like vspipe, ffmpeg, and melt
/// </summary>
public static class ProcessManager
{
    /// <summary>
    /// Attempts graceful shutdown of a process and its entire process tree
    /// </summary>
    /// <param name="process">The process to terminate</param>
    /// <param name="gracefulTimeoutMs">How long to wait for graceful exit (default: 3000ms)</param>
    /// <param name="processName">Name of process for logging (e.g., "vspipe", "ffmpeg")</param>
    /// <returns>True if process was terminated successfully</returns>
    public static async Task<bool> GracefulShutdownAsync(
        Process? process,
        int gracefulTimeoutMs = 3000,
        string processName = "process")
    {
        if (process == null)
        {
            Debug.WriteLine($"[ProcessManager] {processName}: Process is null, nothing to shutdown");
            return true;
        }

        if (process.HasExited)
        {
            Debug.WriteLine($"[ProcessManager] {processName}: Process already exited (code: {process.ExitCode})");
            return true;
        }

        try
        {
            Debug.WriteLine($"[ProcessManager] {processName} (PID {process.Id}): Attempting graceful shutdown...");

            // Step 1: Try graceful termination first (send Ctrl+C on Windows)
            bool gracefulSuccess = await TryGracefulTerminationAsync(process, gracefulTimeoutMs);

            if (gracefulSuccess)
            {
                Debug.WriteLine($"[ProcessManager] {processName} (PID {process.Id}): Gracefully exited");
                return true;
            }

            Debug.WriteLine($"[ProcessManager] {processName} (PID {process.Id}): Graceful shutdown failed, force killing process tree...");

            // Step 2: Force kill entire process tree
            await ForceKillProcessTreeAsync(process, processName);

            // Wait briefly for process to be killed
            await Task.Delay(500);

            if (process.HasExited)
            {
                Debug.WriteLine($"[ProcessManager] {processName} (PID {process.Id}): Force killed successfully");
                return true;
            }
            else
            {
                Debug.WriteLine($"[ProcessManager] {processName} (PID {process.Id}): WARNING - Process may still be running");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessManager] {processName}: Error during shutdown: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Try to gracefully terminate a process by sending Ctrl+C or close signal
    /// </summary>
    private static async Task<bool> TryGracefulTerminationAsync(Process process, int timeoutMs)
    {
        try
        {
            // On Windows, try to close main window first (if it has one)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!process.HasExited && process.MainWindowHandle != IntPtr.Zero)
                {
                    Debug.WriteLine($"[ProcessManager] Sending WM_CLOSE to main window...");
                    process.CloseMainWindow();

                    // Wait for graceful exit
                    var exited = await WaitForExitAsync(process, timeoutMs);
                    if (exited)
                    {
                        return true;
                    }
                }

                // Try sending Ctrl+C via AttachConsole
                // Note: This is complex on Windows and may not work for all processes
                // For now, we'll just wait a bit and check if process exits
                Debug.WriteLine($"[ProcessManager] Waiting {timeoutMs}ms for graceful exit...");
                var waited = await WaitForExitAsync(process, timeoutMs);
                return waited;
            }
            else
            {
                // On Linux/Mac, send SIGTERM
                Debug.WriteLine($"[ProcessManager] Sending SIGTERM...");
                // Note: Process.Kill() on Unix with entireProcessTree:false sends SIGTERM by default
                process.Kill(entireProcessTree: false);

                var exited = await WaitForExitAsync(process, timeoutMs);
                return exited;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessManager] Error during graceful termination: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Force kill the process and its entire process tree
    /// This handles child processes spawned by vspipe (Python, PyTorch, CUDA), ffmpeg, etc.
    /// </summary>
    private static async Task ForceKillProcessTreeAsync(Process process, string processName)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            var processId = process.Id;

            // Kill entire process tree (.NET 5+ feature)
            try
            {
                Debug.WriteLine($"[ProcessManager] {processName}: Killing process tree for PID {processId}...");
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process already exited
                Debug.WriteLine($"[ProcessManager] {processName}: Process already exited during kill");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessManager] {processName}: Error killing process tree: {ex.Message}");

                // Fallback: Try killing just the main process
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch (Exception killEx)
                {
                    Debug.WriteLine($"[ProcessManager] {processName}: Error killing main process: {killEx.Message}");
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessManager] {processName}: Error in ForceKillProcessTreeAsync: {ex.Message}");
        }
    }

    /// <summary>
    /// Wait for process to exit with async support
    /// </summary>
    private static async Task<bool> WaitForExitAsync(Process process, int timeoutMs)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            // Timeout
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ProcessManager] Error waiting for process exit: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Create a cancellation callback that performs graceful shutdown
    /// Use this with CancellationToken.Register()
    /// </summary>
    /// <param name="process">Process to shutdown when cancelled</param>
    /// <param name="processName">Name for logging</param>
    /// <returns>Action to register with CancellationToken</returns>
    public static Action CreateGracefulShutdownCallback(Process process, string processName = "process")
    {
        return () =>
        {
            Debug.WriteLine($"[ProcessManager] Cancellation requested for {processName}");

            // Run graceful shutdown synchronously (we're in a callback)
            // Use shorter timeout since we're in cancellation context
            var shutdownTask = GracefulShutdownAsync(process, gracefulTimeoutMs: 2000, processName);

            // Wait for shutdown (blocking, but we're already in cancellation)
            try
            {
                shutdownTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessManager] Error during cancellation shutdown: {ex.Message}");
            }
        };
    }

    /// <summary>
    /// Cleanup temporary files associated with a process
    /// Call this after process termination
    /// </summary>
    /// <param name="tempFiles">List of temporary file paths to delete</param>
    /// <param name="processName">Process name for logging</param>
    public static void CleanupTempFiles(IEnumerable<string> tempFiles, string processName = "process")
    {
        foreach (var tempFile in tempFiles)
        {
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                    Debug.WriteLine($"[ProcessManager] {processName}: Cleaned up temp file: {tempFile}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ProcessManager] {processName}: Failed to delete temp file {tempFile}: {ex.Message}");
            }
        }
    }
}
