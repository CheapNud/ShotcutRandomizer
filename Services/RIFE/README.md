# RIFE Video Interpolation Pipeline

Complete AI-powered video frame interpolation pipeline for CheapShotcutRandomizer.

## Overview

This pipeline takes input videos and interpolates frames using RIFE AI (Real-Time Intermediate Flow Estimation), converting 30fps videos to 60fps, 120fps, or higher using NVIDIA RTX 3080 GPU acceleration.

## Features

- **RIFE AI Interpolation**: State-of-the-art frame interpolation using rife-ncnn-vulkan
- **NVENC Hardware Acceleration**: 8-10x faster video encoding using RTX 3080
- **Auto Hardware Detection**: Automatically configures optimal settings for RTX 3080
- **Progress Tracking**: Real-time progress reporting at each pipeline stage
- **Validation**: Automatic validation of input/output and frame counts
- **Error Handling**: Helpful error messages with suggestions
- **Cancellation Support**: Graceful cancellation at all stages
- **Automatic Cleanup**: Temporary files cleaned up even on errors

## Pipeline Stages

1. **Analyzing** (0-2%): Validate input video and extract metadata
2. **Extracting Audio** (2-5%): Losslessly extract audio track
3. **Extracting Frames** (5-20%): Extract frames as PNG images
4. **Interpolating Frames** (20-80%): RIFE AI interpolation (slowest stage)
5. **Reassembling Video** (80-100%): Reassemble with NVENC hardware acceleration

## Installation

### Prerequisites

1. **FFmpeg with NVENC support**
   - Download: https://github.com/BtbN/FFmpeg-Builds/releases
   - Get: `ffmpeg-n*-win64-gpl-shared.zip`
   - Add to PATH or place in application directory

2. **RIFE-ncnn-vulkan**
   - Download: https://github.com/nihui/rife-ncnn-vulkan/releases
   - Get: `rife-ncnn-vulkan-*-windows.zip`
   - Extract `rife-ncnn-vulkan.exe` to application directory or PATH
   - Models are included in the download

3. **NVIDIA Drivers**
   - Version 471.41 or newer required for NVENC
   - Download: https://www.nvidia.com/drivers

## Usage

### Basic Example

```csharp
using CheapShotcutRandomizer.Services.RIFE;
using Microsoft.Extensions.DependencyInjection;

// Get services from DI container
var pipeline = serviceProvider.GetRequiredService<RifeVideoProcessingPipeline>();

// Create options (auto-detect RTX 3080 settings)
var options = RifePipelineOptions.CreateDefault();

// Progress tracking
var progress = new Progress<VideoProcessingProgress>(p =>
{
    Console.WriteLine($"{p.CurrentStageDescription}");
    Console.WriteLine($"Stage: {p.StageProgress:F1}% | Overall: {p.OverallProgress:F1}%");
});

// Process video
var success = await pipeline.ProcessVideoAsync(
    inputPath: "input.mp4",
    outputPath: "output_60fps.mp4",
    options: options,
    progress: progress,
    cancellationToken: CancellationToken.None
);

if (success)
{
    Console.WriteLine("Video interpolation completed successfully!");
}
```

### Advanced Example - High Quality

```csharp
// High quality settings
var options = RifePipelineOptions.CreateHighQuality();

// Customize RIFE options
options.RifeOptions.ModelName = "rife-v4.22"; // Latest model
options.RifeOptions.TtaMode = true; // Test-time augmentation (slower but better)
options.RifeOptions.InterpolationPasses = 1; // 2x (30fps → 60fps)

// Customize FFmpeg settings
options.FFmpegSettings = new FFmpegRenderSettings
{
    UseHardwareAcceleration = true,
    VideoCodec = "hevc_nvenc",
    NvencPreset = "p7", // Best quality
    Quality = 18, // Near-lossless
    FrameRate = 60
};

var success = await pipeline.ProcessVideoAsync(
    "input.mp4",
    "output_high_quality.mp4",
    options,
    progress
);
```

