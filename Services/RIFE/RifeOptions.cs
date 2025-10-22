namespace CheapShotcutRandomizer.Services.RIFE;

/// <summary>
/// Configuration options for RIFE interpolation
/// </summary>
public class RifeOptions
{
    /// <summary>
    /// RIFE model to use
    /// Options: "rife", "rife-HD", "rife-UHD", "rife-anime", "rife-v2", "rife-v2.3", "rife-v2.4", "rife-v3.0", "rife-v3.1", "rife-v4", "rife-v4.6", "rife-v4.15-lite", "rife-v4.16-lite", "rife-v4.17", "rife-v4.18", "rife-v4.20", "rife-v4.21", "rife-v4.22", "rife-v4.25"
    /// Recommended: "rife-v4.6" for general use
    /// </summary>
    public string ModelName { get; set; } = "rife-v4.6";

    /// <summary>
    /// GPU ID to use for processing
    /// -1 = CPU (very slow)
    /// 0 = First GPU (RTX 3080)
    /// 1+ = Additional GPUs
    /// </summary>
    public int GpuId { get; set; } = 0;

    /// <summary>
    /// Thread configuration: load:proc:save
    /// Default: "2:2:2" for balanced performance
    /// Example: "4:4:4" for more threads (may not be faster)
    /// </summary>
    public string ThreadConfig { get; set; } = "2:2:2";

    /// <summary>
    /// Enable UHD mode for ultra high definition videos
    /// Only use for 4K+ content
    /// </summary>
    public bool UhMode { get; set; } = false;

    /// <summary>
    /// Test-time augmentation for better quality
    /// Significantly slower (4x processing time)
    /// </summary>
    public bool TtaMode { get; set; } = false;

    /// <summary>
    /// Tile size for processing
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
}
