using Account.API.EventBus;
using Account.Infrastructure;
using Account.Infrastructure.Persistence;
using Common.Logging;
using EventBus.Messages.Common;
using HealthChecks.UI.Client;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Shared.Common.Common;
using Shared.ServiceKit.Auditing;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog(SeriLogger.Configure);

var keyText = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(keyText))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Jwt:SigningKey must be configured outside Development.");
    keyText = "local-development-signing-key-change-before-deployment-2026";
}
if (Encoding.UTF8.GetByteCount(keyText) < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 UTF-8 bytes.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyText));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
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
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddScoped<AuditEventActionFilter>();
builder.Services.AddMassTransit(registration =>
{
    registration.AddConsumer<UserRegisteredConsumer>();
    registration.UsingRabbitMq((context, bus) =>
    {
        var host = builder.Configuration["EventBusSettings:HostAddress"] ?? "amqp://guest:guest@localhost:5672";
        bus.Host(host);
        bus.ReceiveEndpoint(EventBusConstants.UserRegisteredQueue, endpoint =>
            endpoint.ConfigureConsumer<UserRegisteredConsumer>(context));
    });
});
builder.Services.Configure<MassTransitHostOptions>(options =>
{
    options.WaitUntilStarted = true;
    options.StartTimeout = TimeSpan.FromSeconds(30);
    options.StopTimeout = TimeSpan.FromMinutes(1);
});
builder.Services.AddControllers(options => options.Filters.AddService<AuditEventActionFilter>());
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Landscape Studio Account API",
        Version = "v1",
        Description = "User profile, contact information, addresses and preferences; credentials are owned by Identity."
    });
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
var rabbitHost = builder.Configuration["EventBusSettings:HostAddress"] ?? "amqp://guest:guest@localhost:5672";
builder.Services.AddHealthChecks()
    .AddRabbitMQ(rabbitHost, name: "identity-events")
    .AddDbContextCheck<AccountContext>("account-profile-database");

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var selection = scope.ServiceProvider.GetRequiredService<DatabaseProviderResolution>();
    var context = scope.ServiceProvider.GetRequiredService<AccountContext>();
    if (selection.Strategy.ProviderName.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        await context.Database.EnsureCreatedAsync();
    else
        await context.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Account API v1"));
}
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
app.MapGet("/", () => Results.Ok(new
{
    service = "Landscape Studio Account",
    owns = new[] { "profiles", "contact information", "addresses", "preferences" },
    credentialsOwnedBy = "Identity",
    health = "/health"
}));
await app.RunAsync();

public partial class Program { }
