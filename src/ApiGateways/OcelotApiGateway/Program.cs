using Common.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.Cache.CacheManager;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load the shared route table first, then apply environment-specific overrides.
// Do not let a development-only route file replace the full Landscape Studio route map.
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile(
    $"ocelot.{builder.Environment.EnvironmentName}.json",
    optional: true,
    reloadOnChange: true);

var signingKeyText = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(signingKeyText))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Jwt:SigningKey must be supplied through deployment configuration.");
    signingKeyText = "local-development-signing-key-change-before-deployment-2026";
}
if (Encoding.UTF8.GetByteCount(signingKeyText) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must contain at least 32 UTF-8 bytes.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKeyText));
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "GatewayJwt";
        options.DefaultChallengeScheme = "GatewayJwt";
    })
    .AddJwtBearer("GatewayJwt", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "LandscapeStudio.Identity",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "LandscapeStudio.Clients",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddOcelot().AddCacheManager(settings => settings.WithDictionaryHandle());
builder.Host.UseSerilog(SeriLogger.Configure);

var app = builder.Build();
if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseAuthentication();
app.UseAuthorization();

await app.UseOcelot();
app.Run();

public partial class Program { }
