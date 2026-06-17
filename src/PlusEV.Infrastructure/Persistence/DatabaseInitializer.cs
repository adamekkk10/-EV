using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PlusEV.Infrastructure.Persistence;

/// <summary>
/// Convenience helper that runs <c>EnsureCreated</c>/<c>Migrate</c> at startup.
/// Using EnsureCreated here because migrations are scaffolded separately; flip the
/// flag to <see cref="UseMigrations"/> once the project commits to migration history.
/// </summary>
public static class DatabaseInitializer
{
    public const bool UseMigrations = false;

    public static async Task EnsureReadyAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<PlusEvDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        if (UseMigrations)
        {
            logger.LogInformation("Applying EF Core migrations...");
            await ctx.Database.MigrateAsync(ct);
        }
        else
        {
            await ctx.Database.EnsureCreatedAsync(ct);
        }
        logger.LogInformation("Database ready at {Path}", ctx.Database.GetDbConnection().DataSource);
    }
}
