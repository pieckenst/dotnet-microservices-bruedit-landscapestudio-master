using Catalog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Common.Common;
using Shared.Common.Persistence;

namespace Catalog.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddCatalogInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDatabaseProviderStrategy, PostgresProviderStrategy>();
        services.AddSingleton<IDatabaseProviderStrategy, SqlServerProviderStrategy>();
        services.AddSingleton<IDatabaseProviderStrategy, SqliteProviderStrategy>();
        services.AddSingleton<DatabaseProviderFactory>();
        services.AddSingleton<DatabaseProviderResolution>(serviceProvider =>
        {
            var providerFactory = serviceProvider.GetRequiredService<DatabaseProviderFactory>();
            var provider = configuration["Database:Provider"] ?? "PostgreSQL";
            var primaryConnection = configuration.GetConnectionString("CatalogConnectionString") ?? string.Empty;
            var fallbackConnection = configuration.GetConnectionString("SqliteConnectionString")
                ?? "Data Source=catalog.db";

            // Resolve once per service process. A database outage after startup must not route
            // a later request to a different, empty SQLite database and silently fork the data.
            return providerFactory.Resolve(provider, primaryConnection, fallbackConnection);
        });

        services.AddDbContext<CatalogContext>((serviceProvider, options) =>
        {
            var resolved = serviceProvider.GetRequiredService<DatabaseProviderResolution>();
            resolved.Strategy.Configure(options, resolved.ConnectionString);
        });

        // RepositoryBase<T> accepts DbContext; this mapping resolves the same scoped CatalogContext
        // instance rather than creating a second context.
        services.AddScoped<DbContext>(serviceProvider => serviceProvider.GetRequiredService<CatalogContext>());
        services.AddScoped(typeof(IRepositoryBase<>), typeof(RepositoryBase<>));

        return services;
    }
}