### Advanced Example - Fast Processing

```csharp
// Fast processing settings
var options = RifePipelineOptions.CreateFast();

// Use lighter RIFE model
options.RifeOptions.ModelName = "rife-v4.15-lite";

// Faster NVENC preset
options.FFmpegSettings = new FFmpegRenderSettings
{
    UseHardwareAcceleration = true,
    NvencPreset = "p4", // Faster
    Quality = 23
};

var success = await pipeline.ProcessVideoAsync(
    "input.mp4",
    "output_fast.mp4",
    options,
    progress
);
```

### Advanced Example - 4x Interpolation

```csharp
// Convert 30fps → 120fps (4x interpolation)
var options = RifePipelineOptions.CreateDefault();
options.RifeOptions.InterpolationPasses = 2; // 2^2 = 4x

// Output will be 120fps
var success = await pipeline.ProcessVideoAsync(
    "input_30fps.mp4",
    "output_120fps.mp4",
    options,
    progress
);
```

### With Cancellation Support

```csharp
using var cts = new CancellationTokenSource();

// Cancel after 5 minutes
cts.CancelAfter(TimeSpan.FromMinutes(5));

// Or cancel from UI button
// cancelButton.Click += (s, e) => cts.Cancel();

var success = await pipeline.ProcessVideoAsync(
    "input.mp4",
    "output.mp4",
    options,
    progress,
    cts.Token
);

if (!success)
{
    Console.WriteLine("Processing cancelled or failed");
}
```

### Estimate Processing Time

```csharp
var estimatedTime = await pipeline.EstimateProcessingTimeAsync(
    "input.mp4",
    options
);

Console.WriteLine($"Estimated processing time: {estimatedTime.TotalMinutes:F1} minutes");
```

## Configuration Options

### RifeOptions

```csharp
public class RifeOptions
{
    // Model selection
    public string ModelName { get; set; } = "rife-v4.6";

    // GPU configuration
    public int GpuId { get; set; } = 0; // 0 = RTX 3080, -1 = CPU

    // Performance tuning
    public string ThreadConfig { get; set; } = "2:2:2";
    public int TileSize { get; set; } = 0; // 0 = auto

    // Quality options
    public bool TtaMode { get; set; } = false; // Better quality, 4x slower
    public bool UhMode { get; set; } = false; // For 4K+ content

    // Interpolation multiplier
    public int InterpolationPasses { get; set; } = 1; // 1=2x, 2=4x, 3=8x
}
```

### Available RIFE Models

- `rife-v4.22` - Latest, best quality (recommended for high quality)
- `rife-v4.6` - Good balance (default)
- `rife-v4.15-lite` - Lightweight, faster
- `rife-v4` - Older but stable
- `rife-anime` - Optimized for anime content
- `rife-UHD` - For 4K+ content

### FFmpegRenderSettings

```csharp
public class FFmpegRenderSettings
{
    // Hardware acceleration (CRITICAL for speed)
    public bool UseHardwareAcceleration { get; set; } = true;

    // Output frame rate
    public int FrameRate { get; set; } = 60;

    // NVENC settings (if hardware acceleration enabled)
    public string VideoCodec { get; set; } = "hevc_nvenc"; // or "h264_nvenc"
    public string NvencPreset { get; set; } = "p7"; // p1-p7 (p7=best)
    public string RateControl { get; set; } = "vbr";
    public int Quality { get; set; } = 19; // 0-51, lower=better

    // CPU settings (fallback)
    public string CpuPreset { get; set; } = "medium";
}
```

## Performance Benchmarks

**Hardware**: RTX 3080 + Ryzen 9 5900X

| Video Length | Input FPS | Output FPS | Processing Time | Real-time Ratio |
|--------------|-----------|------------|-----------------|-----------------|
| 1 minute     | 30        | 60         | ~2 minutes      | 0.5x            |
| 5 minutes    | 30        | 60         | ~10 minutes     | 0.5x            |
| 30 minutes   | 30        | 60         | ~60 minutes     | 0.5x            |

