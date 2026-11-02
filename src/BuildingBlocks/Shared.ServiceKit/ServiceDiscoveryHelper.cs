using System.Data.Common;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Shared.ServiceKit
{
    /// <summary>
    /// Registers real readiness checks for services and their network dependencies.
    /// </summary>
    public static class ServiceDiscoveryHelper
    {
        public static IHealthChecksBuilder RegisterServiceHealthCheck(
            this IServiceCollection services,
            string serviceName,
            params string[] tags)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
            var defaultTags = tags.Length > 0 ? tags : new[] { "ready", "live" };
            return services.AddHealthChecks()
                .AddCheck(serviceName, () => HealthCheckResult.Healthy($"Service '{serviceName}' is operational"), defaultTags);
        }

        /// <summary>
        /// Adds a bounded TCP connectivity probe for PostgreSQL, MongoDB, RabbitMQ,
        /// SQL Server, or another URI/host-and-port dependency.
        /// This verifies that the configured endpoint accepts connections; protocol-level
        /// health checks should still be preferred when the owning service has them.
        /// </summary>
        public static IHealthChecksBuilder AddDependencyHealthCheck(
            this IServiceCollection services,
            string dependencyName,
            string connectionString,
            string healthCheckType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(dependencyName);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            ArgumentException.ThrowIfNullOrWhiteSpace(healthCheckType);

            if (!TryResolveEndpoint(connectionString, healthCheckType, out var host, out var port))
                throw new ArgumentException(
                    $"Could not resolve a host and port for dependency '{dependencyName}'.",
                    nameof(connectionString));

            return services.AddHealthChecks().AddCheck(
                $"{dependencyName}-check",
                new TcpDependencyHealthCheck(dependencyName, host, port),
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready" });
        }

        private static bool TryResolveEndpoint(
            string connectionString,
            string healthCheckType,
            out string host,
            out int port)
        {
            host = string.Empty;
            port = 0;
            var type = healthCheckType.Trim().ToLowerInvariant();
            var defaultPort = type switch
            {
                "postgres" or "postgresql" or "npgsql" => 5432,
                "mongo" or "mongodb" => 27017,
                "rabbit" or "rabbitmq" or "amqp" => 5672,
                "sqlserver" or "mssql" => 1433,
                _ => 0
            };

            if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri) &&
                !string.IsNullOrWhiteSpace(uri.Host))
            {
                host = uri.Host;
                port = uri.IsDefaultPort ? defaultPort : uri.Port;
                return port is > 0 and <= 65535;
            }

            try
            {
                var values = new DbConnectionStringBuilder { ConnectionString = connectionString };
                foreach (var key in new[] { "Host", "Server", "Data Source", "Address", "Network Address" })
                {
                    if (values.TryGetValue(key, out var value) && value is not null &&
                        !string.IsNullOrWhiteSpace(Convert.ToString(value)))
                    {
                        host = Convert.ToString(value)!.Trim();
                        break;
                    }
                }

                if (values.TryGetValue("Port", out var portValue) &&
                    int.TryParse(Convert.ToString(portValue), out var configuredPort))
                    port = configuredPort;

                var comma = host.LastIndexOf(',');
                if (comma > 0 && int.TryParse(host[(comma + 1)..], out var serverPort))
                {
                    port = serverPort;
                    host = host[..comma].Trim();
                }

                if (port == 0) port = defaultPort;
                return !string.IsNullOrWhiteSpace(host) && port is > 0 and <= 65535;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    internal sealed class TcpDependencyHealthCheck(string dependencyName, string host, int port) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(host, port, timeout.Token);
                return HealthCheckResult.Healthy(
                    $"{dependencyName} accepts TCP connections at {host}:{port}.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return HealthCheckResult.Unhealthy(
                    $"{dependencyName} did not accept a TCP connection within three seconds.");
            }
            catch (Exception exception) when (exception is SocketException or IOException)
            {
                return HealthCheckResult.Unhealthy(
                    $"{dependencyName} is not accepting TCP connections at {host}:{port}.",
                    exception);
            }
        }
    }
}
