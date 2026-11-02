using EventBus.Messages.Events;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shared.ServiceKit.Outbox;
using System.Security.Claims;

namespace Shared.ServiceKit.Auditing;

/// <summary>
/// Enqueues a compact audit event after a successful state-changing controller action.
/// The outbox row is saved by OutboxTransactionMiddleware in the same transaction as
/// the business data. Request bodies, query values, and credentials are never recorded.
/// </summary>
public sealed class AuditEventActionFilter(
    IIntegrationOutbox outbox,
    IConfiguration configuration,
    ILogger<AuditEventActionFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        var http = context.HttpContext;
        var method = http.Request.Method;
        var actionStatus = (executed.Result as IStatusCodeActionResult)?.StatusCode ?? http.Response.StatusCode;
        if (method is "GET" or "HEAD" or "OPTIONS" or "TRACE" ||
            executed.Exception is not null || executed.ExceptionHandled ||
            actionStatus is < 200 or >= 300)
            return;

        var routeValues = context.ActionDescriptor.RouteValues;
        var controller = routeValues.TryGetValue("controller", out var controllerName)
            ? controllerName ?? "Unknown"
            : "Unknown";
        var action = routeValues.TryGetValue("action", out var actionName)
            ? actionName ?? method
            : method;

        // Login is intentionally outside the business outbox transaction: failed-login
        // counters must persist even when Identity responds with 401. Authentication
        // telemetry belongs in the dedicated security logging path, not this CRUD audit stream.
        if (controller.Equals("Auth", StringComparison.OrdinalIgnoreCase) &&
            action.Equals("Login", StringComparison.OrdinalIgnoreCase))
            return;

        var resourceId = ResolveResourceId(http.Request.RouteValues);
        var actorId = http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.FindFirstValue("sub");
        var correlationId = http.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId)) correlationId = http.TraceIdentifier;

        var auditEvent = new AuditEvent
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
                ["statusCode"] = actionStatus.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        };

        // Enqueue failures are allowed to fail the request. Returning success while the
        // audit row was not staged would silently violate the transaction/outbox guarantee.
        try
        {
            outbox.Enqueue(auditEvent);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Could not stage audit event for {Service} {Action} ({CorrelationId})",
                auditEvent.SourceService, action, correlationId);
            throw;
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
