using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Mauw.Erp.Api.Endpoints;
using Mauw.Erp.Api.Extensions;
using Mauw.Erp.Api.Services;
using Mauw.Erp.Application.Extensions;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.Branding;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Mauw.Erp.Infrastructure.Reporting;
using Mauw.Erp.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Mauw.Erp.UnitTests.Application;

/// <summary>
/// Hosts every API endpoint with the production services on a real PostgreSQL database and a test
/// authentication handler, so only the authorization outcome is under test.
/// </summary>
public sealed class AuthorizationMatrixHost : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private WebApplication? _app;

    public Uri Address { get; private set; } = null!;

    public IReadOnlyList<Endpoint> Endpoints =>
        _app!.Services.GetRequiredService<EndpointDataSource>().Endpoints.ToList();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Pooling = false }
            .ConnectionString;
        await using (var db = new MauwDbContext(new DbContextOptionsBuilder<MauwDbContext>()
                         .UseNpgsql(connectionString).Options))
            await db.Database.EnsureCreatedAsync();

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
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // No network or object storage from the matrix: replace the external collaborators.
        services.AddScoped(_ => Mock.Of<IGeocodingService>());
        services.AddScoped(_ => Mock.Of<IVatValidationService>());
        services.AddSingleton(Mock.Of<ISupplierDocumentStorage>());

        services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        services.AddTestRoleAuthorization();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapControllers();
        _app.MapProductEndpoints();
        _app.MapSupportEndpoints();
        _app.MapInventoryEndpoints();
        _app.MapInventoryStockEndpoints();
        _app.MapContactEndpoints();
        _app.MapWarehouseEndpoints();
        _app.MapCompanySettingsEndpoints();
        _app.MapCompanyLogoEndpoints();
        _app.MapLookupsEndpoints();
        _app.MapGeoEndpoints();
        _app.MapOrderEndpoints();
        _app.MapDashboardEndpoints();
        _app.MapInvoiceEndpoints();
        _app.MapDdtEndpoints();
        _app.MapCassaSessionEndpoints();
        _app.MapPriceListEndpoints();
        _app.MapPriceFormulaEndpoints();
        _app.MapSupplierDeliveryNoteEndpoints();
        _app.MapSupplierPurchaseOrderEndpoints();
        // Endpoint without any authorization metadata: it must fall under the fallback policy.
        _app.MapGet("/api/test/unmarked", () => Results.Ok());
        await _app.StartAsync();
        Address = new Uri(_app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single());
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

public class EndpointAuthorizationMatrixTests(AuthorizationMatrixHost host) : IClassFixture<AuthorizationMatrixHost>
{
    private const string Admin = "Admin";
    private const string Sales = "Sales";
    private const string Warehouse = "Warehouse";
    private const string Accounting = "Accounting";
    private const string Id = "6f1c1f0e-0000-4000-8000-000000000001";
    private const string Id2 = "6f1c1f0e-0000-4000-8000-000000000002";
    private static readonly string[] All = [Admin, Sales, Warehouse, Accounting];
    private static readonly string[] AdminOnly = [Admin];
    private static readonly string[] AdminWarehouse = [Admin, Warehouse];
    private static readonly string[] AdminSales = [Admin, Sales];
    private static readonly string[] Billing = [Admin, Accounting, Sales];
    private static readonly string[] AdminWarehouseSales = [Admin, Warehouse, Sales];

