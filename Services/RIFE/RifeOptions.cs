namespace CheapShotcutRandomizer.Services.RIFE;

/// <summary>
/// Neural network engine for RIFE processing
/// </summary>
public enum RifeEngine
{
    TensorRT,
    Vulkan,
    NCNN
}

/// <summary>
/// Scene change detection method
/// </summary>
public enum SceneChangeDetection
{
    /// <summary>
    /// Use SVP motion vectors for scene change detection
    /// </summary>
    SvpMotionVectors,

    /// <summary>
    /// Disable scene change detection
    /// </summary>
    Disabled,

    /// <summary>
    /// Use threshold-based detection
    /// </summary>
    ThresholdBased
}

/// <summary>
/// How to process scene changes
/// </summary>
public enum SceneChangeProcessing
{
    /// <summary>
    /// Repeat the last frame
    /// </summary>
    RepeatFrame,

    /// <summary>
    /// Blend between frames
    /// </summary>
    BlendFrames,

    /// <summary>
    /// Interpolate normally (may cause artifacts)
    /// </summary>
    InterpolateNormally
}

/// <summary>
/// Duplicate frames removal strategy
/// </summary>
public enum DuplicateFramesRemoval
{
    /// <summary>
    /// Do not remove duplicates
    /// </summary>
    DoNotRemove,

    /// <summary>
    /// Remove duplicate frames
    /// </summary>
    RemoveDuplicates,

    /// <summary>
    /// Smart detection of duplicates
    /// </summary>
    SmartDetection
}

/// <summary>
/// Configuration options for RIFE interpolation
/// Supports both standalone RIFE executables and SVP's integrated RIFE (VapourSynth-based)
/// </summary>
public class RifeOptions
{
    // === Core Engine Settings ===

    /// <summary>
    /// Neural network engine to use
    /// TensorRT: Best for NVIDIA RTX GPUs (2000, 3000, 4000, 5000 series)
    /// Vulkan: Universal compatibility, works on most GPUs
    /// NCNN: Mobile-optimized, fallback option
    /// </summary>
    public RifeEngine Engine { get; set; } = RifeEngine.TensorRT;

    /// <summary>
    /// Number of parallel GPU streams for processing
    /// Range: 1-8, Default: 2
    /// Higher values may improve throughput on powerful GPUs
    /// </summary>
    public int GpuThreads { get; set; } = 2;

    /// <summary>
    /// AI model version to use
    /// Available models: 4.6, 4.14, 4.15, 4.16-lite, 4.17, 4.18, 4.20, 4.21, 4.22, 4.22-lite, 4.25, 4.25-lite, 4.26, UHD, anime
    /// Recommended: 4.6 for balanced use (SVP default), 4.18 for best quality, 4.25 for newest algorithm
    /// Use 4.15-lite or 4.22-lite for faster processing on lower-end GPUs
    /// </summary>
    public string ModelName { get; set; } = "4.6";

    /// <summary>
    /// GPU device ID to use for processing
    /// 0 = First GPU, 1 = Second GPU, etc.
    /// -1 = CPU (very slow, not recommended)
    /// </summary>
    public int GpuId { get; set; } = 0;

    /// <summary>
    /// List of available GPU device names (for UI display)
    /// </summary>
    public List<string> AvailableGpus { get; set; } = new();

    // === Performance Settings ===

    /// <summary>
    /// Test-Time Augmentation mode
    /// Improves quality but significantly slower (4x processing time)
    /// Disabled = Standard processing
    /// Enabled = Better quality, much slower
    /// </summary>
    public bool TtaMode { get; set; } = false;

    // === Scene Change Detection ===

    /// <summary>
    /// Scene change detection method
    /// </summary>
    public SceneChangeDetection SceneDetection { get; set; } = SceneChangeDetection.SvpMotionVectors;

    /// <summary>
    /// How to process detected scene changes
    /// </summary>
    public SceneChangeProcessing SceneProcessing { get; set; } = SceneChangeProcessing.BlendFrames;

    /// <summary>
    /// Duplicate frames removal strategy
    /// </summary>
    public DuplicateFramesRemoval DuplicateRemoval { get; set; } = DuplicateFramesRemoval.SmartDetection;

    // === Resolution Settings ===

    /// <summary>
    /// Target frame height for processing (optional)
    /// 0 = Auto (use source resolution)
    /// Common values: 576, 720, 1080, 1440, 2160
    /// </summary>
    public int FrameHeight { get; set; } = 0;

    /// <summary>
    /// Enable UHD mode for ultra high definition videos
    /// Only use for 4K+ content
    /// </summary>
    public bool UhdMode { get; set; } = false;

    /// <summary>
    /// Alias for UhdMode (for backward compatibility)
    /// </summary>
    public bool UhMode
    {
        get => UhdMode;
        set => UhdMode = value;
    }

    // === Legacy Settings (for backward compatibility) ===

    /// <summary>
    /// Thread configuration: load:proc:save (for rife-ncnn-vulkan)
    /// Default: "2:2:2" for balanced performance
    /// </summary>
    public string ThreadConfig { get; set; } = "2:2:2";

    /// <summary>
    /// Tile size for processing (for rife-ncnn-vulkan)
    /// 0 = auto (recommended)
    /// Manual values like 256, 512 for lower VRAM usage
    /// </summary>
    public int TileSize { get; set; } = 0;

    /// <summary>
    /// Number of times to interpolate (creates 2^n frames)
    /// 1 = 2x frames (30fps → 60fps)
    /// 2 = 4x frames (30fps → 120fps)
    /// 3 = 8x frames (30fps → 240fps)
    /// </summary>
    public int InterpolationPasses { get; set; } = 1;

