using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Shared.ServiceKit.Outbox;

/// <summary>
/// Wraps a mutating HTTP request in one database transaction. Application handlers may call
/// SaveChanges as usual; those writes remain uncommitted until the audit filter and explicit
/// integration-event producers have added their outbox rows. The response body is buffered so
/// a successful status is not sent to the client until the database commit has succeeded.
/// </summary>
public sealed class OutboxTransactionMiddleware<TContext>(
    RequestDelegate next,
    ILogger<OutboxTransactionMiddleware<TContext>> logger)
    where TContext : DbContext
{
    public async Task InvokeAsync(HttpContext httpContext, TContext context)
    {
        var method = httpContext.Request.Method;
        // Identity intentionally commits failed-login counters before returning 401.
        // A transaction that rolls back every non-2xx response would undo the lockout policy.
        if (httpContext.Request.Path.Equals("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase) ||
            HttpMethods.IsGet(method) || HttpMethods.IsHead(method) ||
            HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            await next(httpContext);
            return;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(httpContext.RequestAborted);
        var originalBody = httpContext.Response.Body;
        await using var responseBuffer = new MemoryStream();
        httpContext.Response.Body = responseBuffer;

        try
        {
            await next(httpContext);

            if (httpContext.Response.StatusCode is >= (int)HttpStatusCode.OK and < (int)HttpStatusCode.MultipleChoices)
            {
                await context.SaveChangesAsync(httpContext.RequestAborted);
                await transaction.CommitAsync(httpContext.RequestAborted);
            }
            else
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }

            // Flush only after commit/rollback has completed. If commit fails, the catch
            // path restores the original stream and lets the outer exception handler write 500.
            responseBuffer.Position = 0;
            httpContext.Response.Body = originalBody;
            await responseBuffer.CopyToAsync(originalBody, httpContext.RequestAborted);
        }
        catch
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                logger.LogError(rollbackException,
                    "Failed to roll back database transaction for request {TraceId}.",
                    httpContext.TraceIdentifier);
            }

            httpContext.Response.Body = originalBody;
            throw;
        }
        finally
        {
            httpContext.Response.Body = originalBody;
        }
    }
}
