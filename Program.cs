using CheapAvaloniaBlazor.Hosting;
using CheapAvaloniaBlazor.Extensions;
using CheapShotcutRandomizer.Services;
using CheapHelpers.Services.DataExchange.Xml;
using Microsoft.Extensions.DependencyInjection;

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

        // Run the app - all Avalonia complexity handled by the package
        builder.RunApp(args);
    }
}
