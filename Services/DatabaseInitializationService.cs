using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using CheapShotcutRandomizer.Data;

namespace CheapShotcutRandomizer.Services;

/// <summary>
/// Hosted service that initializes the database on startup
/// </summary>
public class DatabaseInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public DatabaseInitializationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RenderJobDbContext>();

        await db.Database.EnsureCreatedAsync(cancellationToken);
        await db.EnableWalModeAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
