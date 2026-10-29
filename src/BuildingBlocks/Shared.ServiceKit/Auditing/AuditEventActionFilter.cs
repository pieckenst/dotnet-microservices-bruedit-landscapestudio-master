using EventBus.Messages.Events;
using MassTransit;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Shared.ServiceKit.Auditing;

/// <summary>
/// Emits a compact audit event after successful state-changing controller actions.
/// Request bodies and query values are deliberately not recorded.
/// </summary>
public sealed class AuditEventActionFilter(
    IPublishEndpoint publishEndpoint,
    IConfiguration configuration,
    ILogger<AuditEventActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        var http = context.HttpContext;
        var method = http.Request.Method;
        if (method is "GET" or "HEAD" or "OPTIONS" || executed.Exception is not null ||
            executed.ExceptionHandled || http.Response.StatusCode is < 200 or >= 300)
            return;

        var routeValues = context.ActionDescriptor.RouteValues;
        var controller = routeValues.TryGetValue("controller", out var controllerName)
            ? controllerName ?? "Unknown"
            : "Unknown";
        var action = routeValues.TryGetValue("action", out var actionName)
            ? actionName ?? method
            : method;
        var resourceId = ResolveResourceId(http.Request.RouteValues);
        var actorId = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub");
        var correlationId = http.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId)) correlationId = http.TraceIdentifier;

        try
        {
            await publishEndpoint.Publish(new AuditEvent
            {
                SourceService = configuration["Audit:SourceService"] ??
                    configuration["ServiceName"] ?? controller,
                Action = $"{controller}.{action}",
                EntityType = controller,
                EntityId = resourceId,
                ActorId = actorId,
                CorrelationId = correlationId,
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["httpMethod"] = method,
                    ["statusCode"] = http.Response.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }
            }, http.RequestAborted);
        }
        catch (Exception exception)
        {
            // Do not turn a committed business operation into a misleading 500 response.
            // The log entry makes event-bus failures visible for operations monitoring.
            logger.LogError(exception,
                "Could not publish audit event for {Service} {Action} ({CorrelationId})",
                configuration["Audit:SourceService"] ?? controller, action, correlationId);
        }
    }

    private static string? ResolveResourceId(RouteValueDictionary routeValues)
    {
        foreach (var key in new[] { "id", "projectId", "clientId", "estimateId", "supplierId", "orderId", "addressId" })
        {
            if (routeValues.TryGetValue(key, out var value) && value is not null)
                return value.ToString();
        }
        return null;
    }
}
