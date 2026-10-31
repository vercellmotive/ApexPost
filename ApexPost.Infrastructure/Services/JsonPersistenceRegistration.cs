using ApexPost.Application.Interfaces;
using ApexPost.Domain.Common;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApexPost.Infrastructure.Services;

public static class JsonPersistenceRegistration
{
    public static IServiceCollection AddJsonPersistence(this IServiceCollection services, string contentRootPath)
    {
        // Resolved from the built configuration so environment and test overrides are always honoured.
        services.AddSingleton(provider =>
        {
            var configuredDirectory = provider.GetRequiredService<IConfiguration>()["Persistence:DataDirectory"];
            return new JsonStoreOptions(
                Path.GetFullPath(
                    string.IsNullOrWhiteSpace(configuredDirectory) ? "App_Data" : configuredDirectory,
                    contentRootPath),
                Path.Combine(AppContext.BaseDirectory, "Data"));
        });

        services.AddJsonRepository<Branch, BranchCode>();
        services.AddJsonRepository<Staff, string>();
        services.AddJsonRepository<Product, Sku>();
        services.AddJsonRepository<Transaction, Guid>();
        services.AddJsonRepository<BranchStock, Guid>();
        services.AddJsonRepository<ShippingZone, string>();
        services.AddSingleton<IAuditStore, JsonAuditStore>();
        return services;
    }

    private static void AddJsonRepository<TEntity, TId>(this IServiceCollection services)
        where TEntity : Entity<TId>
        where TId : notnull =>
        services.AddSingleton<IRepository<TEntity, TId>, JsonFileRepository<TEntity, TId>>();
}
