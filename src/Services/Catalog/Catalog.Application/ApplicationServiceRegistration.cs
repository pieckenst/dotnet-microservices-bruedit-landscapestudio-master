using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.ServiceKit;

namespace Catalog.Application;

public sealed class CatalogApplicationRegistration : ServiceRegistrationBase
{
    public override IServiceCollection AddApplicationServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        return RegisterStandard(services, configuration, typeof(CatalogApplicationRegistration).Assembly);
    }
}

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddCatalogApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return new CatalogApplicationRegistration().AddApplicationServices(services, configuration);
    }
}
