using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.ServiceKit.Outbox;

namespace Shared.ServiceKit.Auditing;

public static class AuditEventPublishingExtensions
{
    public static IMvcBuilder AddMutationAuditEvents<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sourceService,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? configureBus = null)
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceService);

        configuration["Audit:SourceService"] = sourceService;
        services.AddScoped<AuditEventActionFilter>();
        services.AddScoped<IIntegrationOutbox, EfIntegrationOutbox<TDbContext>>();
        services.AddHostedService<IntegrationOutboxDispatcher<TDbContext>>();

        services.AddMassTransit(registration =>
        {
            configureConsumers?.Invoke(registration);
            registration.UsingRabbitMq((context, bus) =>
            {
                var host = configuration["EventBusSettings:HostAddress"]
                    ?? "amqp://guest:guest@localhost:5672";
                bus.Host(host);
                configureBus?.Invoke(context, bus);
            });
        });

        return services.AddControllers(options => options.Filters.AddService<AuditEventActionFilter>());
    }
}