    public static TheoryData<string, string, string> Matrix()
    {
        var rows = new List<(string Method, string Path, string[] Roles)>();
        void Add(string method, string path, string[] roles) => rows.Add((method, path, roles));

        // Products and variants
        foreach (var read in new[]
                 {
                     "/api/v1/products", "/api/v1/products/variants/search?q=x", "/api/v1/products/barcode/X",
                     "/api/v1/products/search-parents?q=x", $"/api/v1/products/{Id}", $"/api/v1/products/{Id}/matrix",
                     $"/api/v1/products/{Id}/variants"
                 })
            Add("GET", read, All);
        Add("POST", "/api/v1/products", AdminWarehouse);
        Add("PUT", $"/api/v1/products/{Id}", AdminWarehouse);
        Add("DELETE", $"/api/v1/products/{Id}", AdminWarehouse);
        Add("POST", $"/api/v1/products/{Id}/variants", AdminWarehouse);
        Add("PATCH", $"/api/v1/products/{Id}/variants/{Id2}", AdminWarehouse);
        Add("DELETE", $"/api/v1/products/{Id}/variants/{Id2}", AdminWarehouse);
        Add("PUT", $"/api/v1/products/{Id}/variants/{Id2}/vetrina", AdminWarehouse);
        Add("DELETE", $"/api/v1/products/{Id}/variants/{Id2}/vetrina", AdminWarehouse);

        // Support tables
        foreach (var table in new[] { "colors", "sizes", "categories", "collections", "materials", "macro-categories" })
        {
            Add("GET", $"/api/v1/support/{table}", All);
            Add("GET", $"/api/v1/support/{table}/{Id}", All);
            Add("POST", $"/api/v1/support/{table}", AdminWarehouse);
            Add("PUT", $"/api/v1/support/{table}/{Id}", AdminWarehouse);
            Add("DELETE", $"/api/v1/support/{table}/{Id}", AdminWarehouse);
        }

        // Warehouses and lookups
        Add("GET", "/api/v1/warehouses", All);
        Add("GET", $"/api/v1/warehouses/{Id}", All);
        Add("POST", "/api/v1/warehouses", AdminWarehouse);
        Add("PUT", $"/api/v1/warehouses/{Id}", AdminWarehouse);
        Add("DELETE", $"/api/v1/warehouses/{Id}", AdminWarehouse);
        Add("PATCH", $"/api/v1/warehouses/{Id}/set-default", AdminWarehouse);
        Add("GET", "/api/v1/lookups/countries", All);
        Add("GET", "/api/v1/geo/autocomplete?input=via", All);
        Add("GET", "/api/v1/geo/place-details?placeId=x", All);

        // Inventory (movements, current stock)
        Add("GET", "/api/v1/inventory/stock", All);
        Add("GET", $"/api/v1/inventory/stock/{Id}/{Id2}", All);
        Add("GET", $"/api/v1/inventory/movements/{Id}", All);
        Add("GET", "/api/v1/inventory/movements-paged", All);
        Add("GET", $"/api/v1/inventory/barcode/X?warehouseId={Id}", All);
        Add("POST", "/api/v1/inventory/scan", AdminWarehouse);
        Add("POST", "/api/v1/inventory/movements", AdminWarehouseSales);
        Add("POST", "/api/v1/inventory/transfer", AdminWarehouse);
        foreach (var prefix in new[] { "/api/inventory", "/api/v1/inventory" })
        {
            Add("GET", $"{prefix}/stocks", All);
            Add("GET", $"{prefix}/stocks/summary", All);
            Add("GET", $"{prefix}/stocks/export?format=csv", All);
            Add("GET", $"{prefix}/stocks/export.csv", All);
            Add("GET", $"{prefix}/stocks/export.pdf", All);
            Add("GET", $"{prefix}/stocks/{Id}/{Id2}/movements", All);
            Add("GET", $"{prefix}/stocks/reconciliation/ambiguous-movements", AdminOnly);
        }

        // Contacts
        foreach (var read in new[]
                 {
                     "/api/v1/contacts", $"/api/v1/contacts/{Id}", $"/api/v1/contacts/{Id}/addresses",
                     "/api/v1/contacts/validate-vat?countryCode=IT&vatNumber=00000000000"
                 })
            Add("GET", read, All);
        Add("POST", "/api/v1/contacts", AdminSales);
        Add("PUT", $"/api/v1/contacts/{Id}", AdminSales);
        Add("DELETE", $"/api/v1/contacts/{Id}", AdminSales);
        Add("PATCH", $"/api/v1/contacts/{Id}/customer-block", AdminSales);
        Add("POST", $"/api/v1/contacts/{Id}/addresses", AdminSales);
        Add("PUT", $"/api/v1/contacts/{Id}/addresses/{Id2}", AdminSales);
        Add("DELETE", $"/api/v1/contacts/{Id}/addresses/{Id2}", AdminSales);
        Add("DELETE", $"/api/v1/contacts/{Id}/persons/{Id2}", AdminSales);
        Add("PUT", $"/api/v1/contacts/agents/{Id}/customers/{Id2}", AdminSales);
        Add("DELETE", $"/api/v1/contacts/agents/{Id}/customers/{Id2}", AdminSales);

        // Orders and cassa
        Add("GET", "/api/v1/orders", AdminSales);
        Add("GET", $"/api/v1/orders/{Id}", All);
        Add("GET", $"/api/v1/orders/{Id}/pdf", AdminSales);
        Add("POST", "/api/v1/orders", AdminSales);
        Add("POST", $"/api/v1/orders/{Id}/items", AdminSales);
        Add("DELETE", $"/api/v1/orders/{Id}/items/{Id2}", AdminSales);
        Add("POST", $"/api/v1/orders/{Id}/invoice-items", Billing);
        Add("DELETE", $"/api/v1/orders/{Id}/invoice-items/{Id2}", Billing);
        Add("PUT", $"/api/v1/orders/{Id}/invoice-shipping-cost", Billing);
        Add("POST", $"/api/v1/orders/{Id}/confirm", AdminSales);
        Add("POST", "/api/v1/cassa/sessions", AdminSales);
        Add("POST", "/api/v1/cassa/sessions/preview", AdminSales);
        Add("GET", "/api/v1/cassa/sessions/paused", AdminSales);
        Add("GET", $"/api/v1/cassa/sessions/{Id}", AdminSales);
        Add("PATCH", $"/api/v1/cassa/sessions/{Id}/pause", AdminSales);
        Add("PATCH", $"/api/v1/cassa/sessions/{Id}/resume", AdminSales);
        Add("PATCH", $"/api/v1/cassa/sessions/{Id}/terminate", AdminSales);

        // Invoices and credit notes; a single document is readable by every role (Warehouse for shipments)
        Add("GET", "/api/v1/invoices", Billing);
        Add("GET", $"/api/v1/invoices/{Id}", All);
        Add("GET", $"/api/v1/invoices/{Id}/pdf", Billing);
        Add("POST", $"/api/v1/invoices/from-order/{Id}", Billing);
        Add("POST", $"/api/v1/invoices/{Id}/payments", Billing);

        // DDT
        Add("GET", "/api/v1/ddts", AdminWarehouse);
        Add("GET", $"/api/v1/ddts/{Id}", All);
        Add("GET", $"/api/v1/ddts/{Id}/pdf", AdminWarehouseSales);
        Add("POST", "/api/v1/ddts", AdminWarehouseSales);
        Add("POST", $"/api/v1/ddts/{Id}/items", AdminWarehouseSales);
        Add("DELETE", $"/api/v1/ddts/{Id}", AdminWarehouse);

        // Company settings, dashboard, price lists
        Add("GET", "/api/v1/settings/company", All);
        Add("PUT", "/api/v1/settings/company", AdminOnly);
        Add("POST", "/api/v1/settings/company/operational-sites", AdminOnly);
        Add("PUT", $"/api/v1/settings/company/operational-sites/{Id}", AdminOnly);
        Add("DELETE", $"/api/v1/settings/company/operational-sites/{Id}", AdminOnly);
        Add("PUT", "/api/v1/settings/company/logos/horizontal", AdminOnly);
        Add("GET", "/api/v1/dashboard", AdminOnly);
        Add("GET", "/api/v1/price-lists", All);
        Add("GET", $"/api/v1/price-lists/{Id}", All);
        Add("POST", "/api/v1/price-lists/resolve", All);
        Add("POST", "/api/v1/price-lists", AdminSales);
        Add("PUT", $"/api/v1/price-lists/{Id}", AdminSales);
        Add("PATCH", $"/api/v1/price-lists/{Id}/activation", AdminSales);
        Add("DELETE", $"/api/v1/price-lists/{Id}", AdminSales);

        // Immagini e catalogo mobile: lettura a ogni ruolo, scrittura immagini solo Admin e Warehouse
        Add("GET", "/api/catalog/resolve-sku/SKU", All);
        Add("GET", $"/api/images/{Id}", All);
        Add("PUT", $"/api/images/{Id}/{Id2}/category", AdminWarehouse);
        Add("DELETE", $"/api/images/{Id}/{Id2}", AdminWarehouse);

        // Formula prezzi: solo Admin
        Add("GET", "/api/v1/settings/price-formula", AdminOnly);
        Add("PUT", "/api/v1/settings/price-formula", AdminOnly);
        Add("POST", "/api/v1/products/price-formula/calculate", AdminOnly);

        // Supplier documents
        foreach (var prefix in new[] { "/api/supplier-delivery-notes", "/api/v1/supplier-delivery-notes" })
        {
            Add("GET", prefix, AdminWarehouse);
            Add("GET", $"{prefix}/{Id}", AdminWarehouse);
            Add("PUT", $"{prefix}/{Id}/lines", AdminWarehouse);
            Add("POST", $"{prefix}/{Id}/confirm", AdminWarehouse);
        }
        Add("GET", "/api/supplier-purchase-orders", AdminWarehouse);
        Add("GET", $"/api/supplier-purchase-orders/{Id}", AdminWarehouse);
        Add("POST", "/api/supplier-purchase-orders", AdminWarehouse);
        Add("POST", $"/api/supplier-purchase-orders/{Id}/confirm", AdminWarehouse);

        var data = new TheoryData<string, string, string>();
        foreach (var (method, path, roles) in rows)
            data.Add(method, path, string.Join(',', roles));
        return data;
    }

