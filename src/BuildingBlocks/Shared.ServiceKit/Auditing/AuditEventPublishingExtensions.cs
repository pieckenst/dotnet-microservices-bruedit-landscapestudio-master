using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.ServiceKit.Auditing;

public static class AuditEventPublishingExtensions
{
    public static IMvcBuilder AddMutationAuditEvents(
        this IServiceCollection services, IConfiguration configuration, string sourceService)
    {
        if (string.IsNullOrWhiteSpace(sourceService))
            throw new ArgumentException("A source service name is required.", nameof(sourceService));

        configuration["Audit:SourceService"] = sourceService;
        services.AddScoped<AuditEventActionFilter>();
        services.AddMassTransit(registration =>
        {
            registration.UsingRabbitMq((context, bus) =>
            {
                var host = configuration["EventBusSettings:HostAddress"] ?? "amqp://guest:guest@localhost:5672";
                bus.Host(host);
            });
        });

        return services.AddControllers(options => options.Filters.AddService<AuditEventActionFilter>());
    }
}
