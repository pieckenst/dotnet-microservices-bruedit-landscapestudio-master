using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Organization.API.Services;
using Xunit;

namespace Organization.Tests;

public sealed class ProjectAssignmentLookupTests
{
    [Fact]
    public async Task ProjectAndTaskLookupsForwardCallerCredentialsAndValidateProjectOwnership()
    {
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var requests = new List<(string Path, string? Authorization, string? CorrelationId)>();
        var handler = new StubHandler(request =>
        {
            requests.Add((
                request.RequestUri!.AbsolutePath,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Correlation-ID", out var values) ? values.Single() : null));

            var json = request.RequestUri.AbsolutePath.EndsWith($"/projects/{projectId:D}", StringComparison.Ordinal)
                ? $$"""{"id":"{{projectId:D}}","status":"InProgress"}"""
                : $$"""{"id":"{{taskId:D}}","projectId":"{{projectId:D}}","status":"NotStarted"}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://project.test/") };
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer caller-token";
        context.Request.Headers["X-Correlation-ID"] = "organization-correlation";
        var lookup = new HttpProjectAssignmentLookup(
            http, new HttpContextAccessor { HttpContext = context },
            NullLogger<HttpProjectAssignmentLookup>.Instance);

        var projectStatus = await lookup.GetProjectStatusAsync(projectId, CancellationToken.None);
        var taskStatus = await lookup.GetProjectTaskStatusAsync(projectId, taskId, CancellationToken.None);

        Assert.Equal("InProgress", projectStatus);
        Assert.Equal("NotStarted", taskStatus);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.Equal("Bearer caller-token", request.Authorization));
        Assert.All(requests, request => Assert.Equal("organization-correlation", request.CorrelationId));
        Assert.Contains(requests, request => request.Path.EndsWith($"/projects/{projectId:D}", StringComparison.Ordinal));
        Assert.Contains(requests, request => request.Path.EndsWith($"/project-tasks/{taskId:D}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TaskFromAnotherProjectIsNotAccepted()
    {
        var requestedProjectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"id":"{{taskId:D}}","projectId":"{{otherProjectId:D}}","status":"NotStarted"}""",
                Encoding.UTF8, "application/json")
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://project.test/") };
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer caller-token";
        var lookup = new HttpProjectAssignmentLookup(
            http, new HttpContextAccessor { HttpContext = context },
            NullLogger<HttpProjectAssignmentLookup>.Instance);

        var result = await lookup.GetProjectTaskStatusAsync(requestedProjectId, taskId, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task MissingProjectPermissionIsNotSilentlyTreatedAsNotFound()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://project.test/") };
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer caller-token";
        var lookup = new HttpProjectAssignmentLookup(
            http, new HttpContextAccessor { HttpContext = context },
            NullLogger<HttpProjectAssignmentLookup>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            lookup.GetProjectStatusAsync(Guid.NewGuid(), CancellationToken.None));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
