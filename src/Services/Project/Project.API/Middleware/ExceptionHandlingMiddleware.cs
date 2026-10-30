using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Exceptions;
using System.Text.Json;
using DomainValidationException = Shared.Common.Exceptions.ValidationException;

namespace Project.API.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(exception, "Project request failed after the response started.");
                throw;
            }
            var (status, title, detail, errors) = MapException(exception);
            if (status >= 500)
                logger.LogError(exception, "Unhandled Project API failure for {Method} {Path}.", context.Request.Method, context.Request.Path);
            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails { Status = status, Title = title, Detail = detail, Instance = context.Request.Path };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            if (errors is not null) problem.Extensions["errors"] = errors;
            await JsonSerializer.SerializeAsync(context.Response.Body, problem, cancellationToken: context.RequestAborted);
        }
    }
    private static (int Status, string Title, string? Detail, object? Errors) MapException(Exception exception) =>
        exception switch
        {
            ForbiddenException => (403, "Forbidden", "You do not have permission to perform this action.", null),
            UnauthorizedAccessException => (403, "Catalog access denied", "The caller needs catalog.read permission to reference Catalog records.", null),
            HttpRequestException => (503, "Catalog unavailable", "The Catalog service could not complete the lookup. Retry after the dependency is healthy.", null),
            TaskCanceledException =>
                (504, "Catalog lookup timed out", "The Catalog service did not respond within the configured timeout.", null),
            NotFoundException => (404, "Resource not found", exception.Message, null),
            KeyNotFoundException => (404, "Resource not found", exception.Message, null),
            FluentValidation.ValidationException validation => (400, "Validation failed", "One or more fields are invalid.",
                validation.Errors.GroupBy(error => error.PropertyName).ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())),
            DomainValidationException validation => (400, "Validation failed", "One or more fields are invalid.", validation.Errors),
            ArgumentException argument => (400, "Invalid request", argument.Message, null),
            DbUpdateException => (409, "Persistence conflict", "The change conflicts with existing project data.", null),
            InvalidOperationException operation => (409, "Operation cannot be completed", operation.Message, null),
            _ => (500, "Internal server error", "The server could not complete the request.", null)
        };
}
