using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Project.Domain.Entities;
using Project.Infrastructure.Persistence;
using Shared.Common.Outbox;
using Shared.ServiceKit.Outbox;
using Xunit;

namespace Project.Tests;

public sealed class OutboxTransactionMiddlewareTests
{
    [Fact]
    public async Task SuccessfulMutationCommitsBusinessDataAndOutboxBeforeWritingResponse()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProjectContext>().UseSqlite(connection).Options;
        await using var db = new ProjectContext(options);
        await db.Database.EnsureCreatedAsync();

        var responseWasWritten = false;
        var middleware = new OutboxTransactionMiddleware<ProjectContext>(
            async http =>
            {
                db.Clients.Add(new Client(Guid.NewGuid(), "Transactional client", null, null, null));
                db.Set<IntegrationOutboxMessage>().Add(new IntegrationOutboxMessage(
                    "EventBus.Messages.Events.AuditEvent", "{}", DateTime.UtcNow));
                await db.SaveChangesAsync();
                http.Response.StatusCode = StatusCodes.Status201Created;
                await http.Response.WriteAsync("created");
                responseWasWritten = true;
            },
            NullLogger<OutboxTransactionMiddleware<ProjectContext>>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/v1/clients";
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        await middleware.InvokeAsync(httpContext, db);

        Assert.True(responseWasWritten);
        Assert.Equal("created", System.Text.Encoding.UTF8.GetString(responseBody.ToArray()));
        Assert.Equal(1, await db.Clients.AsNoTracking().CountAsync());
        Assert.Equal(1, await db.Set<IntegrationOutboxMessage>().AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task FailedMutationRollsBackBusinessDataAndOutbox()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProjectContext>().UseSqlite(connection).Options;
        await using var db = new ProjectContext(options);
        await db.Database.EnsureCreatedAsync();

        var middleware = new OutboxTransactionMiddleware<ProjectContext>(
            async http =>
            {
                db.Clients.Add(new Client(Guid.NewGuid(), "Rejected client", null, null, null));
                db.Set<IntegrationOutboxMessage>().Add(new IntegrationOutboxMessage(
                    "EventBus.Messages.Events.AuditEvent", "{}", DateTime.UtcNow));
                await db.SaveChangesAsync();
                http.Response.StatusCode = StatusCodes.Status400BadRequest;
                await http.Response.WriteAsync("rejected");
            },
            NullLogger<OutboxTransactionMiddleware<ProjectContext>>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/v1/clients";
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        await middleware.InvokeAsync(httpContext, db);

        Assert.Equal("rejected", System.Text.Encoding.UTF8.GetString(responseBody.ToArray()));
        Assert.Equal(0, await db.Clients.AsNoTracking().CountAsync());
        Assert.Equal(0, await db.Set<IntegrationOutboxMessage>().AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task LoginBypassesRequestTransactionSoFailedLoginCountersCanPersist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ProjectContext>().UseSqlite(connection).Options;
        await using var db = new ProjectContext(options);
        await db.Database.EnsureCreatedAsync();

        var called = false;
        var middleware = new OutboxTransactionMiddleware<ProjectContext>(
            http =>
            {
                called = true;
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            NullLogger<OutboxTransactionMiddleware<ProjectContext>>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/v1/auth/login";

        await middleware.InvokeAsync(httpContext, db);

        Assert.True(called);
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
    }
}
