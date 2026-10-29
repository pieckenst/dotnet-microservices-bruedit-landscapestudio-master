using Microsoft.EntityFrameworkCore;
using Project.Application.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Project.Infrastructure.Persistence;
using Project.Infrastructure.Repositories;
using Shared.Common.Common;
using Shared.Common.Persistence;

namespace Project.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddProjectInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDatabaseProviderStrategy, PostgresProviderStrategy>();
        services.AddSingleton<IDatabaseProviderStrategy, SqlServerProviderStrategy>();
        services.AddSingleton<IDatabaseProviderStrategy, SqliteProviderStrategy>();
        services.AddSingleton<DatabaseProviderFactory>();
        services.AddSingleton<DatabaseProviderResolution>(serviceProvider =>
        {
            var factory = serviceProvider.GetRequiredService<DatabaseProviderFactory>();
            var provider = configuration["Database:Provider"] ?? "PostgreSQL";
            var primary = configuration.GetConnectionString("ProjectConnectionString") ?? string.Empty;
            var fallback = configuration.GetConnectionString("SqliteConnectionString") ?? "Data Source=project.db";
            return factory.Resolve(provider, primary, fallback);
        });
        services.AddDbContext<ProjectContext>((serviceProvider, options) =>
        {
            var selection = serviceProvider.GetRequiredService<DatabaseProviderResolution>();
            selection.Strategy.Configure(options, selection.ConnectionString);
        });
        services.AddScoped<DbContext>(serviceProvider => serviceProvider.GetRequiredService<ProjectContext>());
        services.AddScoped(typeof(IRepositoryBase<>), typeof(RepositoryBase<>));
        services.AddScoped<IEstimateRepository, EstimateRepository>();
        return services;
    }
}
