using System.Reflection;
using Mauw.Erp.Application.Constants;
using Mauw.Erp.Application.Costs;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Pricing;
using Mauw.Erp.Domain.Pricing;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

public sealed class CostVisibilityTests
{
    private static ICostVisibility For(params string[] roles)
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(service => service.HasRole(It.IsAny<string>())).Returns<string>(role => roles.Contains(role));
        return new CostVisibility(user.Object);
    }

    private static readonly ICostVisibility Admin = For(AppRoles.Admin);
    private static readonly ICostVisibility Accounting = For(AppRoles.Accounting);
    private static readonly ICostVisibility Sales = For(AppRoles.Sales);
    private static readonly ICostVisibility Warehouse = For(AppRoles.Warehouse);

    public static TheoryData<string, bool, bool> RoleRules => new()
    {
        { AppRoles.Admin, true, true },
        { AppRoles.Accounting, true, false },
        { AppRoles.Sales, false, false },
        { AppRoles.Warehouse, false, false },
        { "Unknown", false, false }
    };

    [Theory]
    [MemberData(nameof(RoleRules))]
    public void Visibility_FollowsTheRoleRules(string role, bool costs, bool margins)
    {
        var visibility = For(role);
        Assert.Equal(costs, visibility.CanSeeCosts);
        Assert.Equal(margins, visibility.CanSeeMargins);
    }

    [Fact]
    public void Visibility_WithoutRolesSeesNothing()
    {
        var visibility = For();
        Assert.False(visibility.CanSeeCosts);
        Assert.False(visibility.CanSeeMargins);
    }

    private static ProductVariantDto Variant() => new(
        Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, "BAR", 12.34m, 5, 20m, 20m, 4.4m, 24.4m);

    private static ProductDto Product() => new(
        Guid.NewGuid(), Guid.NewGuid(), "S", "BAR", "Prodotto", 20m, 22m, 12.34m, [Variant(), Variant()]);

    private static InventoryStockDto StockRow() => new(
        Guid.NewGuid(), "W", "Deposito", Guid.NewGuid(), "SKU", "BAR", "Desc", null, null, null,
        10m, 0m, 0m, 10m, "pz", 12.34m, 123.40m, "StockMovements");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Redact_Product_NullsBaseAndVariantCostsOnlyWithoutCostVisibility(bool canSee)
    {
        var visibility = canSee ? Accounting : Sales;
        var source = Product();
        var result = visibility.Redact(source);
        if (canSee)
        {
            Assert.Same(source, result);
            return;
        }

        Assert.Null(result.BasePurchaseCost);
        Assert.All(result.Variants, variant => Assert.Null(variant.PurchaseCost));
        Assert.Equal(12.34m, source.BasePurchaseCost);
        Assert.All(source.Variants, variant => Assert.Equal(12.34m, variant.PurchaseCost));
        Assert.Equal(source.Name, result.Name);
        Assert.Equal(source.Variants[0].ListPrice, result.Variants[0].ListPrice);
    }

    [Fact]
    public void Redact_InventoryRowsAndTotals_NullValuesAndKeepQuantities()
    {
        var page = new InventoryStocksPageDto([StockRow()], 1, 20, DateTime.UtcNow, null, null);
        var redacted = Warehouse.Redact(page).Items.Single();
        Assert.Null(redacted.UnitCost);
        Assert.Null(redacted.TotalValue);
        Assert.Null(redacted.CostSource);
        Assert.Equal(10m, redacted.QuantityOnHand);

        var totals = Warehouse.Redact(new InventoryStockTotalsDto(10m, 1m, 2m, 9m, 123.40m));
        Assert.Null(totals.TotalValue);
        Assert.Equal(9m, totals.TotalAvailableQuantity);
        Assert.Equal(123.40m, Admin.Redact(new InventoryStockTotalsDto(10m, 1m, 2m, 9m, 123.40m)).TotalValue);
    }

    [Fact]
    public void Redact_Export_ProducesACostFreeReport()
    {
        var export = new InventoryStockExportDto(
            [StockRow()], new InventoryStockTotalsDto(10m, 0m, 0m, 10m, 123.40m), DateTime.UtcNow, null, null);
        Assert.True(Accounting.Redact(export).IncludesCosts);
        var redacted = Sales.Redact(export);
        Assert.False(redacted.IncludesCosts);
        Assert.All(redacted.Items, row => Assert.Null(row.TotalValue));
        Assert.Equal(10m, redacted.Totals.TotalPhysicalQuantity);
    }

    [Fact]
    public void Redact_Movements_StockItemsAndLegacyMovements()
    {
        var movement = new InventoryStockMovementDto(
            Guid.NewGuid(), DateTime.UtcNow, "Receipt", null, 5m, 5m, 12.34m, null, null, 5m);
        var response = new InventoryStockMovementsResponseDto([movement], 1, 20, 1, 1);
        Assert.Null(Warehouse.Redact(response).Items.Single().UnitCost);
        Assert.Equal(12.34m, Admin.Redact(response).Items.Single().UnitCost);

        var item = new StockItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "W", "D", Guid.NewGuid(), "BAR", "P", null, null, 3m, 1m, 2m, 12.34m, 37.02m);
        var redactedItem = Sales.Redact(item);
        Assert.Null(redactedItem.UnitCost);
        Assert.Null(redactedItem.EstimatedValue);
        Assert.Equal(3m, redactedItem.QuantityOnHand);

        var legacy = new StockMovementDto(Guid.NewGuid(), Guid.NewGuid(), "In", 1, 12.34m, null, DateTime.UtcNow);
        Assert.Null(Sales.Redact(legacy).UnitCost);
        Assert.Equal(12.34m, Accounting.Redact(legacy).UnitCost);
    }

    [Fact]
    public void Redact_PriceList_KeepsCostMarkupRuleButNullsItsValueUnlessAdmin()
    {
        var markup = new PriceRuleRequest { Id = Guid.NewGuid(), CalculationType = PriceCalculationType.CostMarkup, Value = 35m };
        var fixedPrice = new PriceRuleRequest { Id = Guid.NewGuid(), CalculationType = PriceCalculationType.FixedPrice, Value = 15m };
        var list = new PriceListResponse(Guid.NewGuid(), "Listino", "EUR", true, [markup, fixedPrice]);

        Assert.Same(list, Admin.Redact(list));
        foreach (var visibility in new[] { Accounting, Sales, Warehouse })
        {
            var redacted = visibility.Redact(list);
            Assert.Equal(2, redacted.Rules.Count);
            Assert.Null(redacted.Rules[0].Value);
            Assert.Equal(PriceCalculationType.CostMarkup, redacted.Rules[0].CalculationType);
            Assert.Equal(markup.Id, redacted.Rules[0].Id);
            Assert.Equal(15m, redacted.Rules[1].Value);
        }
        Assert.Equal(35m, markup.Value);
    }

    public static TheoryData<string> CostSortKeys => new()
    {
        "purchaseCost", "PURCHASECOST", "unitCost", " unitcost ", "totalValue", "estimatedValue"
    };

    [Theory]
    [MemberData(nameof(CostSortKeys))]
    public void Sort_IgnoresCostKeysWithoutCostVisibility(string key)
    {
        Assert.Equal((null, false), Sales.Sort(key, true));
        Assert.Equal((null, false), Warehouse.Sort(key, true));
        Assert.Equal((key, true), Admin.Sort(key, true));
        Assert.Equal((key, true), Accounting.Sort(key, true));
    }

    [Theory]
    [InlineData("sku", true)]
    [InlineData("quantityOnHand", false)]
    [InlineData(null, true)]
    public void Sort_KeepsOtherKeys(string? key, bool descending) =>
        Assert.Equal((key, descending), Sales.Sort(key, descending));

    [Fact]
    public void EveryCostSensitiveMember_IsNullableAndItsTypeIsHandledByTheRedactor()
    {
        var annotated = typeof(CostSensitiveAttribute).Assembly.GetTypes()
            .Where(type => type.GetProperties().Any(property => property.GetCustomAttribute<CostSensitiveAttribute>() is not null))
            .ToList();
        Assert.NotEmpty(annotated);
        Assert.Equal(
            annotated.Select(type => type.FullName).Order(),
            CostRedaction.RedactedTypes.Select(type => type.FullName).Order());

        var context = new NullabilityInfoContext();
        foreach (var property in annotated.SelectMany(type => type.GetProperties())
                     .Where(property => property.GetCustomAttribute<CostSensitiveAttribute>() is not null))
        {
            var underlying = Nullable.GetUnderlyingType(property.PropertyType);
            Assert.True(
                underlying is not null || context.Create(property).WriteState == NullabilityState.Nullable,
                $"{property.DeclaringType!.Name}.{property.Name} must be nullable");
        }
    }
}
