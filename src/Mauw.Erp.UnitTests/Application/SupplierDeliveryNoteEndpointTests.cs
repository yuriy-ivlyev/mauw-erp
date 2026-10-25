using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mauw.Erp.Api.Endpoints;
using Mauw.Erp.Api.Extensions;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public class SupplierDeliveryNoteEndpointTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task MultipartUploadManualAndParsedLinesDownloadAndCancelWorkOverHttp()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var client = fixture.Client("Warehouse");
        using var multipart = fixture.Multipart();
        var created = await client.PostAsync("/api/supplier-delivery-notes", multipart);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var note = (await created.Content.ReadFromJsonAsync<SupplierDeliveryNoteDto>())!;
        Assert.Equal("Draft", note.Status);
        Assert.Empty(note.Lines);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(created.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/supplier-delivery-notes")).StatusCode);

        var parsedResponse = await client.PostAsJsonAsync($"/api/supplier-delivery-notes/{note.Id}/parse", new
        {
            version = note.Version, documentNumber = "B-1", documentDate = DateTimeOffset.UtcNow,
            lines = new[] { new { supplierItemCode = "NEW", description = "Maglia M", quantity = 5, unitPrice = "12.34567890" } }
        });
        Assert.Equal(HttpStatusCode.OK, parsedResponse.StatusCode);
        var parsed = (await parsedResponse.Content.ReadFromJsonAsync<JsonElement>());
        Assert.Equal("Parsed", parsed.GetProperty("status").GetString());
        Assert.Equal("12.34567890", parsed.GetProperty("lines")[0].GetProperty("unitPrice").GetString());
        Assert.Equal("NewArticleNeeded", parsed.GetProperty("lines")[0].GetProperty("lineStatus").GetString());
        var version = parsed.GetProperty("version").GetGuid();
        var removed = await client.PutAsJsonAsync($"/api/supplier-delivery-notes/{note.Id}/lines", new
        {
            version, documentNumber = "B-2", documentDate = DateTimeOffset.UtcNow, lines = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var updated = (await removed.Content.ReadFromJsonAsync<SupplierDeliveryNoteDto>())!;
        Assert.Empty(updated.Lines);
        var downloaded = await client.GetAsync($"/api/supplier-delivery-notes/{note.Id}/file");
        Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
        Assert.Equal("application/pdf", downloaded.Content.Headers.ContentType!.MediaType);
        Assert.Equal("%PDF-1.7 test", await downloaded.Content.ReadAsStringAsync());
        var cancelled = await client.PostAsJsonAsync($"/api/supplier-delivery-notes/{note.Id}/cancel", new { updated.Version });
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("Cancelled", (await cancelled.Content.ReadFromJsonAsync<SupplierDeliveryNoteDto>())!.Status);
        var filtered = await client.GetAsync(
            $"/api/supplier-delivery-notes?search=B-2&supplierId={note.SupplierId}&status=Cancelled&dateFrom={DateTime.UtcNow:yyyy-MM-dd}&dateTo={DateTime.UtcNow:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        var filteredList = await filtered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, filteredList.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, filteredList.GetProperty("items")[0].GetProperty("lineCount").GetInt32());
        Assert.Equal(0, filteredList.GetProperty("items")[0].GetProperty("pieceCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/supplier-delivery-notes?status=Unknown")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/supplier-delivery-notes?dateFrom=2026-10-22&dateTo=2026-10-21")).StatusCode);
        var stale = await client.PostAsJsonAsync($"/api/supplier-delivery-notes/{note.Id}/cancel", new { updated.Version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("application/problem+json", stale.Content.Headers.ContentType!.MediaType);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Sales", HttpStatusCode.Forbidden)]
    [InlineData("Warehouse", HttpStatusCode.OK)]
    [InlineData("Admin", HttpStatusCode.OK)]
    public async Task PurchaseEndpointsEnforceRoles(string? role, HttpStatusCode expected)
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var client = fixture.Client(role);
        Assert.Equal(expected, (await client.GetAsync("/api/supplier-delivery-notes")).StatusCode);
        if (expected is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            Assert.Equal(expected, (await client.GetAsync(
                $"/api/supplier-delivery-notes/{Guid.NewGuid()}/article-match?supplierItemCode=SUP-M")).StatusCode);
    }

    [Theory]
    [InlineData("SUP+M")]
    [InlineData("2900001444804")]
    [InlineData("2900001450614")]
    public async Task SupplierCodeLookupReturnsAnArticleWithoutSavingTheDeliveryNote(string code)
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var client = fixture.Client("Warehouse");
        using var multipart = fixture.Multipart();
        var created = await client.PostAsync("/api/supplier-delivery-notes", multipart);
        var note = (await created.Content.ReadFromJsonAsync<SupplierDeliveryNoteDto>())!;
        var variantId = await fixture.AddArticleAsync(code);

        foreach (var prefix in new[] { "/api", "/api/v1" })
        {
            var response = await client.GetAsync(
                $"{prefix}/supplier-delivery-notes/{note.Id}/article-match?supplierItemCode={Uri.EscapeDataString(code)}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var article = (await response.Content.ReadFromJsonAsync<SupplierArticleMatchDto>())!;
            Assert.Equal(variantId, article.ArticleVariantId);
            Assert.Equal("Maglia HTTP", article.ArticleDescription);
        }
        var unchanged = (await client.GetFromJsonAsync<SupplierDeliveryNoteDto>(
            $"/api/supplier-delivery-notes/{note.Id}"))!;
        Assert.Equal(note.Version, unchanged.Version);
        Assert.Empty(unchanged.Lines);

        var unresolved = await client.GetAsync(
            $"/api/supplier-delivery-notes/{note.Id}/article-match?supplierItemCode=UNKNOWN");
        Assert.Equal(HttpStatusCode.BadRequest, unresolved.StatusCode);
        Assert.Equal("application/problem+json", unresolved.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(
            $"/api/supplier-delivery-notes/{Guid.NewGuid()}/article-match?supplierItemCode=SUP%2BM")).StatusCode);
    }

    [Fact]
    public async Task FractionalJsonQuantitiesAndUnknownDocumentsReturnErrors()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var client = fixture.Client("Admin");
        var id = Guid.NewGuid();
        var fractional = await client.PutAsJsonAsync($"/api/supplier-delivery-notes/{id}/lines", new
        {
            version = Guid.NewGuid(), documentDate = DateTimeOffset.UtcNow,
            lines = new[] { new { description = "Frazionata", quantity = 1.5m, unitPrice = 1m } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, fractional.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/supplier-delivery-notes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(
            $"/api/supplier-delivery-notes/{id}/cancel", new { version = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task SwaggerContainsAllPurchaseEndpointsAndIntegerQuantitySchema()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var client = fixture.Client("Admin");
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var paths = document.GetProperty("paths");
        foreach (var path in new[] { "", "/{id}", "/{id}/file", "/{id}/lines", "/{id}/parse", "/{id}/confirm", "/{id}/cancel", "/{id}/article-match" })
            Assert.True(paths.TryGetProperty($"/api/supplier-delivery-notes{path}", out _), path);
        var quantity = document.GetProperty("components").GetProperty("schemas")
            .GetProperty("SupplierDeliveryNoteLineRequest").GetProperty("properties").GetProperty("quantity");
        Assert.Equal("integer", quantity.GetProperty("type").GetString());
    }

    private sealed class Fixture(WebApplication app, Uri address, Guid supplierId, Guid warehouseId) : IAsyncDisposable
    {
        public static async Task<Fixture> CreateAsync(PostgreSqlContainerFixture postgres)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            await using var database = postgres.CreateDbContext();
            var connectionString = database.Database.GetConnectionString()!;
            builder.Services.AddDbContext<MauwDbContext>(options => options.UseNpgsql(connectionString));
            builder.Services.AddScoped<ISupplierDeliveryNoteService, SupplierDeliveryNoteService>();
            var storage = new Mock<ISupplierDocumentStorage>(MockBehavior.Strict);
            storage.Setup(x => x.UploadAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("supplier-delivery-notes/test/document.pdf");
            storage.Setup(x => x.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => ((Stream)new MemoryStream("%PDF-1.7 test"u8.ToArray()), "application/pdf"));
            builder.Services.AddSingleton(storage.Object);
            builder.Services.AddAuthentication("SupplierTests")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("SupplierTests", _ => { });
            builder.Services.AddAppAuthorization();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new() { Title = "Supplier tests", Version = "v1" }));
            var app = builder.Build();
            app.UseSwagger();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapSupplierDeliveryNoteEndpoints();
            Guid supplierId;
            Guid warehouseId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MauwDbContext>();
                var supplier = new Contact("Fornitore HTTP") { Status = ContactStatus.Active };
                supplier.EnableAsSupplier();
                var address = supplier.SetLegalHeadquarters("Via Test 1", "20100", "Milano");
                db.Add(supplier);
                await db.SaveChangesAsync();
                var warehouse = new Warehouse("HTTP", "Magazzino HTTP", WarehouseType.Standard, address.Id, true);
                db.Add(warehouse);
                await db.SaveChangesAsync();
                supplierId = supplier.Id; warehouseId = warehouse.Id;
            }
            await app.StartAsync();
            var server = app.Services.GetRequiredService<IServer>();
            return new(app, new(server.Features.Get<IServerAddressesFeature>()!.Addresses.Single()), supplierId, warehouseId);
        }

        public async Task<Guid> AddArticleAsync(string barcode)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MauwDbContext>();
            var product = new Product(supplierId, "SHIRT-HTTP", "SHIRT-HTTP", "Maglia HTTP", 0m, 22m, 10m);
            var variant = new ProductVariant(product.Id, barcode, 10m, supplierId, supplierCode: "SUP+M")
            {
                Product = product
            };
            db.AddRange(product, variant);
            await db.SaveChangesAsync();
            return variant.Id;
        }

        public MultipartFormDataContent Multipart()
        {
            var content = new MultipartFormDataContent();
            content.Add(new StringContent(supplierId.ToString()), "supplierId");
            content.Add(new StringContent(warehouseId.ToString()), "warehouseId");
            content.Add(new StringContent(DateTimeOffset.UtcNow.ToString("O")), "documentDate");
            var file = new ByteArrayContent("%PDF-1.7 test"u8.ToArray());
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(file, "file", "supplier.pdf");
            return content;
        }

        public HttpClient Client(string? role)
        {
            var client = new HttpClient { BaseAddress = address };
            if (role is not null) client.DefaultRequestHeaders.Add("X-Supplier-Test-Role", role);
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
            if (!Request.Headers.TryGetValue("X-Supplier-Test-Role", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, role.ToString())], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
