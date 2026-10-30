using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.ServiceKit;

namespace Procurement.Application;

public sealed class ProcurementApplicationRegistration : ServiceRegistrationBase
{
    public override IServiceCollection AddApplicationServices(IServiceCollection services, IConfiguration configuration) =>
        RegisterStandard(services, configuration, typeof(ProcurementApplicationRegistration).Assembly);
}

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddProcurementApplication(this IServiceCollection services, IConfiguration configuration) =>
        new ProcurementApplicationRegistration().AddApplicationServices(services, configuration);
}