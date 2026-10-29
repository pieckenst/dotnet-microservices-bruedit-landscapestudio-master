using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.ServiceKit
{
    /// <summary>
    /// Contract that every microservice in the Landscape Studio system implements.
    /// Defines the service identity and its service/middleware registration hooks.
    /// New microservices implement this interface to plug into the standard bootstrap pipeline.
    /// </summary>
    public interface IServiceDefinition
    {
        /// <summary>Human-readable name of the service (e.g. "Identity", "Project").</summary>
        string ServiceName { get; }

        /// <summary>API version string (e.g. "v1").</summary>
        string Version { get; }

        /// <summary>
        /// Registers all DI services for this microservice (Application + Infrastructure layers,
        /// MediatR, AutoMapper, FluentValidation, DbContext, Swagger, JWT, etc.).
        /// </summary>
        void RegisterServices(IServiceCollection services, IConfiguration configuration);

        /// <summary>
        /// Configures the middleware pipeline for this microservice
        /// (routing, Swagger, exception handling, health checks, etc.).
        /// </summary>
        void ConfigureMiddleware(Microsoft.AspNetCore.Builder.IApplicationBuilder app, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env);
    }
}
