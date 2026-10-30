using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mauw.Erp.Api.Endpoints;
using Mauw.Erp.Api.Extensions;
using Mauw.Erp.Application.Extensions;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.Branding;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Mauw.Erp.Infrastructure.Reporting;
using Mauw.Erp.Infrastructure.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;
using UglyToad.PdfPig;

namespace Mauw.Erp.UnitTests.Application;

public sealed class CostVisibilityHost : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private WebApplication? _app;

    public Uri Address { get; private set; } = null!;
    public Guid WarehouseId { get; private set; }
    public Guid AlfaProductId { get; private set; }
    public Guid AlfaVariantId { get; private set; }
    public Guid BetaVariantId { get; private set; }
    public Guid GammaProductId { get; private set; }
    public Guid GammaVariantId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid DeliveryNoteId { get; private set; }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Pooling = false }
            .ConnectionString;
        await using (var db = new MauwDbContext(new DbContextOptionsBuilder<MauwDbContext>()
                         .UseNpgsql(connectionString).Options))
        {
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(db);
        }

        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        var services = builder.Services;
        services.AddDbContext<MauwDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
        services.AddScoped<IValidator<CreateContactRequest>, CreateContactRequestValidator>();
        services.AddScoped<IValidator<UpdateContactRequest>, UpdateContactRequestValidator>();
        services.AddScoped<IValidator<CreateOrderRequest>, CreateOrderRequestValidator>();
        services.AddScoped<IValidator<CreateOrderItemRequest>, CreateOrderItemRequestValidator>();
        services.AddScoped<IValidator<BarcodeScanRequest>, BarcodeScanRequestValidator>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddPricingApi();
        services.AddInvoicePdfReporting();
        services.AddInventoryStockReporting();
        services.AddCompanyBranding();
        services.AddImageStorage(builder.Configuration);
        services.AddSupplierDeliveryNotes();
        services.AddApplicationServices();
        services.AddMemoryCache();
        services.AddScoped(_ => Mock.Of<IGeocodingService>());
        services.AddScoped(_ => Mock.Of<IVatValidationService>());
        services.AddSingleton(Mock.Of<ISupplierDocumentStorage>());

        var company = new Mock<ICompanySettingsService>();
        company.Setup(service => service.GetCompanyAsync()).ReturnsAsync(OperationResult<CompanySettingsDto>.Ok(
            new CompanySettingsDto(
                Guid.NewGuid(), "Mauw Test S.r.l.", "12345678901", null, null, null, "IT", null, null, null,
                null, null, false, null, null, [])));
        services.AddScoped(_ => company.Object);

        // ICurrentUserService and ICostVisibility come from AddAppAuthorization, as in production.
        services.AddTestRoleAuthorization();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapProductEndpoints();
        _app.MapInventoryEndpoints();
        _app.MapInventoryStockEndpoints();
        _app.MapSupplierDeliveryNoteEndpoints();
        await _app.StartAsync();
        Address = new Uri(_app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
    }

    private async Task SeedAsync(MauwDbContext db)
    {
        var supplier = new Contact("Fornitore costi");
        supplier.EnableAsSupplier();
        db.Add(supplier);
        await db.SaveChangesAsync();
        var address = new ContactAddress(supplier.Id, AddressPurpose.LegalHeadquarters, "Via Test 1", "20100", "Milano");
        db.Add(address);
        await db.SaveChangesAsync();
        var warehouse = new Warehouse("COST-1", "Deposito costi", WarehouseType.Standard, address.Id, true, true);
        var alfa = new Product(supplier.Id, "SA", "PB-A", "Prodotto Alfa", 22m, 12.34m);
        var beta = new Product(supplier.Id, "SB", "PB-B", "Prodotto Beta", 22m, 5m);
        var gamma = new Product(supplier.Id, "SG", "PB-G", "Prodotto Gamma", 22m, 21.43m);
        db.AddRange(warehouse, alfa, beta, gamma);
        await db.SaveChangesAsync();

        var alfa1 = new ProductVariant(alfa.Id, "BAR-A1", 12.34m, supplier.Id) { SellingPrice = 30.5m };
        var alfa2 = new ProductVariant(alfa.Id, "BAR-A2", 12.34m, supplier.Id) { SellingPrice = 30.5m };
        var beta1 = new ProductVariant(beta.Id, "BAR-B1", 5m, supplier.Id) { SellingPrice = 11.5m };
        var gamma1 = new ProductVariant(gamma.Id, "BAR-G1", 21.43m, supplier.Id) { SellingPrice = 47.5m };
        db.AddRange(alfa1, alfa2, beta1, gamma1);
        await db.SaveChangesAsync();

        var alfaStock = new StockItem(warehouse.Id, alfa1.Id, unitCost: 12.34m);
        alfaStock.ApplyMovement(10m);
        var betaStock = new StockItem(warehouse.Id, beta1.Id, unitCost: 5m);
        betaStock.ApplyMovement(4m);
        var alfaReceipt = new StockMovement(alfa1.Id, warehouse.Id, StockMovementReason.SupplierReceipt, 10m);
        var betaReceipt = new StockMovement(beta1.Id, warehouse.Id, StockMovementReason.SupplierReceipt, 4m);
        db.AddRange(alfaStock, betaStock, alfaReceipt, betaReceipt);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE stock_movements SET unit_cost = {12.34m} WHERE id = {alfaReceipt.Id}");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE stock_movements SET unit_cost = {5m} WHERE id = {betaReceipt.Id}");

        var note = new SupplierDeliveryNote
        {
            SupplierId = supplier.Id, WarehouseId = warehouse.Id, DocumentNumber = "DDT-COSTI",
            DocumentDate = DateTime.UtcNow, FilePath = "note.pdf", ContentType = "application/pdf",
            Status = SupplierDeliveryNoteStatus.Parsed,
            Lines =
            [
                new SupplierDeliveryNoteLine
                {
                    Position = 1, SupplierItemCode = "SA", ArticleVariantId = alfa1.Id, Description = "Giacca",
                    Quantity = 3, UnitPrice = 12.34m, LineStatus = SupplierDeliveryNoteLineStatus.Matched
                }
            ]
        };
        db.Add(note);
        await db.SaveChangesAsync();

        SupplierId = supplier.Id;
        WarehouseId = warehouse.Id;
        AlfaProductId = alfa.Id;
        AlfaVariantId = alfa1.Id;
        BetaVariantId = beta1.Id;
        GammaProductId = gamma.Id;
        GammaVariantId = gamma1.Id;
        DeliveryNoteId = note.Id;
    }

    public HttpClient CreateClient(string? role) => TestRoleAuthentication.CreateTestClient(Address, role);

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
        await _container.DisposeAsync();
    }
}