**NVENC vs CPU Encoding** (reassembly stage only):
- NVENC: 500+ fps @ 1080p HEVC (8-10x faster)
- CPU: 30-60 fps @ 1080p HEVC

**Bottleneck**: RIFE interpolation is the slowest stage (60% of total time).

## Error Handling

The pipeline includes comprehensive error handling with helpful suggestions:

```csharp
try
{
    var success = await pipeline.ProcessVideoAsync(...);
    if (!success)
    {
        // Check Debug.WriteLine output for detailed error messages
    }
}
catch (FileNotFoundException)
{
    Console.WriteLine("Input video not found");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Processing cancelled by user");
}
```

Common errors are automatically detected and provide suggestions:
- File not found → Check file path
- NVENC not available → Update drivers
- Out of memory → Close other applications
- Corrupt file → Re-download input
- Codec not supported → Install full FFmpeg

## Temporary Files

Temporary files are stored in:
```
%TEMP%\ShotcutRandomizer\{GUID}\
├── audio.m4a (extracted audio)
├── input_frames\ (original frames)
└── output_frames\ (interpolated frames)
```

Cleanup is automatic, but you can keep files for debugging:

```csharp
options.KeepTemporaryFiles = true;
```

## Troubleshooting

### RIFE not found

Download `rife-ncnn-vulkan.exe` from https://github.com/nihui/rife-ncnn-vulkan/releases and place in:
- Application directory, or
- Any folder in PATH, or
- Specify custom path:

```csharp
var rifeService = new RifeInterpolationService(@"C:\path\to\rife-ncnn-vulkan.exe");
```

### NVENC not available

1. Update NVIDIA drivers (471.41+)
2. Check FFmpeg has NVENC: `ffmpeg -encoders | findstr nvenc`
3. Install FFmpeg with NVENC support

### Out of VRAM

For 4K+ content on RTX 3080 (10GB VRAM):

```csharp
options.RifeOptions.TileSize = 512; // Reduce VRAM usage
options.RifeOptions.UhMode = true; // Enable UHD mode
```

### Processing too slow

Use fast preset:

```csharp
var options = RifePipelineOptions.CreateFast();
options.RifeOptions.ModelName = "rife-v4.15-lite";
options.RifeOptions.TtaMode = false;
```

### Quality not good enough

Use high quality preset:

```csharp
var options = RifePipelineOptions.CreateHighQuality();
options.RifeOptions.ModelName = "rife-v4.22";
options.RifeOptions.TtaMode = true;
options.FFmpegSettings.Quality = 18;
```

## Integration with Dependency Injection

Services are registered in `Program.cs`:

```csharp
// RIFE services
builder.Services.AddScoped<RifeInterpolationService>(sp =>
    new RifeInterpolationService("rife-ncnn-vulkan.exe"));
builder.Services.AddScoped<RifeVideoProcessingPipeline>();

// Utility services
builder.Services.AddScoped<VideoValidator>();
builder.Services.AddScoped<FFmpegErrorHandler>();
```

Inject into your components:

```csharp
public class VideoProcessingPage
{
    private readonly RifeVideoProcessingPipeline _pipeline;

    public VideoProcessingPage(RifeVideoProcessingPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public async Task ProcessVideo()
    {
        var options = RifePipelineOptions.CreateDefault();
        await _pipeline.ProcessVideoAsync("input.mp4", "output.mp4", options);
    }
}
```

## See Also

- [RIFE Project](https://github.com/megvii-research/ECCV2022-RIFE)
- [rife-ncnn-vulkan](https://github.com/nihui/rife-ncnn-vulkan)
- [FFmpeg NVENC Guide](https://docs.nvidia.com/video-technologies/video-codec-sdk/ffmpeg-with-nvidia-gpu/)
