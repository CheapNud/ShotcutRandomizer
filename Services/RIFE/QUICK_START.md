# RIFE Pipeline Quick Start Guide

## 5-Minute Setup

### Step 1: Download Prerequisites (2 minutes)

1. **FFmpeg with NVENC**
   - URL: https://github.com/BtbN/FFmpeg-Builds/releases
   - Download: `ffmpeg-n*-win64-gpl-shared.zip`
   - Extract to `C:\ffmpeg`
   - Add `C:\ffmpeg\bin` to PATH

2. **RIFE-ncnn-vulkan**
   - URL: https://github.com/nihui/rife-ncnn-vulkan/releases
   - Download: `rife-ncnn-vulkan-*-windows.zip`
   - Extract `rife-ncnn-vulkan.exe` to project directory

### Step 2: Verify Installation (1 minute)

```bash
# Check FFmpeg
ffmpeg -version

# Check NVENC support
ffmpeg -encoders | findstr nvenc

# Check RIFE (run from project directory)
rife-ncnn-vulkan.exe -h
```

### Step 3: Basic Usage (2 minutes)

```csharp
// Inject service in your component
@inject RifeVideoProcessingPipeline Pipeline

// Create options
var options = RifePipelineOptions.CreateDefault();

// Add progress tracking
var progress = new Progress<VideoProcessingProgress>(p =>
{
    Console.WriteLine($"{p.OverallProgress:F1}% - {p.CurrentStageDescription}");
});

// Process video
var success = await Pipeline.ProcessVideoAsync(
    "input.mp4",
    "output_60fps.mp4",
    options,
    progress
);
```

## Common Use Cases

### Convert 30fps to 60fps (Default)
```csharp
var options = RifePipelineOptions.CreateDefault();
await Pipeline.ProcessVideoAsync("input.mp4", "output.mp4", options);
```

### Convert 30fps to 120fps (4x)
```csharp
var options = RifePipelineOptions.CreateDefault();
options.RifeOptions.InterpolationPasses = 2; // 2^2 = 4x
await Pipeline.ProcessVideoAsync("input.mp4", "output.mp4", options);
```

### High Quality Processing
```csharp
var options = RifePipelineOptions.CreateHighQuality();
await Pipeline.ProcessVideoAsync("input.mp4", "output.mp4", options);
```

### Fast Processing
```csharp
var options = RifePipelineOptions.CreateFast();
await Pipeline.ProcessVideoAsync("input.mp4", "output.mp4", options);
```

## Expected Processing Times (RTX 3080)

| Input Video | Expected Time |
|-------------|---------------|
| 1 minute    | ~2 minutes    |
| 5 minutes   | ~10 minutes   |
| 30 minutes  | ~60 minutes   |

## Troubleshooting

### "RIFE not found"
Place `rife-ncnn-vulkan.exe` in project directory or PATH

### "NVENC not available"
Update NVIDIA drivers to version 471.41 or newer

### Processing too slow
Use fast preset: `RifePipelineOptions.CreateFast()`

### Out of memory
Close other applications or use lighter model

## Full Documentation

- **Complete Guide**: README.md
- **Examples**: EXAMPLE.cs
- **Implementation Details**: RIFE_IMPLEMENTATION_SUMMARY.md

## One-Liner Examples

```csharp
// Default (30fps → 60fps)
await Pipeline.ProcessVideoAsync("in.mp4", "out.mp4", RifePipelineOptions.CreateDefault());

// High Quality
await Pipeline.ProcessVideoAsync("in.mp4", "out.mp4", RifePipelineOptions.CreateHighQuality());

// Fast
await Pipeline.ProcessVideoAsync("in.mp4", "out.mp4", RifePipelineOptions.CreateFast());

// 4x Speed (30fps → 120fps)
var opt = RifePipelineOptions.CreateDefault();
opt.RifeOptions.InterpolationPasses = 2;
await Pipeline.ProcessVideoAsync("in.mp4", "out.mp4", opt);
```

## That's It!

You're ready to use RIFE interpolation. Check the full README.md for advanced features and configuration options.
