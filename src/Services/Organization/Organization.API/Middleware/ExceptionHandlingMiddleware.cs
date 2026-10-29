using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared.Common.Exceptions;
using System.Text.Json;
using DomainValidationException = Shared.Common.Exceptions.ValidationException;

namespace Organization.API.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (context.Response.HasStarted) { logger.LogError(exception, "Organization response already started."); throw; }
            var (status, title, detail, errors) = exception switch
            {
                NotFoundException => (404, "Resource not found", exception.Message, (object?)null),
                KeyNotFoundException => (404, "Resource not found", exception.Message, (object?)null),
                FluentValidation.ValidationException validation => (400, "Validation failed", "One or more fields are invalid.",
                    (object)validation.Errors.GroupBy(x => x.PropertyName).ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray())),
                DomainValidationException validation => (400, "Validation failed", "One or more fields are invalid.", (object?)validation.Errors),
                ArgumentException argument => (400, "Invalid request", argument.Message, (object?)null),
                DbUpdateException => (409, "Persistence conflict", "The change conflicts with existing organization data.", (object?)null),
                InvalidOperationException operation => (409, "Operation cannot be completed", operation.Message, (object?)null),
                _ => (500, "Internal server error", "The server could not complete the request.", (object?)null)
            };
            if (status >= 500) logger.LogError(exception, "Unhandled Organization API failure for {Method} {Path}.", context.Request.Method, context.Request.Path);
            context.Response.Clear(); context.Response.StatusCode = status; context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails { Status = status, Title = title, Detail = detail, Instance = context.Request.Path };
            problem.Extensions["traceId"] = context.TraceIdentifier;
            if (errors is not null) problem.Extensions["errors"] = errors;
            await JsonSerializer.SerializeAsync(context.Response.Body, problem, cancellationToken: context.RequestAborted);
        }
    }
}
