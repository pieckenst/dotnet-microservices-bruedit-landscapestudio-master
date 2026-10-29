using Common.Logging;
using EventBus.Messages.Common;
using HealthChecks.UI.Client;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Security.Claims;
using System.Text;
using Transaction.API.Data;
using Transaction.API.Data.Interfaces;
using Transaction.API.EventBusConsumer;
using Transaction.API.Repositories;
using Transaction.API.Repositories.Interfaces;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog(SeriLogger.Configure);

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
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("analytics.read", policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
        context.User.IsInRole("Administrator") ||
        context.User.HasClaim("permission", "analytics.read") ||
        context.User.HasClaim("permission", "audit.read")));
});

builder.Services.AddMassTransit(conf =>
{
    conf.AddConsumer<AuditEventConsumer>();
    conf.UsingRabbitMq((ctx, cfg) =>
    {
        var host = builder.Configuration["EventBusSettings:HostAddress"];
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("EventBusSettings:HostAddress must be configured.");
        cfg.Host(host);
        cfg.ReceiveEndpoint(EventBusConstants.AuditEventQueue, endpoint =>
            endpoint.ConfigureConsumer<AuditEventConsumer>(ctx));
    });
});
builder.Services.Configure<MassTransitHostOptions>(options =>
{
    options.WaitUntilStarted = true;
    options.StartTimeout = TimeSpan.FromSeconds(30);
    options.StopTimeout = TimeSpan.FromMinutes(1);
});

builder.Services.AddSingleton<IAuditEventContext, AuditEventContext>();
builder.Services.AddScoped<IAuditEventRepository, AuditEventRepository>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Landscape Studio Analytics and Audit API",
        Version = "v1",
        Description = "Immutable audit event storage, filtered queries, aggregate analytics and CSV export."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer",
        BearerFormat = "JWT", In = ParameterLocation.Header,
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

var mongoConnection = builder.Configuration["DatabaseSettings:ConnectionString"];
var rabbitHost = builder.Configuration["EventBusSettings:HostAddress"];
if (string.IsNullOrWhiteSpace(mongoConnection) || string.IsNullOrWhiteSpace(rabbitHost))
    throw new InvalidOperationException("Analytics database and event bus configuration are required.");

builder.Services.AddHealthChecks()
    .AddMongoDb(mongoConnection, name: "analytics-audit-database")
    .AddRabbitMQ(rabbitHost, name: "audit-event-bus");

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Analytics API v1"));
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
    service = "Landscape Studio Analytics",
    version = "v1",
    endpoints = new[] { "/api/v1/analytics/summary", "/api/v1/audit-events", "/api/v1/audit-events/export", "/health" }
}));
await app.RunAsync();

public partial class Program { }
