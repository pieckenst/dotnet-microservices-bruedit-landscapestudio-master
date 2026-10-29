using Common.Logging;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Procurement.Infrastructure;
using Procurement.Infrastructure.Persistence;
using Serilog;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
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
builder.Host.UseSerilog(SeriLogger.Configure);
builder.Services.AddProcurementInfrastructure(builder.Configuration);
builder.Services.AddControllers();
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
    options.AddPolicy("procurement.read", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("permission", "procurement.read"));
    options.AddPolicy("procurement.write", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("permission", "procurement.write"));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Landscaping Studio Procurement API",
        Version = "v1",
        Description = "Supplier management, purchase orders, inventory movements, and deliveries."
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
builder.Services.AddHealthChecks().AddDbContextCheck<ProcurementContext>("procurement-database");

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ProcurementContext>();
    await context.Database.EnsureCreatedAsync();
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Procurement API v1"));
}
app.UseMiddleware<Procurement.API.Middleware.ExceptionHandlingMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
await app.RunAsync();

public partial class Program { }