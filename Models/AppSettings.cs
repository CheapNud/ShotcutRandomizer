namespace CheapShotcutRandomizer.Models;

/// <summary>
/// Application settings that persist across sessions
/// </summary>
public class AppSettings
{
    // Logging Settings
    public bool VerboseLogging { get; set; } = false;

    // SVP Integration
    public bool UseSvpEncoders { get; set; } = true; // Default to true - SVP has better FFmpeg builds

    // Path Settings
    public string FFmpegPath { get; set; } = "ffmpeg";
    public string FFprobePath { get; set; } = "ffprobe"; // Separate path for ffprobe (usually same directory as ffmpeg)
    public string MeltPath { get; set; } = "melt";
    public string RifePath { get; set; } = "rife-ncnn-vulkan.exe";
    public string RifeVariant { get; set; } = "Vulkan"; // "Vulkan" or "TensorRT"

    // Helper property to get the correct RIFE executable name based on variant
    public string GetRifeExecutableName()
    {
        return RifeVariant == "TensorRT" ? "rife-tensorrt.exe" : "rife-ncnn-vulkan.exe";
    }

    // Render Default Settings
    public string DefaultQuality { get; set; } = "High";
    public string DefaultCodec { get; set; } = "libx264";
    public int DefaultCrf { get; set; } = 23;
    public string DefaultPreset { get; set; } = "medium";

    // RIFE Default Settings
    public int DefaultRifeModel { get; set; } = 46; // rife-v4.6
    public int DefaultRifeThreads { get; set; } = 2;
    public bool DefaultRifeUhdMode { get; set; } = false;
    public bool DefaultRifeTtaMode { get; set; } = false;

    // Application Behavior
    public int MaxConcurrentRenders { get; set; } = 1;
    public bool AutoStartQueue { get; set; } = false;
    public bool ShowNotificationsOnComplete { get; set; } = true;
}
