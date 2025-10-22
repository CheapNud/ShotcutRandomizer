# Hardware Acceleration Guide for RTX 3080 + Ryzen 9 5900X

## CRITICAL: When to Use GPU vs CPU

### ❌ DO NOT USE NVENC: melt (MLT Framework) Rendering

**Why?** MLT's NVENC implementation is BROKEN:
- Single-threaded CPU bottleneck
- GPU memory transfer overhead kills performance
- 2x SLOWER than CPU multi-threading
- Only 2 CPU cores utilized vs all 12

**For your Ryzen 9 5900X:** Use CPU multi-threading instead
- 12 cores @ 100% = way faster than broken NVENC
- Better quality (libx264/libx265 vs NVENC)

```bash
# CORRECT: CPU rendering with melt
melt project.mlt -progress2 -consumer avformat:output.mp4 \
  vcodec=libx264 preset=medium crf=23 real_time=-12
```

### ✅ ABSOLUTELY USE NVENC: FFmpeg Direct Rendering (RIFE Workflow)

**Why?** FFmpeg's NVENC implementation ROCKS:
- RTX 3080 NVENC: ~500 fps @ 1080p HEVC
- Ryzen 9 5900X libx265: ~30-60 fps
- **8-10x faster encoding**
- 4% CPU usage vs 100% all cores

**For your RTX 3080:** Let that beast flex
- Dedicated hardware encoder
- Minimal CPU overhead
- Perfect for RIFE frame reassembly

```bash
# CORRECT: GPU rendering with FFmpeg
ffmpeg -framerate 60 -i frame_%06d.png -i audio.m4a \
  -c:v hevc_nvenc -preset p7 -rc vbr -cq 19 \
  -c:a copy output.mp4
```

---

## Performance Comparison (Your Hardware)

### Scenario 1: Render 1-hour MLT Project

| Method | Time | CPU Usage | GPU Usage |
|--------|------|-----------|-----------|
| melt + NVENC ❌ | ~4 hours | 2 cores @ 100% | 5% |
| melt + CPU ✅ | ~2 hours | 12 cores @ 100% | 0% |

**Winner: CPU** (MLT's NVENC is broken)

### Scenario 2: Reassemble RIFE Frames (10,000 frames)

| Method | Time | CPU Usage | GPU Usage |
|--------|------|-----------|-----------|
| FFmpeg + libx265 ❌ | ~30 min | 12 cores @ 100% | 0% |
| FFmpeg + NVENC ✅ | ~3 min | 1 core @ 20% | 95% |

**Winner: NVENC** (10x faster, FFmpeg implementation is solid)

---

## Configuration Recommendations

### For Render Queue Service (melt-based):

```csharp
public class MeltRenderSettings
{
    // RTX 3080 present but DON'T USE IT for melt
    public bool UseHardwareAcceleration { get; set; } = false; // ALWAYS FALSE

    // Use all 12 cores of Ryzen 9 5900X
    public int ThreadCount { get; set; } = Environment.ProcessorCount; // 12

    // Quality settings
    public string VideoCodec { get; set; } = "libx264"; // or "libx265"
    public string Preset { get; set; } = "medium"; // fast/medium/slow
    public int Crf { get; set; } = 23; // 18-23 recommended
}
```

### For RIFE Pipeline (FFmpeg-based):

```csharp
public class RifeRenderSettings
{
    // RTX 3080 - HELL YES USE IT
    public bool UseHardwareAcceleration { get; set; } = true; // ALWAYS TRUE

    // NVENC settings
    public string VideoCodec { get; set; } = "hevc_nvenc"; // or "h264_nvenc"
    public string NvencPreset { get; set; } = "p7"; // p1=fast, p7=slow/best
    public string RateControl { get; set; } = "vbr"; // vbr or cq
    public int Quality { get; set; } = 19; // 18-23 recommended

    // CPU barely used
    public int ThreadCount { get; set; } = 2; // Just for demux/mux
}
```

---

## Detection Code

```csharp
public class HardwareCapabilities
{
    public bool HasNvidiaGpu { get; set; }
    public string GpuModel { get; set; }
    public int CpuCoreCount { get; set; }
    public bool NvencAvailable { get; set; }

    // Different recommendations for different workflows
    public bool ShouldUseMeltNvenc => false; // NEVER
    public bool ShouldUseFFmpegNvenc => NvencAvailable; // ALWAYS if available
}
```

---

## Bottom Line

1. **melt rendering**: CPU multi-threading ONLY
2. **FFmpeg RIFE pipeline**: NVENC all day every day
3. Your RTX 3080 is a BEAST for FFmpeg, useless for melt
4. Your Ryzen 9 5900X crushes melt rendering
