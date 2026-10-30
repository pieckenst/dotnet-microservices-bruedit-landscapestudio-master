using Common.Logging;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Organization.Application;
using Organization.Infrastructure;
using Organization.Infrastructure.Persistence;
using Serilog;
using Shared.ServiceKit.Auditing;
using Shared.ServiceKit.Outbox;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var keyText = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(keyText))
{
    if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Jwt:SigningKey must be configured outside Development.");
    keyText = "local-development-signing-key-change-before-deployment-2026";
    builder.Configuration["Jwt:SigningKey"] = keyText;
}
if (Encoding.UTF8.GetByteCount(keyText) < 32) throw new InvalidOperationException("Jwt:SigningKey must be at least 32 UTF-8 bytes.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyText));
var issuer = builder.Configuration["Jwt:Issuer"] ?? "LandscapeStudio.Identity";
var audience = builder.Configuration["Jwt:Audience"] ?? "LandscapeStudio.Clients";

builder.Host.UseSerilog(SeriLogger.Configure);
builder.Services.AddOrganizationApplication(builder.Configuration);
builder.Services.AddOrganizationInfrastructure(builder.Configuration);
builder.Services.AddMutationAuditEvents<OrganizationContext>(builder.Configuration, "Organization");
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
    options.AddPolicy("organization.read", p => p.RequireAuthenticatedUser().RequireClaim("permission", "organization.read"));
    options.AddPolicy("organization.write", p => p.RequireAuthenticatedUser().RequireClaim("permission", "organization.write"));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Landscaping Studio Organization API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});
builder.Services.AddHealthChecks().AddDbContextCheck<OrganizationContext>("organization-database");
var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<OrganizationContext>();
    await context.Database.EnsureCreatedAsync();
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Organization API v1"));
}
app.UseMiddleware<Organization.API.Middleware.ExceptionHandlingMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<OutboxTransactionMiddleware<OrganizationContext>>();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => true, ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse });
await app.RunAsync();

public partial class Program { }