    public static TheoryData<string> HistoricalStockPaths()
    {
        var data = new TheoryData<string>();
        foreach (var prefix in new[] { "/api/inventory", "/api/v1/inventory" })
        {
            data.Add($"{prefix}/stocks");
            data.Add($"{prefix}/stocks/summary");
            data.Add($"{prefix}/stocks/export?format=csv");
            data.Add($"{prefix}/stocks/export.csv");
            data.Add($"{prefix}/stocks/export.pdf");
            data.Add($"{prefix}/stocks/{Id}/{Id2}/movements");
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task EndpointAllowsOnlyTheDeclaredRoles(string method, string path, string allowedRoles)
    {
        var allowed = allowedRoles.Split(',');

        Assert.Equal(HttpStatusCode.Unauthorized, await SendAsync(null, method, path));
        foreach (var role in All)
        {
            var status = await SendAsync(role, method, path);
            if (allowed.Contains(role))
            {
                Assert.True(status is not HttpStatusCode.Unauthorized and not HttpStatusCode.Forbidden,
                    $"{role} {method} {path} returned {(int)status}");
            }
            else
                Assert.True(status == HttpStatusCode.Forbidden, $"{role} {method} {path} returned {(int)status}");
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task RemovedCashierRoleHasNoAccess(string method, string path, string allowedRoles)
    {
        _ = allowedRoles;
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync("Cashier", method, path));
    }

    [Theory]
    [MemberData(nameof(HistoricalStockPaths))]
    public async Task HistoricalStockRequiresAdminOrAccounting(string path)
    {
        var historical = $"{path}{(path.Contains('?') ? '&' : '?')}atDate=2026-01-31T00:00:00Z";

        Assert.Equal(HttpStatusCode.Unauthorized, await SendAsync(null, "GET", historical));
        foreach (var role in new[] { Warehouse, Sales })
        {
            Assert.Equal(HttpStatusCode.Forbidden, await SendAsync(role, "GET", historical));
            Assert.Equal(HttpStatusCode.Forbidden,
                await SendAsync(role, "GET", historical.Replace("atDate=", "ATDATE=", StringComparison.Ordinal)));
        }
        foreach (var role in new[] { Admin, Accounting })
        {
            var status = await SendAsync(role, "GET", historical);
            Assert.True(status is not HttpStatusCode.Unauthorized and not HttpStatusCode.Forbidden,
                $"{role} {historical} returned {(int)status}");
        }
        foreach (var role in All)
        {
            var current = await SendAsync(role, "GET", path);
            Assert.True(current is not HttpStatusCode.Unauthorized and not HttpStatusCode.Forbidden,
                $"{role} {path} returned {(int)current}");
            var emptyAtDate = await SendAsync(role, "GET", $"{path}{(path.Contains('?') ? '&' : '?')}atDate=");
            Assert.True(emptyAtDate is not HttpStatusCode.Unauthorized and not HttpStatusCode.Forbidden,
                $"{role} {path} with empty atDate returned {(int)emptyAtDate}");
        }
    }

    [Fact]
    public async Task PublicEndpointsRemainReachableWithoutAuthentication()
    {
        using var anonymous = host.CreateClient(null);
        var user = await anonymous.GetAsync("/api/auth/user");
        Assert.Equal(HttpStatusCode.OK, user.StatusCode);
        Assert.Contains("\"isAuthenticated\":false", await user.Content.ReadAsStringAsync());

        var logo = await anonymous.GetAsync("/api/v1/settings/company/logos/horizontal");
        Assert.True(logo.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound, logo.StatusCode.ToString());
    }

    [Fact]
    public async Task EndpointWithoutAuthorizationMetadataRequiresAuthentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await SendAsync(null, "GET", "/api/test/unmarked"));
        Assert.Equal(HttpStatusCode.OK, await SendAsync(Sales, "GET", "/api/test/unmarked"));
    }

    [Fact]
    public async Task InvoiceListReturnsPagedResults()
    {
        using var client = host.CreateClient(Accounting);
        using var response = await client.GetAsync("/api/v1/invoices?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.True(root.GetProperty("items").ValueKind == JsonValueKind.Array);
        Assert.Equal(1, root.GetProperty("pageNumber").GetInt32());
        Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
        Assert.True(root.GetProperty("totalCount").GetInt32() >= 0);
    }

    [Theory]
    [InlineData("GET", "/api/catalog/resolve-sku/SKU")]
    [InlineData("GET", "/api/images/6f1c1f0e-0000-4000-8000-000000000001")]
    public async Task AuthenticatedOnlyControllersRejectAnonymousCallers(string method, string path)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await SendAsync(null, method, path));
        foreach (var role in All)
        {
            var status = await SendAsync(role, method, path);
            Assert.True(status is not HttpStatusCode.Unauthorized and not HttpStatusCode.Forbidden,
                $"{role} {method} {path} returned {(int)status}");
        }
    }

