using Common.Logging;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Project.Application;
using Project.Application.Contracts;
using Project.API.Services;
using Project.Infrastructure;
using Project.Infrastructure.Persistence;
using Serilog;
using Shared.ServiceKit.Auditing;
using Shared.ServiceKit.Outbox;
using Shared.Common.Outbox;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var signingKeyText = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKeyText))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Jwt:SigningKey must be supplied through deployment configuration.");
    signingKeyText = "local-development-signing-key-change-before-deployment-2026";
    builder.Configuration["Jwt:SigningKey"] = signingKeyText;
}
if (Encoding.UTF8.GetByteCount(signingKeyText) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");

var issuer = builder.Configuration["Jwt:Issuer"] ?? "LandscapeStudio.Identity";
var audience = builder.Configuration["Jwt:Audience"] ?? "LandscapeStudio.Clients";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKeyText));

builder.Host.UseSerilog(SeriLogger.Configure);
builder.Services.AddProjectApplication(builder.Configuration);
builder.Services.AddProjectInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ICatalogItemLookup, HttpCatalogItemLookup>((services, client) =>
{
    var catalogBaseUrl = services.GetRequiredService<IConfiguration>()["Catalog:BaseUrl"]
        ?? "http://localhost:8002/";
    if (!catalogBaseUrl.EndsWith("/", StringComparison.Ordinal))
        catalogBaseUrl += "/";
    client.BaseAddress = new Uri(catalogBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(8);
});
builder.Services.AddMutationAuditEvents<ProjectContext>(builder.Configuration, "Project");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true, IssuerSigningKey = signingKey,
        ValidateIssuer = true, ValidIssuer = issuer,
        ValidateAudience = true, ValidAudience = audience,
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("projects.read", policy => policy.RequireAuthenticatedUser().RequireClaim("permission", "projects.read"));
    options.AddPolicy("projects.write", policy => policy.RequireAuthenticatedUser().RequireClaim("permission", "projects.write"));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Landscaping Studio Project API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer",
        BearerFormat = "JWT", In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddHealthChecks().AddDbContextCheck<ProjectContext>("project-database");

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ProjectContext>();
    await context.Database.EnsureCreatedAsync();
    await context.Database.EnsureIntegrationOutboxSchemaAsync();
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Project API v1"));
}
app.UseMiddleware<Project.API.Middleware.ExceptionHandlingMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<OutboxTransactionMiddleware<ProjectContext>>();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
await app.RunAsync();

public partial class Program { }