    /// <summary>
    /// Build command-line arguments for rife-ncnn-vulkan
    /// </summary>
    public string BuildArguments(string inputFolder, string outputFolder)
    {
        var args = new List<string>
        {
            $"-i \"{inputFolder}\"",
            $"-o \"{outputFolder}\"",
            $"-m {ModelName}",
            $"-g {GpuId}",
            $"-j {ThreadConfig}",
            $"-n {InterpolationPasses}"
        };

        if (UhMode)
            args.Add("-u");

        if (TtaMode)
            args.Add("-x");

        if (TileSize > 0)
            args.Add($"-t {TileSize}");

        return string.Join(" ", args);
    }

    /// <summary>
    /// Calculate output frame multiplier based on passes
    /// </summary>
    public int GetFrameMultiplier() => (int)Math.Pow(2, InterpolationPasses);

    // Additional properties for Python-based RIFE (inference_video.py)

    /// <summary>
    /// Direct interpolation multiplier (2, 4, 8, etc.)
    /// Used by Python RIFE instead of passes
    /// </summary>
    public int InterpolationMultiplier
    {
        get => GetFrameMultiplier();
        set => InterpolationPasses = (int)Math.Log2(value);
    }

    /// <summary>
    /// Model number for SVP RIFE integration (matches SVP's helpers.py algorithm)
    /// Examples: 4.6 -> 46, 4.15 -> 415, 4.22-lite -> 4221
    /// </summary>
    public int ModelNumber
    {
        get
        {
            // Match SVP's helpers.py algorithm exactly
            // Split by '.' or '_' to extract version parts
            var cleanName = ModelName.Replace("rife-v", "").Replace("rife-", "");
            var parts = System.Text.RegularExpressions.Regex.Split(cleanName, @"[\._]");

            if (parts.Length >= 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
            {
                // Calculate base: major * (10 if minor is single digit else 100) + minor
                var baseNum = major * (minor < 10 ? 10 : 100) + minor;

                // Check for lite/heavy variants (multiply by 10 and add suffix)
                var lowerName = cleanName.ToLower();
                if (lowerName.Contains("lite"))
                    return baseNum * 10 + 1;
                if (lowerName.Contains("heavy"))
                    return baseNum * 10 + 2;

                return baseNum;
            }

            // Special cases for named models
            var lowerModel = ModelName.ToLower();
            if (lowerModel.Contains("uhd"))
                return 46; // UHD models use 4.6 base
            if (lowerModel.Contains("anime"))
                return 46; // Anime models use 4.6 base

            return 46; // Default to 4.6
        }
        set
        {
            // Convert number back to model name
            // Handle lite variants (ending in 1)
            if (value % 10 == 1 && value > 100)
            {
                var baseNum = value / 10;
                var major = baseNum / 100;
                var minor = baseNum % 100;
                ModelName = minor < 10 ? $"{major}.{minor}-lite" : $"{major}.{minor}-lite";
            }
            // Handle heavy variants (ending in 2)
            else if (value % 10 == 2 && value > 100)
            {
                var baseNum = value / 10;
                var major = baseNum / 100;
                var minor = baseNum % 100;
                ModelName = minor < 10 ? $"{major}.{minor}-heavy" : $"{major}.{minor}-heavy";
            }
            // Standard versions
            else if (value >= 100)
            {
                var major = value / 100;
                var minor = value % 100;
                ModelName = $"{major}.{minor}";
            }
            else
            {
                var major = value / 10;
                var minor = value % 10;
                ModelName = $"{major}.{minor}";
            }
        }
    }

    /// <summary>
    /// Target output frame rate (for Python RIFE)
    /// </summary>
    public int TargetFps { get; set; } = 60;

    /// <summary>
    /// Resolution scale factor (for Python RIFE)
    /// </summary>
    public double Scale { get; set; } = 1.0;

    // === Helper Methods ===

    /// <summary>
    /// Get all available RIFE models (matches official Practical-RIFE and SVP releases)
    /// </summary>
    public static string[] GetAvailableModels()
    {
        return new[]
        {
            // Standard models (balanced quality/speed)
            "4.6",      // Default, balanced (SVP default)
            "4.14",     // Earlier stable version
            "4.15",     // Good for gaming/high FPS
            "4.17",     // Mid-generation model
            "4.18",     // Recommended by developers for best quality
            "4.20",     // Later stable version
            "4.21",     // Pre-4.22 version
            "4.22",     // Stable recent version
            "4.25",     // Newest algorithm, least visual anomalies (Practical-RIFE default)
            "4.26",     // Latest version

            // Lite models (faster, lower VRAM)
            "4.14-lite", // Lite variant of 4.14
            "4.15-lite", // Fast, good for lower-end GPUs
            "4.16-lite", // Fastest processing
            "4.17-lite", // Lite variant of 4.17
            "4.22-lite", // Good balance of speed/quality
            "4.25-lite", // Latest lite variant

            // Special purpose models
            "UHD",      // Optimized for 4K+ content
            "anime"     // Optimized for animation
        };
    }

    /// <summary>
    /// Get available frame height options
    /// </summary>
    public static int[] GetAvailableFrameHeights()
    {
        return new[] { 0, 576, 720, 1080, 1440, 2160 };
    }

    /// <summary>
    /// Get display name for frame height
    /// </summary>
    public static string GetFrameHeightDisplayName(int height)
    {
        return height switch
        {
            0 => "Auto",
            576 => "576p (SD)",
            720 => "720p (HD)",
            1080 => "1080p (FHD)",
            1440 => "1440p (QHD)",
            2160 => "2160p (4K UHD)",
            _ => $"{height}p"
        };
    }
}