public sealed class CostVisibilityEndpointTests(CostVisibilityHost host) : IClassFixture<CostVisibilityHost>
{
    public static TheoryData<string> CostRoles => new() { "Admin", "Accounting" };
    public static TheoryData<string> NoCostRoles => new() { "Sales", "Warehouse" };
    public static TheoryData<string> AllRoles => new() { "Admin", "Accounting", "Sales", "Warehouse" };

    // Seeded purchase costs and valuations, never allowed to appear in a body returned to Sales or Warehouse.
    // Not preceded by digits, a dot or "dd:" (the seconds of a timestamp).
    private static readonly Regex SeededCosts = new(@"(?<!\d\d:)(?<![\d.])(12\.34|21\.43|123\.4|143\.4)0*(?!\d)", RegexOptions.Compiled);

    // The stocks and export endpoints require every non-nullable filter member in the query string.
    private string StockQuery(string extra = "", bool onlyInStock = false) =>
        $"warehouseId={host.WarehouseId}&page=1&pageSize=50&sortDescending=false&onlyInStock={onlyInStock.ToString().ToLowerInvariant()}" +
        $"&valuationMethod=WeightedAverageCost{extra}";

    private static bool HasCostValue(string body) => SeededCosts.IsMatch(body);

    private static decimal? Num(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : null;

    private static void AssertCost(decimal expected, decimal? actual, bool visible)
    {
        if (visible)
            Assert.Equal(expected, actual);
        else
            Assert.Null(actual);
    }

    private async Task<(JsonElement Json, string Body)> GetAsync(string role, string url)
    {
        using var client = host.CreateClient(role);
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{role} {url}: {(int)response.StatusCode} {body}");
        return (JsonDocument.Parse(body).RootElement.Clone(), body);
    }

    private static JsonElement Single(JsonElement items, string property, string value) =>
        items.EnumerateArray().Single(item => item.GetProperty(property).GetString() == value);

    [Theory]
    [MemberData(nameof(AllRoles))]
    public async Task ProductList_AndDetail_ExposeCostsOnlyToCostRoles(string role)
    {
        var visible = role is "Admin" or "Accounting";
        var (list, listBody) = await GetAsync(role, "/api/v1/products?search=Prodotto&pageSize=50");
        var alfa = Single(list.GetProperty("items"), "name", "Prodotto Alfa");
        AssertCost(12.34m, Num(alfa, "basePurchaseCost"), visible);
        Assert.All(alfa.GetProperty("variants").EnumerateArray(), variant => AssertCost(12.34m, Num(variant, "purchaseCost"), visible));
        Assert.Equal(visible, HasCostValue(listBody));

        var (detail, detailBody) = await GetAsync(role, $"/api/v1/products/{host.AlfaProductId}");
        AssertCost(12.34m, Num(detail, "basePurchaseCost"), visible);
        Assert.Equal(visible, HasCostValue(detailBody));

        var (variants, variantsBody) = await GetAsync(role, $"/api/v1/products/{host.AlfaProductId}/variants");
        Assert.Equal(2, variants.GetArrayLength());
        Assert.All(variants.EnumerateArray(), variant => AssertCost(12.34m, Num(variant, "purchaseCost"), visible));
        Assert.Equal(visible, HasCostValue(variantsBody));
    }

    [Theory]
    [MemberData(nameof(AllRoles))]
    public async Task VariantSearch_AndCassaLookups_ExposeCostsOnlyToCostRoles(string role)
    {
        var visible = role is "Admin" or "Accounting";
        var (search, searchBody) = await GetAsync(role, "/api/v1/products/variants/search?q=BAR-A1");
        AssertCost(12.34m, Num(search[0], "purchaseCost"), visible);
        Assert.Equal(visible, HasCostValue(searchBody));

        var (barcode, barcodeBody) = await GetAsync(role, "/api/v1/products/barcode/BAR-A1");
        Assert.Equal(30.5m, Num(barcode, "unitNetPrice"));
        Assert.False(HasCostValue(barcodeBody));

        var (matrix, matrixBody) = await GetAsync(role, $"/api/v1/products/{host.AlfaProductId}/matrix?warehouseId={host.WarehouseId}");
        Assert.True(matrix.GetProperty("cells").GetArrayLength() > 0);
        Assert.False(HasCostValue(matrixBody));
        Assert.DoesNotContain("purchaseCost", matrixBody, StringComparison.OrdinalIgnoreCase);

        var (withStock, withStockBody) = await GetAsync(role, $"/api/v1/inventory/barcode/BAR-A1?warehouseId={host.WarehouseId}");
        Assert.Equal(30.5m, Num(withStock, "unitNetPrice"));
        Assert.False(HasCostValue(withStockBody));
    }

    [Fact]
    public async Task AvailableOnlyVariantSearchUsesDefaultSellableWarehouseAndExcludesOutOfStock()
    {
        using var client = host.CreateClient("Sales");

        var response = await client.GetAsync("/api/v1/products/variants/search?q=BAR&availableOnly=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var variants = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = variants.EnumerateArray()
            .Select(variant => variant.GetProperty("variantId").GetGuid())
            .Order()
            .ToArray();
        Assert.Equal(new[] { host.AlfaVariantId, host.BetaVariantId }.Order().ToArray(), ids);
        Assert.All(variants.EnumerateArray(), variant =>
            Assert.True(variant.GetProperty("currentStock").GetDecimal() > 0m));
    }

    [Theory]
    [MemberData(nameof(AllRoles))]
    public async Task ProductList_CostSortKeys_AreIgnoredWithoutCostVisibility(string role)
    {
        var visible = role is "Admin" or "Accounting";
        var (list, _) = await GetAsync(role, "/api/v1/products?search=Prodotto&pageSize=50&sortBy=purchaseCost&sortDescending=false");
        var names = list.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToArray();
        string[] byCost = ["Prodotto Beta", "Prodotto Alfa", "Prodotto Gamma"];
        string[] byName = ["Prodotto Alfa", "Prodotto Beta", "Prodotto Gamma"];
        Assert.Equal(visible ? byCost : byName, names);

        var (descending, _) = await GetAsync(role, "/api/v1/products?search=Prodotto&pageSize=50&sortBy=PurchaseCost&sortDescending=true");
        names = descending.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()).ToArray();
        Assert.Equal(visible ? byCost.Reverse().ToArray() : byName, names);
    }

    [Theory]
    [MemberData(nameof(AllRoles))]
    public async Task InventoryStocks_ExposeValuationOnlyToCostRoles(string role)
    {
        var visible = role is "Admin" or "Accounting";
        foreach (var prefix in new[] { "/api/inventory", "/api/v1/inventory" })
        {
            var (page, pageBody) = await GetAsync(role, $"{prefix}/stocks?{StockQuery()}");
            var alfa = Single(page.GetProperty("items"), "barcode", "BAR-A1");
            Assert.Equal(10m, Num(alfa, "quantityOnHand"));
            AssertCost(12.34m, Num(alfa, "unitCost"), visible);
            AssertCost(123.40m, Num(alfa, "totalValue"), visible);
            if (visible)
                Assert.Equal("StockMovements", alfa.GetProperty("costSource").GetString());
            else
                Assert.Equal(JsonValueKind.Null, alfa.GetProperty("costSource").ValueKind);
            Assert.Equal(visible, HasCostValue(pageBody));

            var (summary, summaryBody) = await GetAsync(role, $"{prefix}/stocks/summary?warehouseId={host.WarehouseId}");
            var totals = summary.GetProperty("totals");
            Assert.Equal(14m, Num(totals, "totalPhysicalQuantity"));
            AssertCost(143.40m, Num(totals, "totalValue"), visible);
            Assert.Equal(visible, HasCostValue(summaryBody));
        }
    }

    [Theory]
    [MemberData(nameof(AllRoles))]
    public async Task InventoryStocks_CostSortKeys_AreIgnoredWithoutCostVisibility(string role)
    {
        var visible = role is "Admin" or "Accounting";
        foreach (var key in new[] { "unitCost", "totalValue", "estimatedValue" })
        {
            var (page, _) = await GetAsync(role, $"/api/v1/inventory/stocks?{StockQuery($"&sortBy={key}", onlyInStock: true)}");
            var barcodes = page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("barcode").GetString()).ToArray();
            Assert.Equal(visible ? ["BAR-B1", "BAR-A1"] : ["BAR-A1", "BAR-B1"], barcodes);
        }
    }

