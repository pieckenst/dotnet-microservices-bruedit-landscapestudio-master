using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Project.Application.Contracts;
using Project.Domain.Enums;

namespace Project.API.Services;

/// <summary>
/// Reads a catalog item from its owning service. The caller's bearer token is forwarded so
/// Catalog's own catalog.read policy is still enforced; Project does not use a shared secret
/// or bypass the user's permissions.
/// </summary>
public sealed class HttpCatalogItemLookup(
    HttpClient httpClient,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpCatalogItemLookup> logger) : ICatalogItemLookup
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<CatalogItemReference?> FindAsync(
        Guid catalogItemId,
        EstimateLineItemType itemType,
        CancellationToken cancellationToken)
    {
        if (catalogItemId == Guid.Empty)
            throw new ArgumentException("A valid Catalog item id is required.", nameof(catalogItemId));

        var resource = itemType switch
        {
            EstimateLineItemType.Work => "work-types",
            EstimateLineItemType.Material => "materials",
            EstimateLineItemType.Plant => "plants",
            _ => throw new FluentValidation.ValidationException(
                "Catalog links are supported for work types, materials, and plants only.")
        };

        var authorization = httpContextAccessor.HttpContext?.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authorization) ||
            !AuthenticationHeaderValue.TryParse(authorization, out var bearer) ||
            !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(bearer.Parameter))
        {
            throw new UnauthorizedAccessException("A caller access token is required to validate a Catalog item.");
        }

        using var itemRequest = new HttpRequestMessage(HttpMethod.Get, $"api/v1/{resource}/{catalogItemId:D}");
        itemRequest.Headers.Authorization = bearer;
        CopyCorrelationId(itemRequest);

        using var itemResponse = await httpClient.SendAsync(
            itemRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (itemResponse.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (itemResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                "Catalog denied the item lookup. The caller must have the catalog.read permission.");

        if (!itemResponse.IsSuccessStatusCode)
        {
            var detail = await SafeReadBodyAsync(itemResponse, cancellationToken);
            logger.LogWarning(
                "Catalog lookup for {CatalogItemId} returned HTTP {StatusCode}.",
                catalogItemId, (int)itemResponse.StatusCode);
            throw new HttpRequestException(
                $"Catalog item lookup failed with HTTP {(int)itemResponse.StatusCode}. {detail}",
                null, itemResponse.StatusCode);
        }

        using var itemDocument = await JsonDocument.ParseAsync(
            await itemResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var item = itemDocument.RootElement;
        var measurementUnitId = ReadGuid(item, "measurementUnitId");
        if (measurementUnitId == Guid.Empty)
            throw new InvalidOperationException($"Catalog item '{catalogItemId}' has no valid measurement unit.");

        var itemName = ReadString(item, "name");
        if (string.IsNullOrWhiteSpace(itemName))
            throw new InvalidOperationException($"Catalog item '{catalogItemId}' has no name.");

        var priceProperty = itemType == EstimateLineItemType.Work ? "basePrice" : "unitPrice";
        var price = ReadDecimal(item, priceProperty);

        using var unitRequest = new HttpRequestMessage(
            HttpMethod.Get, $"api/v1/measurement-units/{measurementUnitId:D}");
        unitRequest.Headers.Authorization = bearer;
        CopyCorrelationId(unitRequest);

        using var unitResponse = await httpClient.SendAsync(
            unitRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (unitResponse.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException(
                $"Catalog item '{catalogItemId}' references a missing measurement unit '{measurementUnitId}'.");
        if (unitResponse.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                "Catalog denied the measurement-unit lookup. The caller must have the catalog.read permission.");
        if (!unitResponse.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Catalog measurement-unit lookup failed with HTTP {(int)unitResponse.StatusCode}.",
                null, unitResponse.StatusCode);

        using var unitDocument = await JsonDocument.ParseAsync(
            await unitResponse.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var unit = unitDocument.RootElement;
        var symbol = ReadString(unit, "symbol");
        if (string.IsNullOrWhiteSpace(symbol))
            throw new InvalidOperationException(
                $"Catalog measurement unit '{measurementUnitId}' has no symbol.");

        return new CatalogItemReference(
            catalogItemId,
            itemName.Trim(),
            ReadString(item, "description") ?? string.Empty,
            measurementUnitId,
            symbol.Trim(),
            price,
            ReadBoolean(item, "isActive"),
            ReadBoolean(unit, "isActive"));
    }

    private void CopyCorrelationId(HttpRequestMessage request)
    {
        var correlationId = httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(correlationId))
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
    }

    private static string? ReadString(JsonElement source, string propertyName)
    {
        if (!TryGetProperty(source, propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static Guid ReadGuid(JsonElement source, string propertyName)
    {
        var value = ReadString(source, propertyName);
        return Guid.TryParse(value, out var result) ? result : Guid.Empty;
    }

    private static decimal ReadDecimal(JsonElement source, string propertyName)
    {
        if (!TryGetProperty(source, propertyName, out var value))
            throw new InvalidOperationException($"Catalog response is missing '{propertyName}'.");
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number;
        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number))
            return number;
        throw new InvalidOperationException($"Catalog response contains an invalid '{propertyName}' value.");
    }

    private static bool ReadBoolean(JsonElement source, string propertyName) =>
        TryGetProperty(source, propertyName, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool TryGetProperty(JsonElement source, string propertyName, out JsonElement value)
    {
        if (source.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in source.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static async Task<string> SafeReadBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(body)
            ? string.Empty
            : body.Length <= 300 ? body : body[..300];
    }
}
