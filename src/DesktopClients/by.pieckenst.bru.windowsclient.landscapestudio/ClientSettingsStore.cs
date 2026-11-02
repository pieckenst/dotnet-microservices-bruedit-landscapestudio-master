using System.Text.Json;

namespace by.pieckenst.bru.windowsclient.landscapestudio;

internal static class ClientSettingsStore
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LandscapeStudio");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "desktop-settings.json");

    public static Uri LoadGatewayUri()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
                if (document.RootElement.TryGetProperty("gatewayBaseUrl", out var value) &&
                    value.ValueKind == JsonValueKind.String &&
                    Uri.TryCreate(value.GetString(), UriKind.Absolute, out var stored) &&
                    stored.Scheme is "http" or "https")
                    return EnsureTrailingSlash(stored);
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException) { }
        return new Uri("http://localhost:8010/", UriKind.Absolute);
    }

    public static void SaveGatewayUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Only absolute HTTP or HTTPS URLs can be saved.", nameof(uri));
        Directory.CreateDirectory(SettingsDirectory);
        var content = JsonSerializer.Serialize(new DesktopSettings(EnsureTrailingSlash(uri).AbsoluteUri),
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, content);
    }

    private static Uri EnsureTrailingSlash(Uri uri) =>
        uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri(uri.AbsoluteUri + "/");

    private sealed record DesktopSettings(string GatewayBaseUrl);
}
