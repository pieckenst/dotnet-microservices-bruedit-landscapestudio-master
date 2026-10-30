using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Project.API.Services;
using Project.Domain.Enums;
using Xunit;

namespace Project.Tests;

public sealed class HttpCatalogItemLookupTests
{
    [Fact]
    public async Task MaterialLookupForwardsCallerTokenAndCorrelationIdAndReturnsCatalogSnapshot()
    {
        var itemId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var requests = new List<(string Path, string? Authorization, string? CorrelationId)>();
        var handler = new StubHandler(request =>
        {
            requests.Add((
                request.RequestUri!.AbsolutePath,
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("X-Correlation-ID", out var values) ? values.Single() : null));

            var json = request.RequestUri.AbsolutePath.EndsWith($"/materials/{itemId:D}", StringComparison.Ordinal)
                ? $$"""{"id":"{{itemId:D}}","name":"Granite paving","description":"Cut granite","measurementUnitId":"{{unitId:D}}","unitPrice":48.50,"isActive":true}"""
                : $$"""{"id":"{{unitId:D}}","name":"Square metre","symbol":"m²","isActive":true}""";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://catalog.test/") };
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer user-access-token";
        context.Request.Headers["X-Correlation-ID"] = "correlation-123";
        var lookup = new HttpCatalogItemLookup(
            http, new HttpContextAccessor { HttpContext = context },
            NullLogger<HttpCatalogItemLookup>.Instance);

        var result = await lookup.FindAsync(itemId, EstimateLineItemType.Material, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(itemId, result.Id);
        Assert.Equal("Granite paving", result.Name);
        Assert.Equal("m²", result.MeasurementUnitSymbol);
        Assert.Equal(48.50m, result.ListUnitPrice);
        Assert.True(result.IsActive);
        Assert.True(result.MeasurementUnitIsActive);
        Assert.Equal(2, requests.Count);
        Assert.All(requests, request => Assert.Equal("Bearer user-access-token", request.Authorization));
        Assert.All(requests, request => Assert.Equal("correlation-123", request.CorrelationId));
        Assert.Contains(requests, request => request.Path.EndsWith($"/materials/{itemId:D}", StringComparison.Ordinal));
        Assert.Contains(requests, request => request.Path.EndsWith($"/measurement-units/{unitId:D}", StringComparison.Ordinal));
        http.Dispose();
    }

    [Theory]
    [InlineData(0, "work-types", "basePrice", 125.50)]
    [InlineData(2, "plants", "unitPrice", 34.75)]
    public async Task WorkAndPlantLookupsUseTheirCatalogSpecificPriceFields(
        int itemType, string resource, string priceProperty, decimal expectedPrice)
    {
        var itemId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var requestedPath = string.Empty;
        var handler = new StubHandler(request =>
        {
            requestedPath = request.RequestUri!.AbsolutePath;
            var json = requestedPath.EndsWith($"/measurement-units/{unitId:D}", StringComparison.Ordinal)
                ? $"""{"id":"{{unitId:D}}","name":"Unit","symbol":"pcs","isActive":true}"""
                : $"""{"id":"{{itemId:D}}","name":"Catalog item","description":"Description","measurementUnitId":"{{unitId:D}}","{{priceProperty}}":{{expectedPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"isActive":true}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://catalog.test/") };
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer user-access-token";
        var lookup = new HttpCatalogItemLookup(
            http, new HttpContextAccessor { HttpContext = context },
            NullLogger<HttpCatalogItemLookup>.Instance);

        var result = await lookup.FindAsync(itemId, (EstimateLineItemType)itemType, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedPrice, result.ListUnitPrice);
        Assert.Contains($"/{resource}/{itemId:D}", requestedPath);
    }

    [Fact]
    public async Task CatalogPermissionFailureIsReturnedAsAccessDenied()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://catalog.test/") };
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer token";
        var lookup = new HttpCatalogItemLookup(
            http, new HttpContextAccessor { HttpContext = context },
            NullLogger<HttpCatalogItemLookup>.Instance);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            lookup.FindAsync(Guid.NewGuid(), EstimateLineItemType.Plant, CancellationToken.None));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
