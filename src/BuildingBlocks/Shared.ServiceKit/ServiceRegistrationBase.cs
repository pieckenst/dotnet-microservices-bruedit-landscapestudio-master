using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.ServiceKit
{
    /// <summary>
    /// Base class for the Application-layer service registration in every microservice.
    /// Provides the standard MediatR + AutoMapper + FluentValidation + pipeline behaviours
    /// registration that was previously duplicated across Account.Application and Customer.Application.
    ///
    /// Usage in a service's ApplicationServiceRegistration:
    /// <code>
    /// public static class ApplicationServiceRegistration
    /// {
    ///     public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    ///     {
    ///         var registrar = new ProjectServiceRegistrar();
    ///         return registrar.RegisterStandard(services, configuration,
    ///             typeof(ProjectApplicationAssemblyMarker).Assembly);
    ///     }
    /// }
    /// </code>
    /// </summary>
    public abstract class ServiceRegistrationBase
    {
        /// <summary>
        /// Subclasses override this to add service-specific registrations
        /// (e.g. gRPC clients, event bus publishers, custom services).
        /// </summary>
        public abstract IServiceCollection AddApplicationServices(IServiceCollection services, IConfiguration configuration);

        /// <summary>
        /// Registers the standard MediatR, AutoMapper, and FluentValidation stack
        /// plus the MediatR pipeline behaviours (ValidationBehaviour, UnhandledExceptionBehaviour).
        /// All services inherit this registration, eliminating boilerplate duplication.
        /// </summary>
        protected IServiceCollection RegisterStandard(
            IServiceCollection services,
            IConfiguration configuration,
            params Assembly[] assemblies)
        {
            // MediatR
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(assemblies));

            // AutoMapper — scan the provided assemblies for Profile subclasses
            services.AddAutoMapper(assemblies);

            // FluentValidation — register all AbstractValidator<> types
            foreach (var assembly in assemblies)
            {
                services.AddValidatorsFromAssembly(assembly);
            }

            // MediatR pipeline behaviours
            services.AddTransient(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(Microsoft.Extensions.Logging.Logger<>));

            return services;
        }
    }
}