    [Theory]
    [InlineData(Admin, true)]
    [InlineData(Warehouse, true)]
    [InlineData(Sales, false)]
    [InlineData(Accounting, false)]
    [InlineData("Cashier", false)]
    [InlineData(null, false)]
    public async Task ImageUploadRequiresCatalogManagementRoles(string? role, bool allowed)
    {
        using var client = host.CreateClient(role);
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent([1, 2, 3]), "file", "photo.jpg" }
        };

        using var response = await client.PostAsync($"/api/images/{Id}", form);

        if (allowed)
            Assert.True(response.StatusCode is not HttpStatusCode.Unauthorized and not HttpStatusCode.Forbidden,
                $"{role} returned {(int)response.StatusCode}");
        else
            Assert.Equal(role is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void OnlyTheDocumentedEndpointsAreAnonymousAndEveryOtherEndpointDeclaresAnAuthorizationRule()
    {
        var endpoints = host.Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText != "/api/test/unmarked")
            .ToList();
        Assert.NotEmpty(endpoints);

        var anonymous = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText!.TrimStart('/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] expected =
        [
            "api/auth/login", "api/auth/logout", "api/auth/user",
            "api/v1/settings/company/logos/{name:regex(^(horizontal|compact)$)}"
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal).ToArray(), anonymous);

        var undeclared = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null
                && endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();
        Assert.Empty(undeclared);
    }

    private async Task<HttpStatusCode> SendAsync(string? role, string method, string path)
    {
        using var client = host.CreateClient(role);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT" or "PATCH")
            request.Content = new StringContent("{}", new MediaTypeHeaderValue("application/json"));
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }
}
