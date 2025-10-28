# Cheap Shotcut Randomizer

Desktop app for randomizing and generating optimized Shotcut video project playlists using simulated annealing algorithms, with AI-powered frame interpolation and upscaling.

## Features

### Playlist Management
- **Shuffle Playlists** - Randomly reorder clips with one click
- **Generate Smart Compilations** - Create optimized playlists from multiple sources
- **Advanced Controls** - Fine-tune selection with duration and clip count weights
- **Non-Destructive** - Original projects are never modified

### Video Processing Pipeline
- **Three-Stage Rendering** - MLT → RIFE → Upscale pipeline with automatic intermediate file management
- **Multi-track Support** - Select specific video and audio tracks for rendering
- **Background Queue** - Queue multiple render jobs with persistent SQLite storage
- **Crash Recovery** - Resume interrupted jobs automatically on startup

### AI Upscaling
- **Real-CUGAN** - Anime/cartoon optimized upscaling (10-13x faster than ESRGAN)
  - TensorRT acceleration support
  - 10-20 fps processing on RTX 3080
  - Multiple denoising levels
  - 2x, 3x, 4x scaling
- **Real-ESRGAN** - Photorealistic content upscaling
  - Multiple model options (general, anime, v3)
  - FP16 mode for 50% speed boost
  - Tile mode for low VRAM systems
  - 720p → 1440p/4K upscaling

### Non-AI Upscaling
- **Ultra-Fast Alternatives** - Near real-time processing (100x faster than AI)
- **xBR** - Pattern-recognition based, great for anime and sharp edges
- **Lanczos** - Traditional resampling, smooth results for general content
- **HQx** - High-quality magnification for pixel art and sprites
- **Scale Factors** - 2x, 3x, or 4x upscaling

### Frame Interpolation
- **RIFE AI** - Advanced frame interpolation (24fps → 60fps+)
- **SVP Integration** - TensorRT-accelerated RIFE via SVP 4 Pro
- **Multiple Models** - 4.6 to 4.26, lite variants, UHD, and anime-specific models
- **Practical-RIFE** - Standalone Python implementation support

### Dependency Management
- **DependencyChecker Service** - Automated detection and validation of all external dependencies
- **Dependency Manager UI** - Built-in page for checking and installing dependencies
- **Auto-Detection** - Automatic detection of installed tools (FFmpeg, melt, VapourSynth)
- **SVP Python Detection** - Automatically detects and uses SVP's bundled Python installation
- **Installation Wizard** - Guided installation for RIFE, Real-ESRGAN, Real-CUGAN
- **Strategy Selection** - Chocolatey, portable, installer, or manual installation options
- **Real-time Validation** - Instant dependency status checking with detailed error messages

## Usage

### Getting Started: Check Dependencies
1. **Navigate to Dependency Manager** - Check the Dependency Manager page on first launch
2. **Review Status** - See which dependencies are installed and which are missing
3. **Install Missing** - Use the automated installer or follow manual instructions
4. **Verify** - Click "Refresh Status" to confirm all required dependencies are installed

### Playlist Randomization
1. **Load Project** - Select your `.mlt` Shotcut project file
2. **Shuffle** - Click shuffle button next to any playlist, or
3. **Generate Compilation**:
   - Check playlists to include
   - Adjust weights (optional):
     - Duration Weight: 0-20 (higher = prefer shorter clips, 4 = recommended)
     - Number of Videos Weight: 0-5 (higher = more clips, 0.8 = recommended)
   - Set target duration per playlist with slider (0 = use all)
   - Click "Generate Random Playlist"

Output files: `OriginalName.Random[####].mlt`

### Video Rendering & Processing
1. **Open Render Queue** - Navigate to Render Queue page
2. **Add Job** - Click "Add Render Job"
3. **Select Source** - Choose MLT project or video file
4. **Configure Processing**:
   - Enable RIFE interpolation (optional)
   - Choose AI upscaling (Real-CUGAN/Real-ESRGAN) or Non-AI upscaling (xBR/Lanczos/HQx)
   - Select scale factor and quality settings
