using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.ServiceKit;

namespace Identity.Application;

public sealed class IdentityApplicationRegistration : ServiceRegistrationBase
{
    public override IServiceCollection AddApplicationServices(IServiceCollection services, IConfiguration configuration) =>
        RegisterStandard(services, configuration, typeof(IdentityApplicationRegistration).Assembly);
}

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services, IConfiguration configuration) =>
        new IdentityApplicationRegistration().AddApplicationServices(services, configuration);
}