    [Theory]
    [MemberData(nameof(AllRoles))]
    public async Task InventoryMovements_ExposeUnitCostOnlyToCostRoles(string role)
    {
        var visible = role is "Admin" or "Accounting";
        var (movements, body) = await GetAsync(role, $"/api/v1/inventory/stocks/{host.WarehouseId}/{host.AlfaVariantId}/movements");
        var item = movements.GetProperty("items")[0];
        Assert.Equal(10m, Num(item, "quantity"));
        AssertCost(12.34m, Num(item, "unitCost"), visible);
        Assert.Equal(visible, HasCostValue(body));
    }

    [Theory]
    [MemberData(nameof(AllRoles))]
    public async Task LegacyInventoryEndpoints_ExposeCostsOnlyToCostRoles(string role)
    {
        var visible = role is "Admin" or "Accounting";
        var (stock, stockBody) = await GetAsync(role, $"/api/v1/inventory/stock?warehouseId={host.WarehouseId}&pageSize=50");
        var alfa = Single(stock.GetProperty("items"), "barcode", "BAR-A1");
        Assert.Equal(10m, Num(alfa, "quantityOnHand"));
        AssertCost(12.34m, Num(alfa, "unitCost"), visible);
        AssertCost(123.40m, Num(alfa, "estimatedValue"), visible);
        Assert.Equal(visible, HasCostValue(stockBody));

        var (movements, movementsBody) = await GetAsync(role, $"/api/v1/inventory/movements/{host.AlfaVariantId}");
        Assert.True(movements.GetArrayLength() > 0);
        Assert.All(movements.EnumerateArray(), movement => AssertCost(12.34m, Num(movement, "unitCost"), visible));
        Assert.Equal(visible, HasCostValue(movementsBody));

        foreach (var key in new[] { "unitCost", "estimatedValue" })
        {
            var (sorted, _) = await GetAsync(role, $"/api/v1/inventory/stock?warehouseId={host.WarehouseId}&pageSize=50&sortBy={key}");
            var barcodes = sorted.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("barcode").GetString()).ToArray();
            Assert.Equal(visible ? ["BAR-B1", "BAR-A1"] : ["BAR-A1", "BAR-B1"], barcodes);
        }
    }

    [Theory]
    [MemberData(nameof(AllRoles))]
    public async Task InventoryCsvExport_OmitsCostColumnsAndTotalsWithoutCostVisibility(string role)
    {
        var visible = role is "Admin" or "Accounting";
        foreach (var url in new[]
                 {
                     $"/api/v1/inventory/stocks/export.csv?{StockQuery("&sortBy=totalValue")}",
                     $"/api/inventory/stocks/export?format=csv&{StockQuery()}"
                 })
        {
            using var client = host.CreateClient(role);
            var response = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
            var csv = (await response.Content.ReadAsStringAsync()).TrimStart('\uFEFF');
            var lines = csv.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
            var header = lines.Single(line => line.StartsWith("SKU;", StringComparison.Ordinal));
            var rows = lines.SkipWhile(line => line != header).Skip(1).ToArray();
            Assert.Equal(visible, header.Contains("Valore totale", StringComparison.Ordinal));
            Assert.Equal(visible, HasCostValue(csv));
            Assert.Equal(visible ? 9 : 7, header.Split(';').Length);
            Assert.All(rows.Where(row => !row.StartsWith("TOTALI", StringComparison.Ordinal)),
                row => Assert.Equal(visible ? 9 : 7, row.Split(';').Length));
            var totalRow = rows.Single(row => row.StartsWith("TOTALI", StringComparison.Ordinal));
            Assert.EndsWith(visible ? ";;;143.40" : ";14", totalRow, StringComparison.Ordinal);
            Assert.Contains("Prodotto Alfa", csv, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(AllRoles))]
    public async Task InventoryPdfExport_OmitsCostColumnsAndTotalsWithoutCostVisibility(string role)
    {
        var visible = role is "Admin" or "Accounting";
        using var client = host.CreateClient(role);
        var response = await client.GetAsync($"/api/v1/inventory/stocks/export.pdf?{StockQuery()}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        using var pdf = PdfDocument.Open(await response.Content.ReadAsByteArrayAsync());
        var text = string.Concat(pdf.GetPages().Select(page => page.Text)).Replace(" ", string.Empty);
        Assert.Contains("ProdottoAlfa", text, StringComparison.Ordinal);
        Assert.Equal(visible, text.Contains("Valoretotale", StringComparison.Ordinal));
        Assert.Equal(visible, text.Contains("Criteriodivalorizzazione", StringComparison.Ordinal));
        Assert.Equal(visible, text.Contains("12,34", StringComparison.Ordinal));
        Assert.Equal(visible, text.Contains("143,40", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Warehouse")]
    public async Task SupplierDeliveryNoteLines_KeepTheirUnitPriceForWarehouseAndAdmin(string role)
    {
        var (note, _) = await GetAsync(role, $"/api/v1/supplier-delivery-notes/{host.DeliveryNoteId}");
        var line = note.GetProperty("lines")[0];
        Assert.Equal(12.34m, decimal.Parse(line.GetProperty("unitPrice").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("Sales")]
    [InlineData("Accounting")]
    public async Task SupplierDeliveryNotes_RemainForbiddenToOtherRoles(string role)
    {
        using var client = host.CreateClient(role);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync($"/api/v1/supplier-delivery-notes/{host.DeliveryNoteId}")).StatusCode);
    }

    [Fact]
    public async Task ProductUpdatesByWarehouse_DoNotOverwriteTheHiddenPurchaseCost()
    {
        using var warehouse = host.CreateClient("Warehouse");
        var variantUpdate = await warehouse.PatchAsJsonAsync(
            $"/api/v1/products/{host.GammaProductId}/variants/{host.GammaVariantId}",
            new { barcode = "BAR-G1", purchaseCost = 0m, sellingPrice = 47.5m });
        var variantBody = await variantUpdate.Content.ReadAsStringAsync();
        Assert.True(variantUpdate.StatusCode == HttpStatusCode.OK, variantBody);
        Assert.False(HasCostValue(variantBody));
        Assert.Null(Num(JsonDocument.Parse(variantBody).RootElement, "purchaseCost"));

        var productUpdate = await warehouse.PutAsJsonAsync($"/api/v1/products/{host.GammaProductId}", new
        {
            supplierId = host.SupplierId, supplierCode = "SG", barcode = "PB-G", name = "Prodotto Gamma",
            sellingPrice = 47.5m, vatRate = 22m, basePurchaseCost = 0m, hasVariants = true
        });
        var productBody = await productUpdate.Content.ReadAsStringAsync();
        Assert.True(productUpdate.StatusCode == HttpStatusCode.OK, productBody);
        Assert.False(HasCostValue(productBody));

        using var admin = host.CreateClient("Admin");
        var stored = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/products/{host.GammaProductId}");
        Assert.Equal(21.43m, Num(stored, "basePurchaseCost"));
        Assert.Equal(21.43m, Num(stored.GetProperty("variants")[0], "purchaseCost"));
    }
}
