# Developer Documentation

Internal architecture and implementation details.

---

## Architecture Overview

### Architecture Documentation

- **[Graceful Shutdown Architecture](architecture/graceful-shutdown.md)** - Comprehensive guide to task cancellation and cleanup patterns

### Core Services

1. **MeltRenderService** - MLT/Shotcut project rendering (CPU multi-threading)
2. **FFmpegRenderService** - Video encoding with NVENC hardware acceleration
3. **RifeInterpolationService** - AI frame interpolation via VapourSynth
4. **RealEsrganService** - AI upscaling via VapourSynth
5. **RenderQueueService** - Background job queue with persistence

### Processing Pipeline

```
Source File
    ↓
[Step 1: MLT Render] (if .mlt source)
    ↓ (IntermediatePath)
[Step 2: RIFE Interpolation] (if enabled)
    ↓ (OutputPath or IntermediatePath2)
[Step 3: Real-ESRGAN Upscaling] (if enabled)
    ↓
Final Output
```

**Flags:**
- `RenderType` - `MltSource` or `VideoSource` (auto-detected from file extension)
- `UseRifeInterpolation` - Boolean checkbox
- `UseRealEsrgan` - Boolean checkbox
- `IsTwoStageRender` - True for any 2-stage combination
- `IsThreeStageRender` - True for MLT → RIFE → ESRGAN

---

## Queue Architecture

### Technology Stack
- `Channel<T>` - In-memory message queue
- `BackgroundService` - Long-running background worker
- SQLite + WAL mode - Persistent job storage
- Entity Framework Core - ORM

### Job Lifecycle

```
1. User creates job → AddRenderJobDialog
2. Job saved to SQLite (status: Pending)
3. Channel<T>.Writer signals new job
4. BackgroundService picks up job
5. Job status: Pending → Running
6. Execute pipeline (MLT/RIFE/ESRGAN)
7. Progress events → Blazor UI (via EventCallback)
8. Job status: Running → Completed/Failed
9. Retry logic on failure (max 3 retries)
10. Dead letter queue after retries exhausted
```

### Crash Recovery
On startup, the service:
1. Scans SQLite for jobs with status = Running
2. Checks if ProcessId + MachineName match current process
3. If match → resume job
4. If mismatch → mark as Failed (stale job from crashed process)

---

## VapourSynth Integration

Both RIFE and Real-ESRGAN use VapourSynth for video processing.

### Script Generation

```csharp
// C# generates Python script
var script = $@"
import vapoursynth as vs
from vsrife import RIFE
core = vs.core

clip = core.bs.VideoSource(source=r'{inputPath}')
clip = RIFE(clip, {multiplier}, 1.0, None, None, None, {modelId}, backend, ...)
clip.set_output()
";

// Execute via vspipe.exe
var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "vspipe",
        Arguments = $"--y4m - - < script.vpy | ffmpeg -i pipe: -c:v hevc_nvenc ...",
        RedirectStandardOutput = true,
        RedirectStandardError = true
    }
};
```

### Progress Tracking
- vspipe outputs to stderr: `Frame 1234/5000 (24.68%)`
- Regex pattern extracts frame numbers
- Progress events throttled to 100ms (10 fps max) to prevent UI glitching

---

## Dependency Management

Automated detection, validation, and installation of required tools via the DependencyChecker service and Dependency Manager UI.

### DependencyChecker Service

The `DependencyChecker` service (`Services/DependencyChecker.cs`) provides comprehensive dependency detection and validation:

- **Automated Detection**: Integrates with ExecutableDetectionService and SvpDetectionService
- **VapourSynthEnvironment Integration**: Reports actually-used Python and VapourSynth (not just PATH detection)
- **SVP Python Detection**: Automatically detects SVP's bundled Python installation
- **Real-time Validation**: Version checking, compatibility verification, path validation
- **Detailed Status**: Provides installation paths, versions, and error messages

### Dependency Manager UI

The Dependency Manager page (`Components/Pages/DependencyManager.razor`) provides a user-friendly interface:

