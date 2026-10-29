using Catalog.Application;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using Common.Logging;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog(SeriLogger.Configure);
builder.Services.AddCatalogApplication(builder.Configuration);
builder.Services.AddCatalogInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Landscaping Studio Catalog API",
        Version = "v1",
        Description = "Plants, materials, work types, and measurement units."
    });
});
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<CatalogContext>("catalog-database");

var app = builder.Build();

// This initial schema bootstrap is intentionally centralized until EF migrations are checked in.
// Do not mix EnsureCreated with Database.Migrate in the same database.
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<CatalogContext>();
    await context.Database.EnsureCreatedAsync();

    var seedLogger = scope.ServiceProvider.GetRequiredService<ILogger<CatalogContextSeed>>();
    await CatalogContextSeed.SeedAsync(context, seedLogger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Catalog API v1"));
}

app.UseMiddleware<Catalog.API.Middleware.ExceptionHandlingMiddleware>();
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

await app.RunAsync();

public partial class Program { }
