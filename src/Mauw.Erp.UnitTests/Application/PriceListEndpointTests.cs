using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mauw.Erp.Api.Endpoints;
using Mauw.Erp.Api.Extensions;
using Mauw.Erp.Application.Extensions;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class PriceListEndpointTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task VariantLookupAndRuleSelectionDoNotRequireAValidCatalogPrice()
    {
        await using var fixture = await Fixture.CreateAsync(postgres, invalidCatalogPrice: true);
        using var client = fixture.CreateClient("Sales");
        var response = await client.GetAsync("/api/v1/products/variants/search?q=SKU");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var variants = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(fixture.VariantId, variants[0].GetProperty("variantId").GetGuid());
        Assert.Equal("SKU", variants[0].GetProperty("barcode").GetString());
        var created = await client.PostAsJsonAsync("/api/v1/price-lists", new
        {
            name = "Listino SKU", currency = "EUR", isActive = true,
            rules = new[] { new { calculationType = "FixedPrice", value = 15m, variantId = fixture.VariantId } }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(fixture.VariantId, body.GetProperty("rules")[0].GetProperty("variantId").GetGuid());
    }

    [Fact]
    public async Task ProductsOnlySaveTheFinalPriceAndTheRemovedMarkupEndpointIsGone()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var client = fixture.CreateClient("Sales");
        using var warehouse = fixture.CreateClient("Warehouse");
        var removed = await client.PostAsJsonAsync("/api/v1/products/calculate-selling-price",
            new { purchaseCost = 10m, markupRate = 10m });
        Assert.Contains(removed.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/products", new { })).StatusCode);
        var created = await warehouse.PostAsJsonAsync("/api/v1/products", new
        {
            supplierId = Guid.NewGuid(), supplierCode = "SUP", barcode = "NEW",
            name = "Nuovo", sellingPrice = 18.12345678m, vatRate = 22m,
            basePurchaseCost = 10m, hasVariants = false
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(18.12345678m, body.GetProperty("sellingPrice").GetDecimal());
        Assert.Equal(18.12345678m, body.GetProperty("variants")[0].GetProperty("sellingPrice").GetDecimal());
        Assert.False(body.TryGetProperty("defaultMarkupRate", out _));
        Assert.False(body.TryGetProperty("fixedSellingPrice", out _));
        Assert.False(body.GetProperty("variants")[0].TryGetProperty("markupRate", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogEndpointsPreserveFixedPricesAndExplicitlyReportInvalidStoredPricing(bool invalidCatalogPrice)
    {
        await using var fixture = await Fixture.CreateAsync(postgres, invalidCatalogPrice);
        using var client = fixture.CreateClient("Sales");
        var barcode = await client.GetAsync("/api/v1/products/barcode/SKU");
        var expected = invalidCatalogPrice ? HttpStatusCode.BadRequest : HttpStatusCode.OK;
        Assert.Equal(expected, barcode.StatusCode);
        var products = await client.GetAsync("/api/v1/products");
        Assert.Equal(expected, products.StatusCode);
        var variants = await client.GetAsync($"/api/v1/products/{fixture.ProductId}/variants");
        Assert.Equal(expected, variants.StatusCode);
        Assert.Equal(expected, (await client.GetAsync($"/api/v1/products/{fixture.ProductId}")).StatusCode);
        Assert.Equal(expected, (await client.GetAsync($"/api/v1/products/{fixture.ProductId}/matrix")).StatusCode);
        if (invalidCatalogPrice)
            Assert.Contains("prezzo di vendita", await barcode.Content.ReadAsStringAsync());
        else
        {
            var price = await barcode.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(17.12345678m, price.GetProperty("unitNetPrice").GetDecimal());
        }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/products/barcode/MISSING")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/products/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task HttpCrudAndBatchResolutionReturnDocumentedShapesAndStatuses()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var client = fixture.CreateClient("Sales");
        var created = await client.PostAsJsonAsync("/api/v1/price-lists", new
        {
            name = "Ingrosso", currency = "EUR", isActive = true,
            rules = new[] { new { calculationType = "PercentageDiscount", value = 10m, minimumQuantity = 1m, priority = 10 } }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var location = created.Headers.Location;
        Assert.NotNull(location);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PercentageDiscount", body.GetProperty("rules")[0].GetProperty("calculationType").GetString());
        var id = body.GetProperty("id").GetGuid();
        Assert.EndsWith(id.ToString(), location.ToString(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(location)).StatusCode);
        var updated = await client.PutAsJsonAsync(location, new
        {
            name = "Aggiornato", currency = "EUR", isActive = true,
            rules = new[] { new { calculationType = "FixedPrice", value = 12.12345678m, minimumQuantity = 1m, priority = 10 } }
        });
        Assert.True(updated.StatusCode == HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        var resolved = await client.PostAsJsonAsync("/api/v1/price-lists/resolve", new
        {
            priceListId = id, at = DateTimeOffset.UtcNow,
            lines = new[] { new { variantId = fixture.VariantId, quantity = 5m } }
        });
        Assert.Equal(HttpStatusCode.OK, resolved.StatusCode);
        var quote = await resolved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, quote.GetProperty("priceListId").GetGuid());
        Assert.Equal(12.12345678m, quote.GetProperty("lines")[0].GetProperty("agreedUnitNetPrice").GetDecimal());
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync(
            $"{location}/activation", new { isActive = false })).StatusCode);
        var inactive = await client.PostAsJsonAsync("/api/v1/price-lists/resolve", new
        {
            priceListId = id, at = DateTimeOffset.UtcNow,
            lines = new[] { new { variantId = fixture.VariantId, quantity = 5m } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, inactive.StatusCode);
        Assert.Equal("application/problem+json", inactive.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(location)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(location)).StatusCode);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("Warehouse", 403)]
    [InlineData("Accounting", 403)]
    public async Task ConfigurationRequiresAuthorizedSalesRole(string? role, int status)
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var client = fixture.CreateClient(role);
        var response = await client.PostAsJsonAsync("/api/v1/price-lists", new { name = "Test", rules = Array.Empty<object>() });
        Assert.Equal(status, (int)response.StatusCode);
    }

    [Fact]
    public async Task InvalidInputReturnsProblemDetailsAndReadAccessWorksForReadOnlyRoles()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var sales = fixture.CreateClient("Sales");
        var response = await sales.PostAsJsonAsync("/api/v1/price-lists", new { name = "Test", currency = "USD", rules = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        using var accounting = fixture.CreateClient("Accounting");
        Assert.Equal(HttpStatusCode.OK, (await accounting.GetAsync("/api/v1/price-lists")).StatusCode);
        var notFound = await sales.GetAsync($"/api/v1/price-lists/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal("application/problem+json", notFound.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task OpenApiIncludesAllPriceListOperationsAndStringCalculationTypes()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var client = fixture.CreateClient(null);
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/price-lists", out var collection),
            string.Join(", ", paths.EnumerateObject().Select(property => property.Name)));
        Assert.True(collection.TryGetProperty("get", out _));
        Assert.True(collection.TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/v1/price-lists/{id}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/price-lists/{id}").TryGetProperty("delete", out _));
        Assert.True(paths.GetProperty("/api/v1/price-lists/{id}/activation").TryGetProperty("patch", out _));
        Assert.True(paths.GetProperty("/api/v1/price-lists/resolve").TryGetProperty("post", out _));
        var rule = document.GetProperty("components").GetProperty("schemas").GetProperty("PriceRuleRequest");
        var calculation = rule.GetProperty("properties").GetProperty("calculationType");
        Assert.Equal("#/components/schemas/PriceCalculationType", calculation.GetProperty("$ref").GetString());
        var calculationSchema = document.GetProperty("components").GetProperty("schemas").GetProperty("PriceCalculationType");
        Assert.Equal("string", calculationSchema.GetProperty("type").GetString());
    }

    private sealed class Fixture(WebApplication app, Uri address, Guid variantId, Guid productId) : IAsyncDisposable
    {
        public Guid VariantId { get; } = variantId;
        public Guid ProductId { get; } = productId;

        public static async Task<Fixture> CreateAsync(
            PostgreSqlContainerFixture postgres,
            bool invalidCatalogPrice = false)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            await using var database = postgres.CreateDbContext();
            var connectionString = database.Database.GetConnectionString()!;
            builder.Services.AddDbContext<MauwDbContext>(options => options.UseNpgsql(connectionString));
            builder.Services.AddPricingApi();
            builder.Services.AddApplicationServices();
            builder.Services.AddMemoryCache();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IProductVariantImageRepository, ProductVariantImageRepository>();
            builder.Services.AddSingleton(new Moq.Mock<Mauw.Erp.Application.Services.IImageStorageService>().Object);
            builder.Services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
            builder.Services.AddAuthentication("PricingTests")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("PricingTests", _ => { });
            builder.Services.AddAppAuthorization();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new() { Title = "Pricing tests", Version = "v1" }));
            var app = builder.Build();
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapPriceListEndpoints();
            app.MapProductEndpoints();
            Guid variantId;
            Guid productId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MauwDbContext>();
                var product = new Product(Guid.NewGuid(), "S", "P", "Test", 22m, 10m);
                db.Add(product);
                var variant = new ProductVariant(product.Id, "SKU", 10m, product.SupplierId)
                {
                    Product = product, SellingPrice = invalidCatalogPrice ? -1m : 17.12345678m
                };
                db.Add(variant);
                await db.SaveChangesAsync();
                variantId = variant.Id;
                productId = product.Id;
            }
            await app.StartAsync();
            var server = app.Services.GetRequiredService<IServer>();
            var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, new(address), variantId, productId);
        }

        public HttpClient CreateClient(string? role)
        {
            var client = new HttpClient { BaseAddress = address };
            if (role is not null)
                client.DefaultRequestHeaders.Add("X-Pricing-Test-Role", role);
            return client;
        }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Pricing-Test-Role", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, role.ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
