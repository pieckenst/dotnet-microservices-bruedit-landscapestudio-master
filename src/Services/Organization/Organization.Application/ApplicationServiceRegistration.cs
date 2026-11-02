using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.ServiceKit;

namespace Organization.Application;

public sealed class OrganizationApplicationRegistration : ServiceRegistrationBase
{
    public override IServiceCollection AddApplicationServices(IServiceCollection services, IConfiguration configuration) =>
        RegisterStandard(services, configuration, typeof(OrganizationApplicationRegistration).Assembly);
}

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddOrganizationApplication(this IServiceCollection services, IConfiguration configuration) =>
        new OrganizationApplicationRegistration().AddApplicationServices(services, configuration);
}
