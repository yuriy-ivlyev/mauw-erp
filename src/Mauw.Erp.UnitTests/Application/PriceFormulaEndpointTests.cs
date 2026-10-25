using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Mauw.Erp.Api.Endpoints;
using Mauw.Erp.Api.Extensions;
using Mauw.Erp.Api.Services;
using Mauw.Erp.Application.Extensions;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Application.Validation;
using Mauw.Erp.Contracts.Pricing;
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
public class PriceFormulaEndpointTests(PostgreSqlContainerFixture postgres)
{
    private const string FormulaPath = "/api/v1/settings/price-formula";
    private const string CalculatePath = "/api/v1/products/price-formula/calculate";
    private const string NotConfigured = "Formula prezzi non configurata: impostarla in Impostazioni > Formula prezzi.";

    private static readonly SavePriceFormulaRequest ValidFormula = new()
    {
        Steps = [new("Ricarico", 40m), new("Provvigione", 10m), new("Sconto cliente", 5m)],
        RoundingStep = 0.10m
    };

    public static TheoryData<string, SavePriceFormulaRequest> InvalidFormulas => new()
    {
        { "no steps", new() { Steps = [], RoundingStep = 0.10m } },
        { "negative percentage", new() { Steps = [new("A", -0.01m)], RoundingStep = 0.10m } },
        { "percentage above 9999", new() { Steps = [new("A", 9999.01m)], RoundingStep = 0.10m } },
        { "three decimals", new() { Steps = [new("A", 10.123m)], RoundingStep = 0.10m } },
        { "empty label", new() { Steps = [new("  ", 10m)], RoundingStep = 0.10m } },
        { "zero rounding step", new() { Steps = [new("A", 10m)], RoundingStep = 0m } },
        { "negative rounding step", new() { Steps = [new("A", 10m)], RoundingStep = -0.10m } },
        { "rounding step with three decimals", new() { Steps = [new("A", 10m)], RoundingStep = 0.105m } }
    };

    public static TheoryData<decimal> InvalidCosts => new() { -0.01m, 10.005m, 100000000000000m };

    [Fact]
    public async Task GetBeforeConfigurationReturnsAnUnconfiguredDefault()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var admin = fixture.CreateClient("Admin");

