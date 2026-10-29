using Account.Application.Contracts.Persistence;
using Account.Infrastructure.Persistence;
using Account.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Common.Common;

namespace Account.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDatabaseProviderStrategy, PostgresProviderStrategy>();
        services.AddSingleton<IDatabaseProviderStrategy, SqlServerProviderStrategy>();
        services.AddSingleton<IDatabaseProviderStrategy, SqliteProviderStrategy>();
        services.AddSingleton<DatabaseProviderFactory>();
        services.AddSingleton<DatabaseProviderResolution>(sp =>
        {
            var factory = sp.GetRequiredService<DatabaseProviderFactory>();
            var provider = configuration["Database:Provider"] ?? "PostgreSQL";
            var primary = configuration.GetConnectionString("AccountConnectionString") ?? string.Empty;
            var fallback = configuration.GetConnectionString("SqliteConnectionString") ?? "Data Source=account.db";
            return factory.Resolve(provider, primary, fallback);
        });
        services.AddDbContext<AccountContext>((sp, options) =>
        {
            var selection = sp.GetRequiredService<DatabaseProviderResolution>();
            selection.Strategy.Configure(options, selection.ConnectionString);
        });
        services.AddScoped(typeof(IRepositoryBase<>), typeof(RepositoryBase<>));
        services.AddScoped<IAccountRepository, AccountRepository>();
        return services;
    }
}
