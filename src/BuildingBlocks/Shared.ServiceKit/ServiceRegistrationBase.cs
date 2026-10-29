using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.ServiceKit
{
    /// <summary>
    /// Base application-layer registration for microservices.
    /// Registers MediatR handlers, AutoMapper profiles, FluentValidation validators,
    /// and the standard validation/exception pipeline behaviours.
    /// </summary>
    public abstract class ServiceRegistrationBase
    {
        /// <summary>Add service-specific application registrations.</summary>
        public abstract IServiceCollection AddApplicationServices(
            IServiceCollection services,
            IConfiguration configuration);

        /// <summary>
        /// Registers the shared application stack for the supplied assemblies.
        /// Derived registrations should call this method before adding service-specific dependencies.
        /// </summary>
        protected IServiceCollection RegisterStandard(
            IServiceCollection services,
            IConfiguration configuration,
            params Assembly[] assemblies)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            if (assemblies is null || assemblies.Length == 0)
                throw new ArgumentException("At least one application assembly must be supplied.", nameof(assemblies));

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(assemblies));
            services.AddAutoMapper(assemblies);

            foreach (var assembly in assemblies)
                services.AddValidatorsFromAssembly(assembly);

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));

            return services;
        }
    }
}