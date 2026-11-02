using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace LandscapeStudio.Web.Services;

public sealed class LandscapeApiClient(HttpClient http, IHttpContextAccessor accessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<T> GetAsync<T>(string path, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Get, path, cancellationToken: cancellationToken);

    public Task<T> PostAsync<TRequest, T>(string path, TRequest body,
        bool includeToken = true, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Post, path, body, includeToken, cancellationToken);

    public Task<T> PutAsync<TRequest, T>(string path, TRequest body,
        CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Put, path, body, cancellationToken: cancellationToken);

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default) =>
        SendWithoutResponseAsync(HttpMethod.Delete, path, cancellationToken);

    public async Task<(byte[] Content, string ContentType, string FileName)> GetFileAsync(
        string path, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, path, includeToken: true);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(response.StatusCode, await GetErrorMessageAsync(response, cancellationToken));

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName
            ?? "export.csv";
        fileName = fileName.Trim('"');
        return (await response.Content.ReadAsByteArrayAsync(cancellationToken),
            response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream", fileName);
    }

    public async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null,
        bool includeToken = true, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(method, path, body, includeToken);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(response.StatusCode, await GetErrorMessageAsync(response, cancellationToken));

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            if (typeof(T) == typeof(object)) return (T)(object)new object();
            throw new ApiException(response.StatusCode, "The API returned no content for a response that requires a body.");
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
            throw new ApiException(response.StatusCode, "The API returned an empty response.");
        try
        {
            var value = JsonSerializer.Deserialize<T>(payload, JsonOptions);
            return value is null
                ? throw new ApiException(response.StatusCode, "The API returned an empty JSON value.")
                : value;
        }
        catch (JsonException exception)
        {
            throw new ApiException(response.StatusCode, "The API returned an unexpected response format.", exception);
        }
    }

    private async Task SendWithoutResponseAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, includeToken: true);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(response.StatusCode, await GetErrorMessageAsync(response, cancellationToken));
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body = null, bool includeToken = true)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        var context = accessor.HttpContext;
        if (includeToken)
        {
            var token = context?.Session.GetString("access_token");
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var correlationId = context?.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId)) correlationId = context?.TraceIdentifier;
        if (!string.IsNullOrWhiteSpace(correlationId))
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        return request;
    }

    private static async Task<string> GetErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
            return $"Gateway returned {(int)response.StatusCode} ({response.ReasonPhrase}).";

        try
        {
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;
            foreach (var property in new[] { "detail", "message", "title", "error" })
                if (root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString() ?? body;
        }
        catch (JsonException) { }

        return body.Length > 500 ? body[..500] : body;
    }
}

public sealed class ApiException(HttpStatusCode statusCode, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public bool IsUnauthorized => StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}
