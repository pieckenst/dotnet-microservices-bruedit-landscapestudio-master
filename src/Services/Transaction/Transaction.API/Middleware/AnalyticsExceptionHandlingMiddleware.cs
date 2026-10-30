using Microsoft.AspNetCore.Mvc;

namespace Transaction.API.Middleware;

/// <summary>
/// Converts unexpected service failures into a stable ProblemDetails response while retaining
/// the exception and trace identifier in structured server logs.
/// </summary>
public sealed class AnalyticsExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<AnalyticsExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Unhandled analytics API exception for {HttpMethod} {RequestPath}; trace {TraceId}",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected server error occurred."
            };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            await context.Response.WriteAsJsonAsync(problem, "application/problem+json", cancellationToken: context.RequestAborted);
        }
    }
}