- **Status Dashboard**: Overall health percentage and summary
- **Categorized View**: Required vs Optional dependencies
- **One-click Installation**: "Install Missing" button for batch installation
- **Individual Control**: Install or check each dependency separately
- **Real-time Updates**: Progress tracking during installation

### Detection Strategy
1. **Check PATH** - Standard system executables
2. **Check Registry** - Installed applications
3. **Check Common Locations** - Hardcoded paths for known apps
4. **SVP Detection** - Special logic for SVP 4 installation and Python
5. **VapourSynth Environment** - Detects VapourSynth and Python via VapourSynthEnvironment service

### Installation Strategies
1. **Chocolatey** - Package manager (requires admin)
2. **Portable** - Download ZIP and extract
3. **Installer** - Download EXE and run
4. **Manual** - User installs with provided instructions

### Dependencies

**Required:**
- FFmpeg (video encoding)
- FFprobe (video analysis)
- Melt (Shotcut rendering)

**Optional:**
- VapourSynth (RIFE + ESRGAN + Real-CUGAN)
- VapourSynth Source Plugin (video loading: BestSource, L-SMASH, FFMS2)
- SVP RIFE (TensorRT RIFE implementation)
- Python 3.8-3.11 (for vsrealesrgan and vsmlrt, auto-detected from SVP if available)
- Practical-RIFE (standalone RIFE)

---

## Hardware Detection

### GPU Detection
- Query WMI: `SELECT * FROM Win32_VideoController`
- Parse for NVIDIA/AMD/Intel
- Check for NVENC availability (RTX cards)

### CPU Detection
- `Environment.ProcessorCount` for core count
- Parse CPU model from WMI
- Calculate optimal thread count

### Recommendations
```csharp
// MLT: Always use CPU
public bool UseMeltNvenc => false;

// FFmpeg: Use NVENC if available
public bool UseFFmpegNvenc => NvencAvailable && HasNvidiaGpu;

// Thread counts
public int MeltThreads => ProcessorCount; // All cores
public int FFmpegThreads => 2; // Minimal (NVENC does the work)
```

---

## Progress Reporting

### Throttling Strategy
Progress updates are throttled to prevent UI overload:

```csharp
private DateTime _lastProgressUpdate = DateTime.UtcNow;

private void ReportProgress(double percentage)
{
    var now = DateTime.UtcNow;
    if ((now - _lastProgressUpdate).TotalMilliseconds < 100)
        return; // Throttle to 100ms (10 fps)

    _lastProgressUpdate = now;
    OnProgressChanged?.Invoke(percentage);
}
```

### Event Chain
1. External process writes to stderr/stdout
2. DataReceivedEventHandler captures output
3. Parse progress (frame numbers, percentage, etc.)
4. Throttle and fire event
5. RenderQueueService receives event
6. Update SQLite job record
7. Fire Blazor event callback
8. UI updates (MudProgressLinear)

---

## Error Handling

### Retry Logic
Uses Polly for resilient execution:

```csharp
var retryPolicy = Policy
    .Handle<Exception>()
    .WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // 2s, 4s, 8s
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            Debug.WriteLine($"Retry {retryCount} after {timeSpan}");
        });

await retryPolicy.ExecuteAsync(async () =>
{
    await ExecuteRenderAsync(job);
});
```

### Failure Recovery
1. Catch exception during render
2. Store error message + stack trace in RenderJob
3. Increment RetryCount
4. If RetryCount < MaxRetries (3) → retry
5. Else → move to Failed status (dead letter)

---

## Testing

### Test Documentation

Complete test documentation is available at [../CheapShotcutRandomizer.Tests/README.md](../CheapShotcutRandomizer.Tests/README.md)

### Unit Tests
- BUnit for Blazor component testing
- Moq for mocking services
- FluentAssertions for readable assertions

### Test Structure
```csharp
[Fact]
public void RenderJob_CalculatesProgressCorrectly()
{
    // Arrange
    var job = new RenderJob
    {
        CurrentFrame = 500,
        TotalFrames = 1000
    };

    // Act
    var percentage = job.ProgressPercentage;

    // Assert
    percentage.Should().Be(50.0);
}
```

---

## Key Learnings

