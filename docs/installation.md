# Installation Guide

Complete setup instructions for all AI processing features.

---

## Table of Contents
- [RIFE Frame Interpolation](#rife-frame-interpolation)
- [Real-ESRGAN AI Upscaling](#real-esrgan-ai-upscaling)
- [VapourSynth Setup](#vapoursynth-setup)
- [Troubleshooting](#troubleshooting)

---

## RIFE Frame Interpolation

AI-powered frame interpolation to increase video frame rates (30fps → 60fps, etc.).

### Recommended: SVP's RIFE (TensorRT Accelerated)

**Requirements:**
- NVIDIA GPU (RTX 20 series or newer)
- SVP 4 Pro
- VapourSynth

**Installation:**

1. **Install SVP 4 Pro**
   - Download: https://www.svp-team.com/get/
   - Select "RIFE AI engine" during installation
   - Installs to: `C:\Program Files (x86)\SVP 4\rife\`

2. **Install VapourSynth** (see [VapourSynth Setup](#vapoursynth-setup) below)

3. **Configure in App**
   - Settings → RIFE Folder Path
   - Browse to: `C:\Program Files (x86)\SVP 4\rife`

**Available Models:**
- 4.6 (balanced, default)
- 4.14-4.26 (various improvements)
- 4.22-lite, 4.25-lite (faster)
- UHD (for 4K+)
- Anime (optimized for animation)

### Alternative: Practical-RIFE (Standalone Python)

**Requirements:**
- Python 3.8-3.11
- NVIDIA GPU with CUDA
- 4GB+ VRAM

**Installation:**
```cmd
# Install Python 3.11 (check "Add to PATH")
# Then:
cd C:\
git clone https://github.com/hzwer/Practical-RIFE.git
cd Practical-RIFE

pip install torch torchvision --index-url https://download.pytorch.org/whl/cu118
pip install opencv-python numpy
```

**Test:**
```cmd
python inference_video.py --video input.mp4 --multi 2
```

---

## Real-ESRGAN AI Upscaling

AI-powered video upscaling (720p → 1440p/4K).

### Requirements
- **VapourSynth**
- **Python 3.8-3.11**
- **NVIDIA GPU** (RTX 20 series or newer)
- **6GB+ VRAM** (4GB minimum with tiling)

### Installation Steps

1. **Install VapourSynth** (see [VapourSynth Setup](#vapoursynth-setup) below)

2. **Install Python 3.11**
   - Download: https://www.python.org/downloads/
   - Check "Add Python to PATH" during installation

3. **Install vsrealesrgan**
   ```cmd
   pip install torch torchvision --index-url https://download.pytorch.org/whl/cu126
   pip install vsrealesrgan
   ```

4. **Install TensorRT (Optional, 2-3x faster)**
   ```cmd
   pip install torch_tensorrt tensorrt-cu12
   ```

5. **Verify**
   ```cmd
   python -c "import vsrealesrgan; print('vsrealesrgan OK')"
   ```

### Available Models

| Model | Scale | Best For | Speed |
|-------|-------|----------|-------|
| RealESRGAN_x4plus | 4x | General content (default) | Fast |
| RealESRGAN_x4plus_anime_6B | 4x | Anime/animation | Medium |
| RealESRGAN_x2plus | 2x | Moderate upscaling | Fastest |
| realesr-general-x4v3 | 4x | General + denoising | Medium |
| RealESRGAN_AnimeVideo-v3 | 4x | Anime with temporal consistency | Slow |

Models auto-download on first use (stored in `~/.cache/torch/hub/`).

### Performance Settings

**Tile Mode:**
- 256px: Low VRAM (2-4GB), slowest
- 512px: Moderate VRAM (6-8GB), balanced ✅
- 768px: High VRAM (10-12GB), fastest

**FP16 Mode (Recommended):**
- 50% faster, half VRAM usage
- Minimal quality loss
- All RTX GPUs support it

---

## VapourSynth Setup

Required for both RIFE and Real-ESRGAN processing.

### Install VapourSynth

1. **Download & Install**
   - Download: https://github.com/vapoursynth/vapoursynth/releases
   - Run installer (adds `vspipe` to PATH)
   - Restart computer

2. **Verify**
   ```cmd
   vspipe --version
   ```

### Install Source Plugin

VapourSynth needs a plugin to load videos. Choose one:

#### BestSource (Recommended)
- Download: https://github.com/vapoursynth/bestsource/releases
- Extract `BestSource.dll` to:
  - `C:\Program Files\VapourSynth\plugins\`
  - OR `%APPDATA%\VapourSynth\plugins\`

#### L-SMASH Source (Alternative)
- Download: https://github.com/AkarinVS/L-SMASH-Works/releases
- Extract `LSMASHSource.dll` to `C:\Program Files\VapourSynth\plugins\`

#### FFMS2 (Another Alternative)
- Download: https://github.com/FFMS/ffms2/releases
- Extract `FFMS2.dll` to `C:\Program Files\VapourSynth\plugins\`

---

## Troubleshooting

### VapourSynth Errors

**"No attribute with the name bs/ffms2/lsmas exists"**
- Install a source plugin (BestSource recommended)
- Restart application after installing
- Check plugin is in correct folder

**"vspipe not found"**
- Restart computer after VapourSynth installation
- Verify: `where vspipe` shows correct path

### CUDA/GPU Errors

**"CUDA out of memory"**
- Enable Tile Mode in settings
- Reduce Tile Size (512→384→256)
- Enable FP16 mode
- Close other GPU applications

**"TensorRT initialization timeout"**
- First run takes 5-15 minutes (CUDA kernel compilation)
- Check Task Manager for GPU activity
- Update NVIDIA drivers

### Python Errors

**"vsrealesrgan not installed"**
```cmd
pip install --upgrade pip
pip install vsrealesrgan --force-reinstall
```

**"Module not found"**
- Verify Python version: `python --version` (should be 3.8-3.11)
- Reinstall with correct CUDA version:
  ```cmd
  pip install torch torchvision --index-url https://download.pytorch.org/whl/cu126
  ```

### Performance Issues

**Slow Processing:**
1. Enable FP16 mode (biggest improvement)
2. Increase Tile Size if VRAM allows
3. Disable denoising if not needed
4. Use lite models (4.22-lite, etc.)
5. Update NVIDIA drivers

---

## Hardware Requirements

### Minimum
- **GPU:** NVIDIA GTX 1060 (6GB)
- **CPU:** Quad-core processor
- **RAM:** 8GB
- **VRAM:** 4GB (with tiling)

### Recommended
- **GPU:** NVIDIA RTX 3060+ (8GB+)
- **CPU:** 6+ core processor
- **RAM:** 16GB
- **VRAM:** 8GB+

### Optimal
- **GPU:** NVIDIA RTX 4070+ (12GB+)
- **CPU:** 8+ core processor (Ryzen 9, i7/i9)
- **RAM:** 32GB
- **VRAM:** 12GB+

---

## Performance Benchmarks

### RTX 3080 (10GB VRAM)

| Operation | Speed | Notes |
|-----------|-------|-------|
| RIFE 30→60fps (1080p) | 120 fps | TensorRT accelerated |
| ESRGAN 720p→1440p | 6 fps | FP16, tile 512px |
| ESRGAN 1080p→4K | 3 fps | FP16, tile 384px |

### Processing Time (1-minute video on RTX 3080)

| Pipeline | Time | Stages |
|----------|------|--------|
| MLT render only | 30s | MLT |
| MLT + RIFE | ~11min | MLT → RIFE |
| MLT + ESRGAN | ~11min | MLT → ESRGAN |
| MLT + RIFE + ESRGAN | ~21min | MLT → RIFE → ESRGAN |

---

## Links

- **RIFE:** https://github.com/hzwer/Practical-RIFE
- **SVP:** https://www.svp-team.com
- **Real-ESRGAN:** https://github.com/xinntao/Real-ESRGAN
- **vsrealesrgan:** https://github.com/HolyWu/vs-realesrgan
- **VapourSynth:** https://github.com/vapoursynth/vapoursynth
- **PyTorch:** https://pytorch.org
- **TensorRT:** https://developer.nvidia.com/tensorrt
