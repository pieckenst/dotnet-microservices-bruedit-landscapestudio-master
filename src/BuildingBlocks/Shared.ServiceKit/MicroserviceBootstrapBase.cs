using Common.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Shared.ServiceKit
{
    /// <summary>
    /// Common ASP.NET Core bootstrap for services that want to use the shared service framework.
    /// Service-specific registration and middleware remain explicit in derived classes.
    /// </summary>
    public abstract class MicroserviceBootstrapBase
    {
        public abstract void ConfigureServices(IServiceCollection services, IConfiguration configuration);

        public abstract void ConfigureMiddleware(IApplicationBuilder app, IWebHostEnvironment env);

        public async Task RunAsync(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.UseSerilog(SeriLogger.Configure);

            // Ensure the standard HTTP and health endpoints can be mapped by the host.
            builder.Services.AddControllers();
            builder.Services.AddHealthChecks();
            builder.Services.AddAuthentication();
            builder.Services.AddAuthorization();

            ConfigureServices(builder.Services, builder.Configuration);

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            ConfigureMiddleware(app, app.Environment);

            app.MapControllers();
            app.MapHealthChecks("/health");

            await app.RunAsync();
        }
    }
}