        var response = await admin.GetAsync(FormulaPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isConfigured").GetBoolean());
        Assert.Equal(0, body.GetProperty("steps").GetArrayLength());
        Assert.Equal(0.10m, body.GetProperty("roundingStep").GetDecimal());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("updatedAt").ValueKind);
        Assert.Equal(0, await fixture.CountAsync<PriceFormula>());
    }

    [Fact]
    public async Task PutThenGetRoundTripsValuesOrderAndAuthor()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var admin = fixture.CreateClient("Admin");

        var saved = await admin.PutAsJsonAsync(FormulaPath, ValidFormula);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var savedBody = await saved.Content.ReadFromJsonAsync<PriceFormulaDto>();
        var loaded = await (await admin.GetAsync(FormulaPath)).Content.ReadFromJsonAsync<PriceFormulaDto>();

        Assert.Equal(savedBody, loaded, new PriceFormulaDtoComparer());
        Assert.True(loaded!.IsConfigured);
        Assert.Equal(0.10m, loaded.RoundingStep);
        Assert.Equal(["Ricarico", "Provvigione", "Sconto cliente"], loaded.Steps.Select(step => step.Label));
        Assert.Equal([40m, 10m, 5m], loaded.Steps.Select(step => step.Percentage));
        Assert.Equal("user-Admin", loaded.UpdatedByUserId);
        Assert.NotNull(loaded.UpdatedAt);
    }

    [Fact]
    public async Task PutReplacesTheStepsOfTheSingleFormula()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var admin = fixture.CreateClient("Admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(FormulaPath, ValidFormula)).StatusCode);

        var replacement = new SavePriceFormulaRequest { Steps = [new("Nuovo", 25.50m), new("Altro", 0m)], RoundingStep = 0.05m };
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(FormulaPath, replacement)).StatusCode);

        var loaded = await (await admin.GetAsync(FormulaPath)).Content.ReadFromJsonAsync<PriceFormulaDto>();
        Assert.Equal(["Nuovo", "Altro"], loaded!.Steps.Select(step => step.Label));
        Assert.Equal([25.50m, 0m], loaded.Steps.Select(step => step.Percentage));
        Assert.Equal(0.05m, loaded.RoundingStep);
        Assert.Equal(1, await fixture.CountAsync<PriceFormula>());
        Assert.Equal(2, await fixture.CountAsync<PriceFormulaStep>());
    }

    [Theory]
    [MemberData(nameof(InvalidFormulas))]
    public async Task InvalidPutReturnsBadRequestAndKeepsTheStoredFormula(string scenario, SavePriceFormulaRequest invalid)
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var admin = fixture.CreateClient("Admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(FormulaPath, ValidFormula)).StatusCode);
        var before = await (await admin.GetAsync(FormulaPath)).Content.ReadFromJsonAsync<PriceFormulaDto>();

        var response = await admin.PutAsJsonAsync(FormulaPath, invalid);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, scenario);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()), scenario);
        var after = await (await admin.GetAsync(FormulaPath)).Content.ReadFromJsonAsync<PriceFormulaDto>();
        Assert.Equal(before, after, new PriceFormulaDtoComparer());
    }

    [Fact]
    public async Task InvalidPutBeforeConfigurationStoresNothing()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var admin = fixture.CreateClient("Admin");

        var response = await admin.PutAsJsonAsync(FormulaPath, new SavePriceFormulaRequest { Steps = [] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await fixture.CountAsync<PriceFormula>());
        Assert.False((await (await admin.GetAsync(FormulaPath)).Content.ReadFromJsonAsync<PriceFormulaDto>())!.IsConfigured);
    }

    [Fact]
    public async Task CalculateBeforeConfigurationReturnsTheNotConfiguredError()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var admin = fixture.CreateClient("Admin");

        var response = await admin.PostAsJsonAsync(CalculatePath, new CalculatePriceRequest { PurchaseCost = 20.00m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(NotConfigured, problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task CalculateReturnsTheCascadeBreakdownAndTheRoundedUpFinalPrice()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var admin = fixture.CreateClient("Admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(FormulaPath, ValidFormula)).StatusCode);

        var response = await admin.PostAsJsonAsync(CalculatePath, new CalculatePriceRequest { PurchaseCost = 20.00m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(20.00m, body.GetProperty("purchaseCost").GetDecimal());
        Assert.Equal(32.34m, body.GetProperty("unroundedPrice").GetDecimal());
        Assert.Equal(0.10m, body.GetProperty("roundingStep").GetDecimal());
        Assert.Equal(32.40m, body.GetProperty("finalPrice").GetDecimal());
        var steps = body.GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(["Ricarico", "Provvigione", "Sconto cliente"], steps.Select(s => s.GetProperty("label").GetString()));
        Assert.Equal([40m, 10m, 5m], steps.Select(s => s.GetProperty("percentage").GetDecimal()));
        Assert.Equal([28.00m, 30.80m, 32.34m], steps.Select(s => s.GetProperty("runningValue").GetDecimal()));
    }

    [Theory]
    [MemberData(nameof(InvalidCosts))]
    public async Task CalculateRejectsInvalidPurchaseCosts(decimal cost)
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var admin = fixture.CreateClient("Admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(FormulaPath, ValidFormula)).StatusCode);

        var response = await admin.PostAsJsonAsync(CalculatePath, new CalculatePriceRequest { PurchaseCost = cost });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("costo d'acquisto", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task CalculateNeverPersistsAnything()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var admin = fixture.CreateClient("Admin");
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(FormulaPath, ValidFormula)).StatusCode);
        var formulaBefore = await (await admin.GetAsync(FormulaPath)).Content.ReadFromJsonAsync<PriceFormulaDto>();
        var priceBefore = await fixture.VariantPriceAsync();

        Assert.Equal(HttpStatusCode.OK,
            (await admin.PostAsJsonAsync(CalculatePath, new CalculatePriceRequest { PurchaseCost = 20.00m })).StatusCode);

        Assert.Equal(17.12345678m, priceBefore);
        Assert.Equal(priceBefore, await fixture.VariantPriceAsync());
        Assert.Equal(1, await fixture.CountAsync<Product>());
        Assert.Equal(1, await fixture.CountAsync<ProductVariant>());
        var formulaAfter = await (await admin.GetAsync(FormulaPath)).Content.ReadFromJsonAsync<PriceFormulaDto>();
        Assert.Equal(formulaBefore, formulaAfter, new PriceFormulaDtoComparer());
    }

    [Theory]
    [InlineData(null, "GET", FormulaPath, 401)]
    [InlineData(null, "PUT", FormulaPath, 401)]
    [InlineData(null, "POST", CalculatePath, 401)]
    [InlineData("Sales", "GET", FormulaPath, 403)]
    [InlineData("Sales", "PUT", FormulaPath, 403)]
    [InlineData("Sales", "POST", CalculatePath, 403)]
    [InlineData("Warehouse", "GET", FormulaPath, 403)]
    [InlineData("Warehouse", "PUT", FormulaPath, 403)]
    [InlineData("Warehouse", "POST", CalculatePath, 403)]
    [InlineData("Accounting", "GET", FormulaPath, 403)]
    [InlineData("Accounting", "PUT", FormulaPath, 403)]
    [InlineData("Accounting", "POST", CalculatePath, 403)]
    [InlineData("Admin", "GET", FormulaPath, 200)]
    [InlineData("Admin", "PUT", FormulaPath, 200)]
    [InlineData("Admin", "POST", CalculatePath, 400)]
    public async Task OnlyAdminsCanUseTheFormulaEndpoints(string? role, string method, string path, int expected)
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var client = fixture.CreateClient(role);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "PUT")
            request.Content = JsonContent.Create(ValidFormula);
        if (method == "POST")
            request.Content = JsonContent.Create(new CalculatePriceRequest { PurchaseCost = 20.00m });

        var response = await client.SendAsync(request);

        Assert.Equal(expected, (int)response.StatusCode);
        if (role is "Sales" or "Warehouse" or "Accounting")
            Assert.Equal(0, await fixture.CountAsync<PriceFormula>());
    }

    [Fact]
    public async Task LegacyCalculationIsMarkedObsoleteInOpenApiAndStillWorks()
    {
        await using var fixture = await Fixture.CreateAsync(postgres);
        using var sales = fixture.CreateClient("Sales");
        using var anonymous = fixture.CreateClient(null);

        var legacy = await sales.PostAsJsonAsync("/api/v1/products/calculate-selling-price",
            new { purchaseCost = 10m, markupRate = 10m });
        Assert.Equal(HttpStatusCode.OK, legacy.StatusCode);
        Assert.Equal(11m, await legacy.Content.ReadFromJsonAsync<decimal>());

        var document = await anonymous.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var summary = document.GetProperty("paths").GetProperty("/api/v1/products/calculate-selling-price")
            .GetProperty("post").GetProperty("summary").GetString();
        Assert.StartsWith("[Obsoleto: sostituito da price-formula/calculate]", summary);
        Assert.True(document.GetProperty("paths").TryGetProperty(CalculatePath, out _));
        Assert.True(document.GetProperty("paths").GetProperty(FormulaPath).TryGetProperty("put", out _));
    }

    private sealed class PriceFormulaDtoComparer : IEqualityComparer<PriceFormulaDto?>
    {
        public bool Equals(PriceFormulaDto? x, PriceFormulaDto? y) =>
            x is not null && y is not null
            && x.IsConfigured == y.IsConfigured && x.RoundingStep == y.RoundingStep
            && x.UpdatedAt == y.UpdatedAt && x.UpdatedByUserId == y.UpdatedByUserId
            && x.Steps.SequenceEqual(y.Steps);

        public int GetHashCode(PriceFormulaDto? obj) => obj?.RoundingStep.GetHashCode() ?? 0;
    }

    private sealed class Fixture(WebApplication app, Uri address, Guid variantId) : IAsyncDisposable
    {
        public static async Task<Fixture> CreateAsync(PostgreSqlContainerFixture postgres)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(System.Net.IPAddress.Loopback, 0));
            await using var database = postgres.CreateDbContext();
            var connectionString = database.Database.GetConnectionString()!;
            builder.Services.AddDbContext<MauwDbContext>(options => options.UseNpgsql(connectionString));
            builder.Services.AddPricingApi();
            builder.Services.AddApplicationServices();
            builder.Services.AddMemoryCache();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IProductVariantImageRepository, ProductVariantImageRepository>();
            builder.Services.AddSingleton(new Moq.Mock<IImageStorageService>().Object);
            builder.Services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
            builder.Services.AddAuthentication("FormulaTests")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("FormulaTests", _ => { });
            builder.Services.AddAppAuthorization();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new() { Title = "Formula tests", Version = "v1" }));
            var app = builder.Build();
            app.UseSwagger();
            app.UseAuthentication();
            app.UseAuthorization();
            // I prodotti sono mappati insieme alla formula per provare che /{id} non intercetta la rotta letterale.
            app.MapProductEndpoints();
            app.MapPriceFormulaEndpoints();
            Guid variantId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MauwDbContext>();
                var product = new Product(Guid.NewGuid(), "S", "P", "Test", 0m, 22m, 10m);
                db.Add(product);
                var variant = new ProductVariant(product.Id, "SKU", 10m, product.SupplierId)
                {
                    Product = product, SellingPrice = 17.12345678m
                };
                db.Add(variant);
                await db.SaveChangesAsync();
                variantId = variant.Id;
            }
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, new(address), variantId);
        }

        public HttpClient CreateClient(string? role)
        {
            var client = new HttpClient { BaseAddress = address };
            if (role is not null)
                client.DefaultRequestHeaders.Add("X-Formula-Test-Role", role);
            return client;
        }

        public async Task<int> CountAsync<T>() where T : class
        {
            using var scope = app.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<MauwDbContext>().Set<T>().CountAsync();
        }

        public async Task<decimal> VariantPriceAsync()
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MauwDbContext>();
            return (await db.Set<ProductVariant>().AsNoTracking().SingleAsync(v => v.Id == variantId)).SellingPrice!.Value;
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
            if (!Request.Headers.TryGetValue("X-Formula-Test-Role", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Role, role.ToString()), new Claim(ClaimTypes.NameIdentifier, $"user-{role}")],
                Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}
