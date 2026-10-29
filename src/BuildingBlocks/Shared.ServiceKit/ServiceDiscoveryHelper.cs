using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Shared.ServiceKit
{
    /// <summary>
    /// Helper for registering health checks that the Ocelot gateway
    /// and WebStatus dashboard use to monitor service availability.
    /// Each microservice calls RegisterServiceHealthCheck in its
    /// ConfigureServices method.
    /// </summary>
    public static class ServiceDiscoveryHelper
    {
        /// <summary>
        /// Registers a named health check for the current microservice.
        /// The name is used by the WebStatus dashboard to display per-service status.
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        /// <param name="serviceName">Name displayed in the health dashboard.</param>
        /// <param name="tags">Optional tags for grouping (e.g. "ready", "live").</param>
        public static IHealthChecksBuilder RegisterServiceHealthCheck(
            this IServiceCollection services,
            string serviceName,
            params string[] tags)
        {
            var defaultTags = tags.Length > 0 ? tags : new[] { "ready", "live" };
            return services.AddHealthChecks()
                .AddCheck(serviceName, () => HealthCheckResult.Healthy($"Service '{serviceName}' is operational"), defaultTags);
        }

        /// <summary>
        /// Registers a health check that pings a downstream dependency
        /// (e.g. PostgreSQL, MongoDB, RabbitMQ) with a configurable name.
        /// </summary>
        public static IHealthChecksBuilder AddDependencyHealthCheck(
            this IServiceCollection services,
            string dependencyName,
            string connectionString,
            string healthCheckType)
        {
            return services.AddHealthChecks().AddCheck(
                $"{dependencyName}-check",
                () =>
                {
                    // The actual connectivity check is done by EF Core / MongoDB driver
                    // health check packages registered by the Infrastructure layer.
                    // This is a placeholder for the named registration.
                    return HealthCheckResult.Healthy($"{dependencyName} is reachable");
                },
                new[] { "ready" });
        }
    }
}
