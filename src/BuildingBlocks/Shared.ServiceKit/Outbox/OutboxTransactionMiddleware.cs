using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Shared.ServiceKit.Outbox;

/// <summary>
/// Wraps a mutating HTTP request in one database transaction. Application handlers may call
/// SaveChanges as usual; those writes remain uncommitted until the audit filter and explicit
/// integration-event producers have added their outbox rows.
/// </summary>
public sealed class OutboxTransactionMiddleware<TContext>(
    RequestDelegate next,
    ILogger<OutboxTransactionMiddleware<TContext>> logger)
    where TContext : DbContext
{
    public async Task InvokeAsync(HttpContext httpContext, TContext context)
    {
        var method = httpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) ||
            HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            await next(httpContext);
            return;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(httpContext.RequestAborted);
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

            throw;
        }
    }
}
