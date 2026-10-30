using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace by.pieckenst.bru.windowsclient.landscapestudio;

internal sealed class DesktopApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
    private readonly HttpClient _http;
    private string? _accessToken;

    public DesktopApiClient(Uri baseAddress)
    {
        if (baseAddress is null || !baseAddress.IsAbsoluteUri ||
            baseAddress.Scheme is not ("http" or "https"))
            throw new ArgumentException("Enter an absolute HTTP or HTTPS gateway URL.", nameof(baseAddress));
        var normalized = baseAddress.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? baseAddress
            : new Uri(baseAddress.AbsoluteUri + "/", UriKind.Absolute);
        BaseAddress = normalized;
        _http = new HttpClient { BaseAddress = normalized, Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public Uri BaseAddress { get; }
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);
    public string? UserName { get; private set; }
    public string? Role { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }

    public async Task LoginAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentException("Username is required.", nameof(userName));
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Password is required.", nameof(password));

        using var response = await _http.PostAsJsonAsync("Auth/login", new LoginRequest(userName.Trim(), password), JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new DesktopApiException(response.StatusCode, await ReadErrorAsync(response, cancellationToken));

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;
        _accessToken = ReadString(root, "accessToken", "AccessToken");
        if (string.IsNullOrWhiteSpace(_accessToken))
            throw new DesktopApiException(response.StatusCode, "Identity returned a response without an access token.");

        var user = root.TryGetProperty("user", out var lowerUser) ? lowerUser :
            root.TryGetProperty("User", out var upperUser) ? upperUser : default;
        UserName = ReadString(user, "userName", "UserName") ?? userName.Trim();
        Role = ReadString(user, "role", "Role") ?? "User";
        var expires = ReadString(root, "expiresAtUtc", "ExpiresAtUtc");
        ExpiresAtUtc = DateTime.TryParse(expires, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed) ? parsed : null;
    }

    public async Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new DesktopApiException(response.StatusCode, await ReadErrorAsync(response, cancellationToken));
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }

    public async Task<JsonElement> PostAsync<T>(string path, T payload, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, path, payload);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new DesktopApiException(response.StatusCode, await ReadErrorAsync(response, cancellationToken));
        if (response.StatusCode == HttpStatusCode.NoContent)
            return JsonDocument.Parse("{}").RootElement.Clone();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }

    public async Task PutAsync<T>(string path, T payload, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, path, payload);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new DesktopApiException(response.StatusCode, await ReadErrorAsync(response, cancellationToken));
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, path);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new DesktopApiException(response.StatusCode, await ReadErrorAsync(response, cancellationToken));
    }

    public async Task PatchAsync(string path, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Patch, path);
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new DesktopApiException(response.StatusCode, await ReadErrorAsync(response, cancellationToken));
    }

    private HttpRequestMessage CreateRequest<T>(HttpMethod method, string path, T? payload)
    {
        var request = new HttpRequestMessage(method, path);
        if (payload is not null) request.Content = JsonContent.Create(payload, options: JsonOptions);
        if (!string.IsNullOrWhiteSpace(_accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", Guid.NewGuid().ToString("N"));
        return request;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path) =>
        CreateRequest<object>(method, path, null);

    private static string? ReadString(JsonElement parent, params string[] properties)
    {
        if (parent.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in properties)
            if (parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        return null;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body)) return $"Gateway returned {(int)response.StatusCode} ({response.ReasonPhrase}).";
        try
        {
            using var document = JsonDocument.Parse(body);
            foreach (var key in new[] { "detail", "message", "title", "error" })
                if (document.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString() ?? body;
        }
        catch (JsonException) { }
        return body.Length > 500 ? body[..500] : body;
    }

    public void Dispose() => _http.Dispose();

    private sealed record LoginRequest(string UserName, string Password);
}

internal sealed class DesktopApiException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public bool IsUnauthorized => StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}
