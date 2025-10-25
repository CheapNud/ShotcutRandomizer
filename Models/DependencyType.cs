namespace CheapShotcutRandomizer.Models;

/// <summary>
/// Types of external dependencies required by the application
/// </summary>
public enum DependencyType
{
    /// <summary>
    /// FFmpeg - video encoding/decoding tool (required)
    /// Can be sourced from Shotcut or SVP
    /// </summary>
    FFmpeg,

    /// <summary>
    /// FFprobe - video analysis tool (required)
    /// Usually bundled with FFmpeg
    /// </summary>
    FFprobe,

    /// <summary>
    /// Shotcut/MLT Melt - video project renderer (required)
    /// Required for rendering Shotcut projects
    /// </summary>
    Melt,

    /// <summary>
    /// VapourSynth - video processing framework (optional)
    /// Required for SVP RIFE integration
    /// </summary>
    VapourSynth,

    /// <summary>
    /// VapourSynth Source Plugin - video loading for VapourSynth (optional)
    /// One of: BestSource, L-SMASH, or FFMS2
    /// Required for VapourSynth-based RIFE
    /// </summary>
    VapourSynthSourcePlugin,

    /// <summary>
    /// SVP 4 Pro - Smooth Video Project with RIFE TensorRT (optional)
    /// Provides high-quality FFmpeg builds and RIFE integration
    /// </summary>
    SvpRife,

    /// <summary>
    /// Python 3.8-3.11 - Python interpreter (optional)
    /// Required for standalone Practical-RIFE
    /// </summary>
    Python,

    /// <summary>
    /// Practical-RIFE - standalone Python RIFE implementation (optional)
    /// Alternative to SVP's RIFE
    /// </summary>
    PracticalRife,

    /// <summary>
    /// vsrealesrgan - VapourSynth Real-ESRGAN plugin (optional)
    /// Required for AI upscaling with Real-ESRGAN
    /// Python package: pip install vsrealesrgan
    /// </summary>
    VsRealEsrgan,

    /// <summary>
    /// PyTorch with CUDA support (optional)
    /// Required for vsrealesrgan GPU acceleration
    /// Install from: https://pytorch.org/get-started/locally/
    /// </summary>
    PyTorchCuda,

    /// <summary>
    /// vs-mlrt - VapourSynth ML Runtime with TensorRT/CUDA (optional)
    /// Required for Real-CUGAN AI upscaling (10-13x faster than Real-ESRGAN)
    /// Python package: pip install vsmlrt
    /// Requires TensorRT for optimal performance
    /// </summary>
    VsMLRT,

    /// <summary>
    /// TensorRT - NVIDIA's high-performance inference runtime (optional)
    /// Required for vs-mlrt TensorRT backend (fastest Real-CUGAN performance)
    /// Download from: https://developer.nvidia.com/tensorrt
    /// </summary>
    TensorRT
}
