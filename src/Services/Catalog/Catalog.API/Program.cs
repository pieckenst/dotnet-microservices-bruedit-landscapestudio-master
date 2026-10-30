using Catalog.Application;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using Common.Logging;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Shared.ServiceKit.Auditing;
using Shared.ServiceKit.Outbox;
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
builder.Services.AddCatalogApplication(builder.Configuration);
builder.Services.AddCatalogInfrastructure(builder.Configuration);
builder.Services.AddMutationAuditEvents<CatalogContext>(builder.Configuration, "Catalog");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("catalog.read", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("permission", "catalog.read"));
    options.AddPolicy("catalog.write", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("permission", "catalog.write"));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Landscaping Studio Catalog API",
        Version = "v1",
        Description = "Plants, materials, work types, and measurement units."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Supply an access token issued by the Identity service."
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
builder.Services.AddHealthChecks()
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
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<OutboxTransactionMiddleware<CatalogContext>>();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

await app.RunAsync();

public partial class Program { }
