using ApexPost.Application.Interfaces;
using ApexPost.Application.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Infrastructure.Persistence;

public static class SqlServerRegistration
{
    public const string ConnectionStringName = "ApexPostDb";

    public static IServiceCollection AddSqlServerPersistence(this IServiceCollection services)
    {
        services.AddDbContext<ApexPostDbContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{ConnectionStringName}' is not configured. Set it with dotnet user-secrets; see README.md.");
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3));

            // These failures all surface as exceptions, and GlobalExceptionMiddleware logs them at the right level.
            // Without this, an expected "cannot go negative" or a 409 would also log as an EF Core error.
            options.ConfigureWarnings(warnings => warnings.Ignore(
                CoreEventId.QueryIterationFailed,
                CoreEventId.SaveChangesFailed,
                CoreEventId.OptimisticConcurrencyException,
                RelationalEventId.CommandError));
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IBranchRepository, BranchRepository>();
        services.AddScoped<IStaffRepository, StaffRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<ICounterTransactionRepository, CounterTransactionRepository>();
        services.AddScoped<IShippingZoneRepository, ShippingZoneRepository>();
        services.AddScoped<IStockAdjustmentCommand, StockAdjustmentCommand>();
        services.AddScoped<IPriceRevisionCommand, PriceRevisionCommand>();
        services.AddScoped<IDashboardQueries, DashboardQueries>();
        services.AddScoped<IReportQueries, ReportQueries>();
        services.AddScoped<IScreenQueries, ScreenQueries>();
        services.AddScoped<DatabaseSeeder>();
        services.AddSingleton<IAuditStore, SqlAuditStore>();
        return services;
    }
}

// dotnet-ef needs a context at design time. This placeholder holds no credentials and only generates code and scripts.
public sealed class ApexPostDesignTimeFactory : IDesignTimeDbContextFactory<ApexPostDbContext>
{
    public ApexPostDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("APEXPOST_DESIGN_CONNECTION")
            ?? "Server=localhost;Database=ApexPostDb;Integrated Security=true;TrustServerCertificate=true";
        return new ApexPostDbContext(new DbContextOptionsBuilder<ApexPostDbContext>().UseSqlServer(connectionString).Options);
    }
}
