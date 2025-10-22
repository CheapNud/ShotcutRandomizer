using CheapAvaloniaBlazor.Hosting;
using CheapAvaloniaBlazor.Extensions;
using CheapShotcutRandomizer.Services;
using CheapShotcutRandomizer.Services.Queue;
using CheapShotcutRandomizer.Data;
using CheapShotcutRandomizer.Data.Repositories;
using CheapHelpers.Services.DataExchange.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

namespace CheapShotcutRandomizer;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = new CheapAvaloniaBlazor.Hosting.HostBuilder()
            .WithTitle("Cheap Shotcut Randomizer")
            .WithSize(1000, 800)
            .AddMudBlazor();

        // Register services
        builder.Services.AddScoped<IXmlService, XmlService>();
        builder.Services.AddScoped<ShotcutService>();
        builder.Services.AddScoped<FileSearchService>();

        // Video rendering services
        builder.Services.AddScoped<FFmpegRenderService>();
        builder.Services.AddScoped<MeltRenderService>();
        builder.Services.AddSingleton<HardwareDetectionService>();

        // RIFE services
        builder.Services.AddScoped<CheapShotcutRandomizer.Services.RIFE.RifeInterpolationService>(sp =>
            new CheapShotcutRandomizer.Services.RIFE.RifeInterpolationService("rife-ncnn-vulkan.exe"));
        builder.Services.AddScoped<CheapShotcutRandomizer.Services.RIFE.RifeVideoProcessingPipeline>();

        // Utility services
        builder.Services.AddScoped<CheapShotcutRandomizer.Services.Utilities.VideoValidator>();
        builder.Services.AddScoped<CheapShotcutRandomizer.Services.Utilities.FFmpegErrorHandler>();

        // Database for render queue
        builder.Services.AddDbContext<RenderJobDbContext>(options =>
            options.UseSqlite("Data Source=renderjobs.db"));

        // Repositories
        builder.Services.AddScoped<IRenderJobRepository, RenderJobRepository>();

        // Queue infrastructure
        builder.Services.AddSingleton<IBackgroundTaskQueue>(_ =>
            new BackgroundTaskQueue(capacity: 100));

        // Render queue service (singleton for background service)
        builder.Services.AddSingleton<RenderQueueService>(serviceProvider =>
            new RenderQueueService(
                serviceProvider,
                serviceProvider.GetRequiredService<IBackgroundTaskQueue>(),
                maxConcurrentRenders: 1 // Configure: 1 for video rendering (CPU/GPU intensive)
            ));

        // Register as both IRenderQueueService and IHostedService
        builder.Services.AddSingleton<IRenderQueueService>(sp =>
            sp.GetRequiredService<RenderQueueService>());
        builder.Services.AddHostedService(sp =>
            sp.GetRequiredService<RenderQueueService>());

        // Database initialization hosted service (runs after app starts)
        builder.Services.AddHostedService<DatabaseInitializationService>();

        // Configure graceful shutdown
        builder.Services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(30);
        });

        // Run the app - all Avalonia complexity handled by the package
        builder.RunApp(args);
    }
}
