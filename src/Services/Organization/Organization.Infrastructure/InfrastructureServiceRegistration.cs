using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Organization.Infrastructure.Persistence;
using Shared.Common.Common;

namespace Organization.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddOrganizationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDatabaseProviderStrategy, PostgresProviderStrategy>();
        services.AddSingleton<IDatabaseProviderStrategy, SqlServerProviderStrategy>();
        services.AddSingleton<IDatabaseProviderStrategy, SqliteProviderStrategy>();
        services.AddSingleton<DatabaseProviderFactory>();
        services.AddSingleton<DatabaseProviderResolution>(sp =>
        {
            var factory = sp.GetRequiredService<DatabaseProviderFactory>();
            var provider = configuration["Database:Provider"] ?? "PostgreSQL";
            var primary = configuration.GetConnectionString("OrganizationConnectionString") ?? string.Empty;
            var fallback = configuration.GetConnectionString("SqliteConnectionString") ?? "Data Source=organization.db";
            return factory.Resolve(provider, primary, fallback);
        });
        services.AddDbContext<OrganizationContext>((sp, options) =>
        {
            var selection = sp.GetRequiredService<DatabaseProviderResolution>();
            selection.Strategy.Configure(options, selection.ConnectionString);
        });
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<OrganizationContext>());
        services.AddScoped(typeof(Shared.Common.Persistence.IRepositoryBase<>), typeof(Shared.Common.Persistence.RepositoryBase<>));
        return services;
    }
}
