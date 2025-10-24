# RIFE Installation Guide

## Overview
RIFE (Real-Time Intermediate Flow Estimation) provides AI-powered frame interpolation to increase video frame rates. This application supports two RIFE implementations:

1. **SVP's RIFE** (Recommended for NVIDIA users) - TensorRT accelerated
2. **Practical-RIFE** (Standalone Python version) - More flexible, supports more GPUs

## Option 1: SVP's RIFE (Recommended)

### Requirements
- NVIDIA GPU (RTX 20 series or newer recommended)
- SVP 4 Pro (includes RIFE with TensorRT)
- VapourSynth (for integration with this app)

### Installation Steps

1. **Install SVP 4 Pro**
   - Download from: https://www.svp-team.com/get/
   - During installation, select "RIFE AI engine" component
   - The RIFE files will be installed to: `C:\Program Files (x86)\SVP 4\rife\`

2. **Install VapourSynth** (Required for this app to use SVP's RIFE)
   - Download from: https://github.com/vapoursynth/vapoursynth/releases
   - Run the installer (adds `vspipe` to PATH automatically)
   - Restart your computer or refresh PATH

3. **Install VapourSynth Source Plugin** (Required for video loading)
   VapourSynth needs a source plugin to load video files. Install **one** of these:

   **Option A: BestSource** (Recommended - works with most formats)
   - Download from: https://github.com/vapoursynth/bestsource/releases
   - Extract `BestSource.dll` to:
     - `C:\Program Files\VapourSynth\plugins\`
     - OR `%APPDATA%\VapourSynth\plugins\`

   **Option B: L-SMASH Source** (Alternative)
   - Download from: https://github.com/AkarinVS/L-SMASH-Works/releases
   - Extract `LSMASHSource.dll` to `C:\Program Files\VapourSynth\plugins\`

   **Option C: FFMS2** (Another alternative)
   - Download from: https://github.com/FFMS/ffms2/releases
   - Extract `FFMS2.dll` to `C:\Program Files\VapourSynth\plugins\`

4. **Verify Installation**
   ```cmd
   vspipe --version
   ```

   Test if source plugin works:
   ```cmd
   vspipe --info test_script.vpy
   ```
   Where test_script.vpy contains:
   ```python
   import vapoursynth as vs
   core = vs.core
   clip = core.bs.VideoSource(source=r'C:\path\to\video.mp4')
   clip.set_output()
   ```

### SVP RIFE Models Available
- 4.6 (Default, balanced)
- 4.14, 4.15, 4.16-lite
- 4.17, 4.18, 4.20, 4.21
- 4.22, 4.22-lite (Latest)
- 4.25, 4.25-lite, 4.26
- UHD (for 4K+ content)
- Anime (optimized for animation)

## Option 2: Practical-RIFE (Standalone)

### Requirements
- Python 3.8-3.11 (3.12+ not yet supported)
- NVIDIA GPU with CUDA support (or AMD with ROCm)
- 4GB+ VRAM recommended

### Installation Steps

1. **Install Python**
   - Download Python 3.11 from: https://www.python.org/downloads/
   - During installation, check "Add Python to PATH"

2. **Clone Practical-RIFE Repository**
   ```cmd
   cd C:\
   git clone https://github.com/hzwer/Practical-RIFE.git
   cd Practical-RIFE
   ```

3. **Install Dependencies**

   For NVIDIA GPUs:
   ```cmd
   pip install torch torchvision --index-url https://download.pytorch.org/whl/cu118
   pip install opencv-python numpy
   ```

   For CPU only (very slow):
   ```cmd
   pip install torch torchvision
   pip install opencv-python numpy
   ```

4. **Download Model Files**
   - Models are downloaded automatically on first run
   - Or manually download from: https://github.com/hzwer/Practical-RIFE/releases
   - Place in `train_log/` folder

5. **Test Installation**
   ```cmd
   python inference_video.py --video input.mp4 --multi 2
   ```

### Common Installation Locations
The app will automatically search for Practical-RIFE in:
- `C:\Practical-RIFE`
- `C:\RIFE`
- `%USERPROFILE%\Practical-RIFE`
- `%USERPROFILE%\Documents\Practical-RIFE`
- `D:\Practical-RIFE`

## Usage in Application

### Configure Path in Settings
1. Open Settings page
2. Navigate to "RIFE Folder Path"
3. Click "Browse" and select:
   - For SVP: `C:\Program Files (x86)\SVP 4\rife`
   - For Practical-RIFE: The cloned repository folder

### Command-Line Usage (Reference)

**Practical-RIFE:**
```bash
python inference_video.py --video input.mp4 --output output.mp4 --multi 2 --model 4.22
```

**Parameters:**
- `--multi`: Frame multiplication (2, 4, 8)
- `--model`: Model version (4.6, 4.22, etc.)
- `--uhd`: Enable UHD mode for 4K+ videos
- `--scale`: Resolution scaling (0.5, 2.0, etc.)
- `--gpu`: GPU device ID (0, 1, etc.)

## Troubleshooting

### VapourSynth Source Plugin Error
**Error:** "No attribute with the name ffms2/lsmas/avisource exists"

**Solution:**
- Install a VapourSynth source plugin (see step 3 above)
- BestSource is recommended: https://github.com/vapoursynth/bestsource/releases
- Place the DLL in: `C:\Program Files\VapourSynth\plugins\`
- Restart the application after installing
- If BestSource.dll is already there, rebuild the application - it will now explicitly load it

### VapourSynth Not Found
- Ensure VapourSynth is installed
- Restart computer after installation
- Check if `vspipe` is in PATH: `where vspipe`

### Python/RIFE Not Found
- Verify Python version: `python --version`
- Should be 3.8-3.11
- Check RIFE folder contains `inference_video.py`

### CUDA/GPU Errors
- Update NVIDIA drivers
- Install CUDA Toolkit matching PyTorch version
- For RTX 30/40 series, ensure CUDA 11.8+

### Out of Memory Errors
- Reduce tile size in settings
- Close other GPU applications
- Use lite models (4.16-lite, 4.22-lite)

## Performance Tips

### For Best Speed (SVP RIFE):
- Use TensorRT acceleration (automatic with SVP)
- GPU threads: 2-4 (based on GPU)
- Model: 4.6 or 4.15-lite

### For Best Quality:
- Model: 4.22 or 4.25
- Enable TTA mode (slower but better)
- Use higher CRF values for output

### For 4K/UHD Content:
- Enable UHD mode
- Use UHD model if available
- Increase tile size if VRAM allows

## Model Recommendations

| Content Type | Recommended Model | Notes |
|-------------|------------------|-------|
| General | 4.6 or 4.22 | Balanced speed/quality |
| Fast Preview | 4.16-lite | Fastest processing |
| High Quality | 4.25 | Best quality, slower |
| 4K/UHD | UHD model | Optimized for high res |
| Anime | Anime model | Better for animation |
| Gaming | 4.15 | Good for 60→120fps |

## Links
- SVP 4 Pro: https://www.svp-team.com
- Practical-RIFE: https://github.com/hzwer/Practical-RIFE
- VapourSynth: https://github.com/vapoursynth/vapoursynth
- PyTorch: https://pytorch.org