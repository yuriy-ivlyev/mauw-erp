using System.Net;
using System.Text.Json;
using Mauw.Erp.Api.Endpoints;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public sealed class InventoryStockEndpointsTests
{
    [Fact]
    public async Task StocksSummary_DoesNotRequirePageAndReturnsNestedTotalsForBothPrefixes()
    {
        var summary = new InventoryStocksSummaryDto(
            42,
            50,
            1,
            new InventoryStockTotalsDto(8m, 2m, 3m, 9m, 123.45m));
        var page = new InventoryStocksPageDto(
            [],
            2,
            50,
            new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc),
            null,
            "Default warehouse allocation");
        var requests = new List<InventoryStockFilterRequest>();
        var queryService = new Mock<IInventoryStockQueryService>();
        queryService
            .Setup(service => service.GetStocksPageAsync(
                It.IsAny<InventoryStockFilterRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<InventoryStocksPageDto>.Ok(page));
        queryService
            .Setup(service => service.GetStocksSummaryAsync(
                It.IsAny<InventoryStockFilterRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<InventoryStockFilterRequest, CancellationToken>((request, _) => requests.Add(request))
            .ReturnsAsync(OperationResult<InventoryStocksSummaryDto>.Ok(summary));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton(queryService.Object);
        builder.Services.AddScoped<ICompanySettingsService>(_ => throw new NotSupportedException());
        builder.Services.AddScoped<IInventoryStockPdfGenerator>(_ => throw new NotSupportedException());
        builder.Services.AddTestRoleAuthorization();
        await using var app = builder.Build();
        app.UseDeveloperExceptionPage();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapInventoryStockEndpoints();
        await app.StartAsync();
        var warehouseId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        try
        {
            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = TestRoleAuthentication.CreateTestClient(new Uri(address), "Admin");
            var pageResponse = await client.GetAsync(
                "/api/v1/inventory/stocks?page=2&pageSize=50&sortDescending=false" +
                "&onlyInStock=false&valuationMethod=WeightedAverageCost");
            Assert.True(
                pageResponse.StatusCode == HttpStatusCode.OK,
                await pageResponse.Content.ReadAsStringAsync());
            using (var pageBody = JsonDocument.Parse(await pageResponse.Content.ReadAsStringAsync()))
            {
                Assert.Equal(2, pageBody.RootElement.GetProperty("page").GetInt32());
                Assert.True(pageBody.RootElement.TryGetProperty("items", out _));
                Assert.False(pageBody.RootElement.TryGetProperty("totalCount", out _));
                Assert.False(pageBody.RootElement.TryGetProperty("totals", out _));
            }

            const string filterQuery =
                "?search=SKU&atDate=2026-10-25T12%3A00%3A00Z&onlyInStock=true" +
                "&valuationMethod=LastPurchaseCost&pageSize=50";

            foreach (var prefix in new[] { "/api/inventory", "/api/v1/inventory" })
            {
                var response = await client.GetAsync(
                    $"{prefix}/stocks/summary{filterQuery}&warehouseId={warehouseId}&categoryId={categoryId}");

                Assert.True(
                    response.StatusCode == HttpStatusCode.OK,
                    await response.Content.ReadAsStringAsync());
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = body.RootElement;
                Assert.Equal(42, root.GetProperty("totalCount").GetInt32());
                Assert.Equal(50, root.GetProperty("pageSize").GetInt32());
                Assert.Equal(1, root.GetProperty("totalPages").GetInt32());
                var totals = root.GetProperty("totals");
                Assert.Equal(8m, totals.GetProperty("totalPhysicalQuantity").GetDecimal());
                Assert.Equal(123.45m, totals.GetProperty("totalValue").GetDecimal());
                Assert.False(root.TryGetProperty("items", out _));
            }
        }
        finally
        {
            await app.StopAsync();
        }

        Assert.Equal(2, requests.Count);
        foreach (var request in requests)
        {
            Assert.Equal(1, request.Page);
            Assert.Equal(50, request.PageSize);
            Assert.Equal("SKU", request.Search);
            Assert.Equal(warehouseId, request.WarehouseId);
            Assert.Equal(categoryId, request.CategoryId);
            Assert.Equal(new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc), request.AtDate);
            Assert.True(request.OnlyInStock);
            Assert.Equal(Mauw.Erp.Contracts.Enums.ValuationMethod.LastPurchaseCost, request.ValuationMethod);
        }
    }

    [Fact]
    public async Task MappingBothInventoryRoutePrefixes_DoesNotCreateDuplicateEndpointNames()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<IInventoryStockQueryService, InventoryStockQueryService>();
        builder.Services.AddScoped<ICompanySettingsService>(_ => throw new NotSupportedException());
        builder.Services.AddScoped<IInventoryStockPdfGenerator>(_ => throw new NotSupportedException());
        await using var app = builder.Build();

        app.MapInventoryStockEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
        var reconciliationEndpoints = endpoints
            .Where(endpoint => endpoint.RoutePattern.RawText?.EndsWith(
                "/stocks/reconciliation/ambiguous-movements",
                StringComparison.Ordinal) == true)
            .ToArray();
        Assert.Equal(2, reconciliationEndpoints.Length);

        var endpointNames = endpoints
            .Select(endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        Assert.Equal(endpointNames.Length, endpointNames.Distinct(StringComparer.Ordinal).Count());
    }
}
