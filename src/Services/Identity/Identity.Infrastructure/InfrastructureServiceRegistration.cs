using Identity.Application.Contracts;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Common.Common;

namespace Identity.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDatabaseProviderStrategy, PostgresProviderStrategy>();
        services.AddSingleton<IDatabaseProviderStrategy, SqlServerProviderStrategy>();
        services.AddSingleton<IDatabaseProviderStrategy, SqliteProviderStrategy>();
        services.AddSingleton<DatabaseProviderFactory>();
        services.AddSingleton<DatabaseProviderResolution>(sp =>
        {
            var factory = sp.GetRequiredService<DatabaseProviderFactory>();
            var provider = configuration["Database:Provider"] ?? "PostgreSQL";
            var primary = configuration.GetConnectionString("IdentityConnectionString") ?? string.Empty;
            var fallback = configuration.GetConnectionString("SqliteConnectionString") ?? "Data Source=identity.db";
            return factory.Resolve(provider, primary, fallback);
        });
        services.AddDbContext<IdentityContext>((sp, options) =>
        {
            var selection = sp.GetRequiredService<DatabaseProviderResolution>();
            selection.Strategy.Configure(options, selection.ConnectionString);
        });
        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddSingleton<IPasswordHasher<UserCredential>, PasswordHasher<UserCredential>>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        return services;
    }
}
