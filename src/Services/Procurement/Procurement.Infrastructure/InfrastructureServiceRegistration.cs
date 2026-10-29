using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Procurement.Infrastructure.Persistence;
using Shared.Common.Common;

namespace Procurement.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddProcurementInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDatabaseProviderStrategy, PostgresProviderStrategy>();
        services.AddSingleton<IDatabaseProviderStrategy, SqlServerProviderStrategy>();
        services.AddSingleton<IDatabaseProviderStrategy, SqliteProviderStrategy>();
        services.AddSingleton<DatabaseProviderFactory>();
        services.AddSingleton<DatabaseProviderResolution>(sp =>
        {
            var factory = sp.GetRequiredService<DatabaseProviderFactory>();
            var provider = configuration["Database:Provider"] ?? "PostgreSQL";
            var primary = configuration.GetConnectionString("ProcurementConnectionString") ?? string.Empty;
            var fallback = configuration.GetConnectionString("SqliteConnectionString") ?? "Data Source=procurement.db";
            return factory.Resolve(provider, primary, fallback);
        });
        services.AddDbContext<ProcurementContext>((sp, options) =>
        {
            var selection = sp.GetRequiredService<DatabaseProviderResolution>();
            selection.Strategy.Configure(options, selection.ConnectionString);
        });
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<ProcurementContext>());
        services.AddScoped(typeof(Shared.Common.Persistence.IRepositoryBase<>), typeof(Shared.Common.Persistence.RepositoryBase<>));
        return services;
    }
}
