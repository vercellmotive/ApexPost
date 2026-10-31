using ApexPost.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace ApexPost.Web.Configuration;

// Development convenience only: enabled by Database:SeedOnStartup, and it never creates or migrates the schema.
public sealed class DatabaseSeedingHostedService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<DatabaseSeedingHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Database:SeedOnStartup"))
        {
            return;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var seeded = await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync(cancellationToken);
            logger.LogInformation("Database seeding {Outcome}.", seeded ? "completed" : "skipped because data already exists");
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            logger.LogError(exception, "Database seeding failed. Set the connection string and run dotnet ef database update; see README.md.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