5. **Add to Queue** - Job processes automatically in background
6. **Monitor Progress** - Real-time progress tracking with stage indicators

## Algorithm

Uses simulated annealing optimization to select the best combination of clips based on:
- Target duration constraints
- Duration weight preferences
- Number of videos weight preferences

## Requirements

### Minimum
- .NET 10.0
- Windows 10/11
- NVIDIA GTX 1060 (6GB) or equivalent
- 8GB RAM
- 4GB VRAM (with tiling)

### Recommended
- NVIDIA RTX 3060+ (8GB+)
- 16GB RAM
- 8GB+ VRAM
- 6+ core CPU

### Optional Dependencies
- FFmpeg (video encoding)
- Melt (Shotcut rendering)
- VapourSynth (RIFE/AI upscaling)
- Python 3.8-3.11 (AI upscaling)
- SVP 4 Pro (TensorRT RIFE)

## Tech Stack

- Blazor Server + Avalonia (CheapAvaloniaBlazor)
- MudBlazor UI components
- Entity Framework Core + SQLite (job persistence)
- VapourSynth (video processing framework)
- FFmpeg (video encoding/decoding)
- CheapHelpers.Services (XML serialization)

## Documentation

**Complete documentation is available at [docs/README.md](docs/README.md)**

### User Guides
- [Installation Guide](docs/installation.md) - Setup RIFE, Real-ESRGAN, Real-CUGAN, and all dependencies
- [Hardware Guide](docs/hardware.md) - Hardware acceleration, NVENC, performance tuning

### Feature Documentation
- [RIFE Frame Interpolation](docs/features/rife.md) - AI-powered frame interpolation
- [Real-CUGAN AI Upscaling](docs/features/real-cugan.md) - Anime/cartoon optimized upscaling
- [Real-ESRGAN AI Upscaling](docs/features/real-esrgan.md) - Photorealistic content upscaling
- [Non-AI Upscaling](docs/features/non-ai-upscaling.md) - Ultra-fast alternatives
- [VapourSynth Setup](docs/features/vapoursynth.md) - Video processing framework

### Developer Documentation
- [Development Documentation](docs/development.md) - Architecture, implementation details, system design
- [Graceful Shutdown Architecture](docs/architecture/graceful-shutdown.md) - Task cancellation patterns
- [Test Documentation](CheapShotcutRandomizer.Tests/README.md) - Unit testing architecture

### Performance Benchmarks
**RTX 3080 (10GB VRAM) Processing Times:**
- RIFE 30→60fps (1080p): ~120 fps
- Real-CUGAN 720p→1440p: 10-20 fps (3-6 min per min of video)
- Real-ESRGAN 720p→1440p: 0.5-1 fps (30+ min per min of video)
- Non-AI xBR/Lanczos: Near real-time (6-18 sec per min of video)

**Complete Pipeline (1-minute video on RTX 3080):**
- MLT render only: ~30s
- MLT + RIFE: ~11min
- MLT + Real-CUGAN: ~14min
- MLT + RIFE + Real-CUGAN: ~25min

## Troubleshooting

### First Step: Check Dependency Manager
1. Navigate to **Dependency Manager** in the application
2. Click **"Refresh Status"** to re-check all dependencies
3. Review status messages for each dependency
4. Use **"Install Missing"** to fix missing dependencies automatically

### Common Issues
- **"vspipe not found"** - Check Dependency Manager for VapourSynth status, install if missing
- **"No source plugin found"** - Check Dependency Manager for VapourSynth Source Plugin status
- **"CUDA out of memory"** - Enable tile mode, reduce tile size, or enable FP16
- **"Python not found"** - Check Dependency Manager - the app can detect SVP's Python automatically
- **Slow processing** - Enable FP16 mode, increase tile size, use TensorRT backend

See [Installation Guide](docs/installation.md) for detailed troubleshooting.


<img width="1508" height="1247" alt="Screenshot 2025-10-22 024423" src="https://github.com/user-attachments/assets/56b6ce72-ca23-4109-ad6f-05685f09a05f" />

---
