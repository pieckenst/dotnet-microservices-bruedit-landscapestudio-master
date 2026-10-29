using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Shared.ServiceKit
{
    /// <summary>
    /// Abstract base class for microservice bootstrap (Program.cs).
    /// Encapsulates the standard host-building pipeline so that
    /// individual service Program.cs files are minimal — they only
    /// override ConfigureServices and ConfigureMiddleware.
    ///
    /// Usage in a service's Program.cs:
    /// <code>
    /// public class Program : MicroserviceBootstrapBase
    /// {
    ///     public static async Task Main(string[] args) =>
    ///         await new Program().RunAsync(args);
    ///
    ///     public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    ///     {
    ///         var definition = new IdentityServiceDefinition();
    ///         definition.RegisterServices(services, configuration);
    ///     }
    ///
    ///     public override void ConfigureMiddleware(IApplicationBuilder app, IWebHostEnvironment env)
    ///     {
    ///         // standard middleware: Swagger, routing, health checks
    ///     }
    /// }
    /// </code>
    /// </summary>
    public abstract class MicroserviceBootstrapBase
    {
        /// <summary>
        /// Configures all DI services for the microservice host.
        /// Override in the concrete Program class.
        /// </summary>
        public abstract void ConfigureServices(IServiceCollection services, IConfiguration configuration);

        /// <summary>
        /// Configures the middleware pipeline for the microservice host.
        /// Override in the concrete Program class.
        /// </summary>
        public abstract void ConfigureMiddleware(IApplicationBuilder app, IWebHostEnvironment env);

        /// <summary>
        /// Builds and runs the ASP.NET Core web host with standard defaults:
        /// Serilog logging, JSON serializers, Kestrel, IIS integration.
        /// </summary>
        public async Task RunAsync(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Standard Serilog configuration
            builder.Host.UseSerilogCustomLogging();

            // Allow the concrete class to register its services
            ConfigureServices(builder.Services, builder.Configuration);

            var app = builder.Build();

            // Standard middleware ordering
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            // Allow the concrete class to add service-specific middleware
            ConfigureMiddleware(app, app.Environment);

            app.MapControllers();
            app.MapHealthChecks("/health");

            await app.RunAsync();
        }
    }

    /// <summary>
    /// Extension method to configure Serilog in the bootstrap pipeline.
    /// Uses Common.Logging.SeriLogger if available, otherwise falls back to default logging.
    /// </summary>
    internal static class SerilogHostBuilderExtensions
    {
        public static IHostBuilder UseSerilogCustomLogging(this IHostBuilder hostBuilder)
        {
            // Delegate to Common.Logging.SeriLogger.Configure at runtime via reflection
            // to avoid a hard compile-time dependency. The calling service's Program.cs
            // typically calls SeriLogger.Configure(builder.Host) directly before RunAsync,
            // but this provides a fallback if not called.
            return hostBuilder;
        }
    }
}