### MLT NVENC is Broken
Agent research initially recommended NVENC for all encoding. **This was wrong for MLT.**
- MLT's NVENC has single-threaded bottleneck
- 2x SLOWER than CPU multi-threading on 12-core CPU
- Use CPU for MLT, NVENC for FFmpeg only

### VapourSynth Parameter Changes
RIFE parameter syntax changed between versions:
- Old: `RIFE(clip, tiles_w=None, tiles_h=None)` ❌
- New: `RIFE(clip, multiplier, scale, None, None, None, ...)` ✅
- SVP's helpers.py uses positional arguments

### Progress Throttling is Critical
Without throttling:
- 1000s of events per second
- Blazor UI freezes/glitches
- Database write contention
- Solution: 100ms throttle (10 fps max)

---

## File Locations

**Note:** All file paths listed below are relative to the project root: `C:\Users\Brech\source\repos\ShotcutRandomizer`

### Services
- `Services/MeltRenderService.cs` - MLT rendering
- `Services/FFmpegRenderService.cs` - Video encoding
- `Services/RIFE/RifeInterpolationService.cs` - RIFE via VapourSynth
- `Services/RealESRGAN/RealEsrganService.cs` - ESRGAN via VapourSynth
- `Services/Queue/RenderQueueService.cs` - Background queue
- `Services/HardwareDetectionService.cs` - GPU/CPU detection
- `Services/ExecutableDetectionService.cs` - Dependency detection
- `Services/DependencyChecker.cs` - Comprehensive dependency validation and status reporting
- `Services/DependencyInstaller.cs` - Automated dependency installation
- `Services/VapourSynth/VapourSynthEnvironment.cs` - VapourSynth and Python environment detection

### UI Components
- `Components/Pages/RenderQueue.razor` - Main queue UI
- `Components/Shared/AddRenderJobDialog.razor` - Job creation dialog
- `Components/Shared/RenderJobCard.razor` - Individual job card
- `Components/Pages/Settings.razor` - Settings page
- `Components/Pages/DependencyManager.razor` - Dependency management UI
- `Components/Shared/DependencyListItem.razor` - Individual dependency status card

### Models
- `Models/RenderJob.cs` - Job entity
- `Models/RenderType.cs` - Source type enum
- `Models/AppSettings.cs` - Application settings
- `Models/DependencyInfo.cs` - Dependency metadata and status
- `Models/DependencyType.cs` - Dependency type enumeration
- `Models/DependencyStatus.cs` - Overall dependency health status
- `Models/InstallationResult.cs` - Installation operation result

---

## Database Schema

### RenderJobs Table

```sql
CREATE TABLE RenderJobs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    JobId TEXT NOT NULL UNIQUE,
    SourceVideoPath TEXT NOT NULL,
    OutputPath TEXT NOT NULL,
    RenderType INTEGER NOT NULL, -- 0=MltSource, 1=VideoSource
    Status INTEGER NOT NULL, -- 0=Pending, 1=Running, 2=Completed, 3=Failed
    UseRifeInterpolation INTEGER NOT NULL, -- Boolean
    UseRealEsrgan INTEGER NOT NULL, -- Boolean
    IsTwoStageRender INTEGER NOT NULL,
    IsThreeStageRender INTEGER NOT NULL,
    IntermediatePath TEXT,
    IntermediatePath2 TEXT,
    RenderSettings TEXT NOT NULL, -- JSON
    RealEsrganOptionsJson TEXT,
    ProgressPercentage REAL NOT NULL,
    CurrentFrame INTEGER NOT NULL,
    TotalFrames INTEGER,
    CurrentStage TEXT,
    CreatedAt TEXT NOT NULL,
    StartedAt TEXT,
    CompletedAt TEXT,
    LastError TEXT,
    RetryCount INTEGER NOT NULL DEFAULT 0,
    MaxRetries INTEGER NOT NULL DEFAULT 3
);
```

---

## Contributing

When adding new features:

1. Follow existing patterns (Service → Queue → UI)
2. Add progress reporting with 100ms throttle
3. Write unit tests
4. Update this documentation
5. Test multi-stage pipelines (MLT→RIFE→ESRGAN)
