using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Exceptions;
using System.Text.Json;
using DomainValidationException = Shared.Common.Exceptions.ValidationException;

namespace Catalog.API.Middleware;

/// <summary>Maps known application failures to safe Problem Details responses.</summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The caller disconnected; do not turn cancellation into a server error.
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(exception, "Request failed after the response had started.");
                throw;
            }

            var (status, title, detail, errors) = MapException(exception);
            if (status >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Unhandled Catalog API failure for {Method} {Path}.",
                    context.Request.Method, context.Request.Path);
            }

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
            if (errors is not null) problem.Extensions["errors"] = errors;

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                problem,
                cancellationToken: context.RequestAborted);
        }
    }

    private static (int Status, string Title, string? Detail, object? Errors) MapException(Exception exception)
    {
        return exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message, null),
            FluentValidation.ValidationException validation =>
                (StatusCodes.Status400BadRequest, "Validation failed", "One or more fields are invalid.",
                    validation.Errors
                        .GroupBy(error => error.PropertyName)
                        .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())),
            DomainValidationException validation =>
                (StatusCodes.Status400BadRequest, "Validation failed", "One or more fields are invalid.", validation.Errors),
            ArgumentException argument =>
                (StatusCodes.Status400BadRequest, "Invalid request", argument.Message, null),
            DbUpdateException =>
                (StatusCodes.Status409Conflict, "Persistence conflict",
                    "The change conflicts with existing catalog data. Check unique values and referenced measurement units.", null),
            InvalidOperationException operation =>
                (StatusCodes.Status409Conflict, "Operation cannot be completed", operation.Message, null),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error",
                "The server could not complete the request.", null)
        };
    }
}
