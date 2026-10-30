using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.ServiceKit;

namespace Project.Application;

public sealed class ProjectApplicationRegistration : ServiceRegistrationBase
{
    public override IServiceCollection AddApplicationServices(IServiceCollection services, IConfiguration configuration) =>
        RegisterStandard(services, configuration, typeof(ProjectApplicationRegistration).Assembly);
}

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddProjectApplication(this IServiceCollection services, IConfiguration configuration) =>
        new ProjectApplicationRegistration().AddApplicationServices(services, configuration);
}
