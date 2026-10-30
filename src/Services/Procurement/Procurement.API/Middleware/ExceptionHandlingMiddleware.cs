using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Procurement.API.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // A disconnected caller should not be reported as a server failure.
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(exception, "Procurement request failed after the response started.");
                throw;
            }

            var (status, title, detail) = exception switch
            {
                ValidationException validation => (StatusCodes.Status400BadRequest, "Validation failed",
                    string.Join("; ", validation.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}"))),
                ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request", exception.Message),
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message),
                InvalidOperationException => (StatusCodes.Status409Conflict, "Operation cannot be completed", exception.Message),
                DbUpdateException => (StatusCodes.Status409Conflict, "Persistence conflict", "The change conflicts with existing procurement data."),
                _ => (StatusCodes.Status500InternalServerError, "Internal server error", "The server could not complete the request.")
            };
            if (status >= 500)
                logger.LogError(exception, "Unhandled Procurement API failure for {Method} {Path}.", context.Request.Method, context.Request.Path);
            else
                logger.LogInformation("Procurement request rejected with {StatusCode} for {Method} {Path}.",
                    status, context.Request.Method, context.Request.Path);

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = context.Request.Path
            };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            await JsonSerializer.SerializeAsync(context.Response.Body, problem, cancellationToken: context.RequestAborted);
        }
    }
}