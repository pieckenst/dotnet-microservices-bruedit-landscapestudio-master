using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Organization.Application.Contracts;

namespace Organization.API.Services;

public sealed class HttpProjectAssignmentLookup(
    HttpClient http,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpProjectAssignmentLookup> logger) : IProjectAssignmentLookup
{
    public async Task<string?> GetProjectStatusAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty) return null;
        using var response = await SendAsync($"/api/v1/projects/{projectId:D}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, "Project", projectId, cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return ReadString(document.RootElement, "status");
    }

    public async Task<string?> GetProjectTaskStatusAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken)
    {
        if (projectId == Guid.Empty || taskId == Guid.Empty) return null;
        using var response = await SendAsync($"/api/v1/project-tasks/{taskId:D}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response, "Project task", taskId, cancellationToken);
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);

        if (!Guid.TryParse(ReadString(document.RootElement, "projectId"), out var returnedProjectId) ||
            returnedProjectId != projectId)
            return null;

        return ReadString(document.RootElement, "status");
    }

    private async Task<HttpResponseMessage> SendAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("A caller access token is required to validate Project references.");

        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        var correlationId = httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-ID"].ToString();
        if (!string.IsNullOrWhiteSpace(correlationId))
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Project reference lookup timed out for {Path}.", path);
            throw new HttpRequestException("Project service did not respond before the reference lookup timeout.");
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response, string referenceType, Guid id, CancellationToken cancellationToken)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("The caller is not permitted to validate Project references.");

        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"{referenceType} lookup failed with HTTP {(int)response.StatusCode}: {details}",
                null, response.StatusCode);
        }
    }

    private static string? ReadString(JsonElement source, string name) =>
        TryGetProperty(source, name, out var value) &&
        value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;

    private static bool TryGetProperty(JsonElement source, string name, out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object)
            foreach (var property in source.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
        value = default;
        return false;
    }
}
