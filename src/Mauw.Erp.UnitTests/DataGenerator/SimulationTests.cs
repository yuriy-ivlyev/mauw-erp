using Mauw.Erp.DataGenerator;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.UnitTests.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mauw.Erp.UnitTests.DataGenerator;

[Collection(PostgreSqlTestCollection.Name)]
public class SimulationTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public void OptionsValidateWithoutThrowing()
    {
        Assert.True(new SimulationOptions().Validate().Success);
        Assert.False((new SimulationOptions { EndDate = DateOnly.MinValue }).Validate().Success);
        Assert.False((new SimulationOptions { Customers = 11 }).Validate().Success);
        Assert.False((new SimulationOptions { ActiveAgents = 301 }).Validate().Success);
        Assert.False((new SimulationOptions { InitialProducts = 20005 }).Validate().Success);
        Assert.False((new SimulationOptions { BatchSize = 0 }).Validate().Success);
        Assert.False((new SimulationOptions { WeeklyPiecesFactor = 0 }).Validate().Success);
        Assert.Equal(4, new SimulationOptions().WeeklyPiecesFactor);
    }

    [Fact]
    public async Task SimulationReconcilesChronologicalStockAndLinkedFiscalDocuments()
    {
        await using var db = Context();
        var options = FixtureOptions();
        var result = await Generator(db).GenerateAsync(options, CancellationToken.None);
        Assert.True(result.Success, result.Error);
        var summary = result.Data!;
        Assert.True(summary.StockReconciled);
        Assert.Equal(10, summary.ActiveCustomers);
        Assert.Equal(3, summary.AssignedCustomers * options.ActiveCustomers / options.Customers);
        AssertDominantShare(summary);
        Assert.Equal(summary.PurchasedUnits / 100, summary.DefectiveUnits);
        Assert.Equal(summary.PurchasedUnits / 100 * 2, summary.SupplierReturnUnits);
        Assert.True(summary.CommercialStockUnits > 0);
        Assert.True(summary.LiquidatedUnits > 0);
        Assert.Empty(db.ChangeTracker.Entries());

        var registry = await db.Contacts.AsNoTracking().ToListAsync();
        var addresses = await db.ContactAddresses.AsNoTracking().ToListAsync();
        var persons = await db.ContactPersons.AsNoTracking().ToListAsync();
        Assert.All(registry, contact =>
        {
            Assert.Matches("^[0-9]{11}$", contact.VatNumber!);
            Assert.True(ContactDataFactory.IsValidVatNumber(contact.VatNumber!));
            Assert.False(string.IsNullOrEmpty(contact.TaxCode));
            Assert.Contains(addresses, a => a.ContactId == contact.Id && a.Purpose == AddressPurpose.LegalHeadquarters);
            Assert.Contains(persons, p => p.ContactId == contact.Id && p.IsPrimary);
        });
        Assert.Equal(registry.Count, registry.Select(item => item.VatNumber).Distinct().Count());
        Assert.All(registry.Where(item => item.TaxCode!.Length == 16), contact =>
            Assert.Equal(ContactDataFactory.TaxCodeCheckCharacter(contact.TaxCode![..15]), contact.TaxCode![15]));
        Assert.All(registry.Where(item => item.SdiCode is not null), contact => Assert.Equal(7, contact.SdiCode!.Length));

        var products = await db.Products.AsNoTracking().OrderBy(item => item.Barcode).ToListAsync();
        var variants = await db.ProductVariants.AsNoTracking().ToListAsync();
        Assert.All(variants, item => Assert.InRange(item.ColorId!.Length, 1, 3));
        Assert.Equal(options.Products, products.Count);
        foreach (var product in products)
        {
            var children = variants.Where(item => item.ProductId == product.Id).ToList();
            Assert.Contains(children.Count, new[] { 35, 42 });
            Assert.Equal(7, children.Select(item => item.SizeId).Distinct().Count());
            Assert.InRange(product.BasePurchaseCost, 5m, 40m);
            var markedUp = new CatalogPriceCalculator().FromMarkup(product.BasePurchaseCost, 40m);
            Assert.Equal(decimal.Ceiling(markedUp.Data!.Value * 10m) / 10m, product.SellingPrice);
        }
        var customers = await db.CustomerProfiles.AsNoTracking().ToListAsync();
        Assert.Equal(options.Customers * 3 / 10, customers.Count(item => item.DefaultAgentId.HasValue));
        var generatedCustomerIds = customers.Select(item => item.ContactId).ToHashSet();
        var generatedCustomers = registry.Where(item => generatedCustomerIds.Contains(item.Id)).ToList();
        Assert.All(generatedCustomers, item => Assert.False(string.IsNullOrWhiteSpace(item.TradeName)));
        Assert.All(generatedCustomers, item => Assert.NotEqual(item.BusinessName, item.TradeName));
        Assert.All(registry.Where(item => !generatedCustomerIds.Contains(item.Id)), item => Assert.Null(item.TradeName));
        var activeContacts = await db.Contacts.AsNoTracking()
            .Where(item => item.CustomerProfile != null && item.Status == ContactStatus.Active)
            .Select(item => item.Id).ToListAsync();
        Assert.Equal(options.ActiveCustomers * 3 / 10,
            customers.Count(item => activeContacts.Contains(item.ContactId) && item.DefaultAgentId.HasValue));
        Assert.Equal(options.ActiveAgents, customers.Where(item => item.DefaultAgentId.HasValue)
            .Select(item => item.DefaultAgentId).Distinct().Count());

        var movements = await db.StockMovements.AsNoTracking().OrderBy(item => item.MovementDateUtc).ToListAsync();
        var stock = await db.StockItems.AsNoTracking().ToListAsync();
        foreach (var group in movements.GroupBy(item => (item.WarehouseId, item.VariantId)))
        {
            long running = 0;
            foreach (var movement in group)
            {
                running += movement.Quantity;
                Assert.True(running >= 0, "A movement cannot consume future stock.");
                Assert.Equal(DateTimeKind.Utc, movement.MovementDateUtc.Kind);
                var product = products.Single(item => item.Id == variants.Single(variant => variant.Id == movement.VariantId).ProductId);
                Assert.True(product.CreatedAt <= movement.MovementDateUtc);
            }
            Assert.Equal(running, stock.Single(item => item.WarehouseId == group.Key.WarehouseId &&
                item.ProductVariantId == group.Key.VariantId).QuantityOnHand);
        }
        var commercial = await db.Warehouses.AsNoTracking().SingleAsync(item => item.IsSellable);
        Assert.Equal(0, movements.Where(item => item.WarehouseId == commercial.Id &&
            item.MovementDateUtc.Year < options.EndDate.Year).Sum(item => item.Quantity));

        var orders = await db.Orders.AsNoTracking().Include(item => item.Items).ToDictionaryAsync(item => item.Id);
        var ddts = await db.DdtDocuments.AsNoTracking().Include(item => item.Items).ToDictionaryAsync(item => item.OrderId!.Value);
        var invoices = await db.Invoices.AsNoTracking().Include(item => item.Items).ToListAsync();
        var customerContactIds = customers.Select(item => item.ContactId).ToHashSet();
        Assert.NotEmpty(orders);
        Assert.All(orders.Values, order => Assert.Contains(order.ContactId, customerContactIds));
        Assert.All(ddts.Values, ddt => Assert.Contains(ddt.ContactId, customerContactIds));
        Assert.All(invoices, invoice => Assert.Contains(invoice.ContactId, customerContactIds));
        var calculator = new ItalianAccountingCalculator();
        foreach (var invoice in invoices)
        {
            var order = orders[invoice.OrderId!.Value];
            var ddt = ddts[order.Id];
            Assert.True(order.OrderDate <= ddt.DdtDate && ddt.DdtDate <= invoice.InvoiceDate);
            Assert.Equal(order.ContactId, invoice.ContactId);
            Assert.Equal(order.ContactId, ddt.ContactId);
            var calculated = calculator.CalculateAndApply(invoice.Items);
            Assert.True(calculated.Success, calculated.Error);
            Assert.Equal(calculated.Data!.TotalGross, invoice.GrossTotal);
            Assert.Equal(order.GrossTotal, invoice.GrossTotal);
            Assert.Equal(ddt.GrossTotal, invoice.GrossTotal);
            var expected = invoice.Items.OrderBy(item => item.VariantId)
                .Select(item => (item.VariantId, item.Quantity, item.UnitNetPrice, item.DiscountPercent, item.VatRate, item.NatureCode, item.LineNetTotal));
            Assert.Equal(expected, order.Items.OrderBy(item => item.VariantId)
                .Select(item => (item.VariantId, item.Quantity, item.UnitNetPrice, item.DiscountPercent, item.VatRate, item.NatureCode, item.LineNetTotal)));
            Assert.Equal(expected, ddt.Items.OrderBy(item => item.VariantId)
                .Select(item => (item.VariantId, item.Quantity, item.UnitNetPrice, item.DiscountPercent, item.VatRate, item.NatureCode, item.LineNetTotal)));
            Assert.All(invoice.Items, item =>
            {
                Assert.Null(item.LineVatTotal);
                Assert.Null(item.LineGrossTotal);
            });
        }
        Assert.Equal(options.ActiveCustomers, invoices.Select(item => item.ContactId).Distinct().Count());

        await using var repeated = Context();
        var second = await Generator(repeated).GenerateAsync(options, CancellationToken.None);
        Assert.True(second.Success, second.Error);
        Assert.Equal(summary, second.Data);
        Assert.Equal(products.Select(item => (item.Barcode, item.BasePurchaseCost, item.SellingPrice)),
            (await repeated.Products.AsNoTracking().OrderBy(item => item.Barcode).ToListAsync())
            .Select(item => (item.Barcode, item.BasePurchaseCost, item.SellingPrice)));

        var allVariants = await db.ProductVariants.AsNoTracking().ToListAsync();
        Assert.All(allVariants.GroupBy(item => item.ProductId), group => Assert.Single(group, item => item.VarianteVetrina));

        // Ids and timestamps differ between runs; every seed-driven registry value must not.
        var registryOf = async (MauwDbContext context) => string.Join("\n",
            (await context.Contacts.AsNoTracking().OrderBy(item => item.VatNumber)
                .Select(item => new { item.BusinessName, item.VatNumber, item.TaxCode, item.PecEmail, item.SdiCode }).ToListAsync())
            .Select(item => item.ToString())
            .Concat((await context.ContactAddresses.AsNoTracking().OrderBy(item => item.Email).ThenBy(item => item.Street)
                .Select(item => new { item.Street, item.ZipCode, item.City, item.Phone, item.Email }).ToListAsync())
                .Select(item => item.ToString()))
            .Concat((await context.ContactPersons.AsNoTracking().OrderBy(item => item.Email)
                .Select(item => new { item.FirstName, item.LastName, item.Email, item.Phone }).ToListAsync())
                .Select(item => item.ToString())));
        Assert.Equal(await registryOf(db), await registryOf(repeated));
    }

    [Fact]
    public async Task CatalogUsesRealColorsEan13BarcodesAndConcentratedStock()
    {
        await using var db = Context();
        var options = FixtureOptions() with
        {
            Years = 3, EndDate = new DateOnly(2026, 10, 19), Products = 100, InitialProducts = 20,
            Suppliers = 10, WeeklyPiecesFactor = 1, SalesLagDays = 60, YearEndCarryoverPercent = 50
        };
        var result = await Generator(db).GenerateAsync(options, CancellationToken.None);
        Assert.True(result.Success, result.Error);

        var colors = await db.Colors.AsNoTracking().ToListAsync();
        Assert.Equal(6, colors.Count);
        Assert.Equal(colors.Count, colors.Select(item => item.HtmlColor).Distinct().Count());
        Assert.All(colors, color => Assert.Matches("^#[0-9A-F]{6}$", color.HtmlColor));
        Assert.Contains(colors, color => color.Name == "Bianco" && color.HtmlColor == "#FFFFFF");

        var products = await db.Products.AsNoTracking().ToListAsync();
        Assert.All(products, product => Assert.True(BarcodeFactory.IsValidEan13(product.Barcode)));
        Assert.Equal(products.Count, products.Select(product => product.Barcode).Distinct().Count());

        var variants = await db.ProductVariants.AsNoTracking().ToListAsync();
        Assert.All(variants, variant =>
        {
            Assert.Matches("^[0-9]{13}$", variant.Barcode);
            Assert.True(BarcodeFactory.IsValidEan13(variant.Barcode));
            Assert.NotNull(variant.SupplierCode);
            Assert.Equal(products.Single(product => product.Id == variant.ProductId).SupplierId, variant.SupplierId);
        });
        Assert.Equal(variants.Count, variants.Select(item => item.Barcode).Distinct().Count());
        Assert.All(variants.GroupBy(item => item.SupplierId), group =>
            Assert.Equal(group.Count(), group.Select(item => item.SupplierCode).Distinct().Count()));

        // Deliveries are size runs of one color: stocked variants hold several pieces, not a single one.
        var stock = await db.StockItems.AsNoTracking().Where(item => item.QuantityOnHand > 0).ToListAsync();
        var commercial = await db.Warehouses.AsNoTracking().SingleAsync(item => item.IsSellable);
        var available = stock.Where(item => item.WarehouseId == commercial.Id).ToList();
        Assert.NotEmpty(available);
        Assert.True(available.Average(item => item.QuantityOnHand) >= 2m);
    }

    [Fact]
    public async Task YearEndCarryoverKeepsMoreStockOnTheShelves()
    {
        var options = FixtureOptions() with
        {
            Years = 3, EndDate = new DateOnly(2026, 10, 19), Products = 100, InitialProducts = 20, Suppliers = 10,
            WeeklyPiecesFactor = 1
        };
        await using var cleared = Context();
        var none = await Generator(cleared).GenerateAsync(options with { YearEndCarryoverPercent = 0 }, CancellationToken.None);
        await using var kept = Context();
        var carried = await Generator(kept).GenerateAsync(options with { YearEndCarryoverPercent = 50 }, CancellationToken.None);
        Assert.True(none.Success, none.Error);
        Assert.True(carried.Success, carried.Error);
        Assert.True(carried.Data!.StockReconciled);
        Assert.True(carried.Data.CommercialStockUnits > none.Data!.CommercialStockUnits);
        Assert.True(carried.Data.LiquidatedUnits < none.Data.LiquidatedUnits);
        Assert.False((options with { YearEndCarryoverPercent = 100 }).Validate().Success);
        Assert.False((options with { SellThroughPercent = 100 }).Validate().Success);
    }

    [Fact]
    public void BarcodeFactoryBuildsValidEan13()
    {
        Assert.Equal("2900000000018", BarcodeFactory.Ean13(1));
        Assert.True(BarcodeFactory.IsValidEan13(BarcodeFactory.Ean13(770231)));
        Assert.False(BarcodeFactory.IsValidEan13("2900000000015"));
        Assert.False(BarcodeFactory.IsValidEan13("SIM000001C0XXS"));
    }

    private static void AssertDominantShare(SimulationSummary summary) =>
        Assert.InRange(summary.DominantSupplierUnits, summary.PurchasedUnits * 4 / 5 - 24, summary.PurchasedUnits * 4 / 5 + 24);

    [Fact]
    public async Task InvalidInputDoesNotMutateDatabase()
    {
        await using var db = Context();
        var result = await Generator(db).GenerateAsync(FixtureOptions() with { BatchSize = 0 }, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.False(await db.Contacts.AnyAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SoftDeletedBusinessDataStillPreventsGeneration(bool warehouse)
    {
        await using var db = Context();
        if (warehouse)
        {
            var owner = new Mauw.Erp.Domain.Entities.Contact("Warehouse owner");
            var address = owner.SetLegalHeadquarters("Via Test 1", "20100", "Milano");
            db.Contacts.Add(owner);
            await db.SaveChangesAsync();
            var existing = new Mauw.Erp.Domain.Entities.Warehouse("DELETED", "Deleted",
                WarehouseType.Standard, address.Id, true);
            existing.SoftDelete(null);
            db.Warehouses.Add(existing);
        }
        else
        {
            db.Contacts.Add(new Mauw.Erp.Domain.Entities.Contact("Deleted") { IsDeleted = true });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await Generator(db).GenerateAsync(FixtureOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.False(await db.Products.AnyAsync());
        Assert.False(await db.Invoices.AnyAsync());
    }

    [Fact]
    public async Task FiveYearFixtureHonorsReceiptCalendarAndClearsOnlyCompletedYears()
    {
        await using var db = Context();
        var options = FixtureOptions() with
        {
            Years = 5, EndDate = new DateOnly(2026, 10, 19),
            Suppliers = 600, Products = 600, InitialProducts = 5, BatchSize = 1000
        };
        var result = await Generator(db).GenerateAsync(options, CancellationToken.None);
        Assert.True(result.Success, result.Error);
        var summary = result.Data!;
        Assert.Equal(new DateOnly(2021, 10, 19), summary.StartDate);
        Assert.Equal(options.Products, summary.IntroducedProducts);
        Assert.Equal(60, summary.DominantSuppliers);
        AssertDominantShare(summary);
        Assert.True(summary.StockReconciled);
        Assert.True(summary.CommercialStockUnits > 0);

        var movements = await db.StockMovements.AsNoTracking().ToListAsync();
        Assert.All(movements, movement => Assert.InRange(DateOnly.FromDateTime(movement.MovementDateUtc),
            summary.StartDate, options.EndDate));
        var receipts = movements.Where(item => item.Type == StockMovementType.PurchaseReceipt).ToList();
        var opening = receipts.Where(item => DateOnly.FromDateTime(item.MovementDateUtc) == summary.StartDate).ToList();
        Assert.Equal(options.InitialProducts * 24, opening.Count);
        var firstWeek = summary.StartDate.DayNumber + 1;
        var weekly = receipts.Except(opening)
            .GroupBy(item => (DateOnly.FromDateTime(item.MovementDateUtc).DayNumber - firstWeek) / 7);
        foreach (var week in weekly)
        {
            Assert.InRange(week.Sum(item => item.Quantity), 300, 600);
            var days = Math.Min(7, options.EndDate.DayNumber - firstWeek - week.Key * 7 + 1);
            Assert.InRange(week.Select(item => item.ReferenceDoc).Distinct().Count(), Math.Min(2, days), Math.Min(3, days));
        }
        var commercial = await db.Warehouses.AsNoTracking().SingleAsync(item => item.IsSellable);
        foreach (var year in Enumerable.Range(summary.StartDate.Year, options.EndDate.Year - summary.StartDate.Year))
            Assert.Equal(0, movements.Where(item => item.WarehouseId == commercial.Id &&
                item.MovementDateUtc.Year == year).Sum(item => item.Quantity));
        var variants = await db.ProductVariants.AsNoTracking().ToDictionaryAsync(item => item.Id);
        var products = await db.Products.AsNoTracking().ToListAsync();
        foreach (var product in products)
        {
            var firstReceipt = receipts.Where(item => variants[item.VariantId].ProductId == product.Id)
                .Min(item => item.MovementDateUtc);
            Assert.Equal(DateOnly.FromDateTime(firstReceipt), DateOnly.FromDateTime(product.CreatedAt));
        }
    }

    private static SimulationOptions FixtureOptions() => new()
    {
        EndDate = new DateOnly(2026, 2, 1), Years = 1, Customers = 20, ActiveCustomers = 10,
        Suppliers = 10, Agents = 4, ActiveAgents = 2,         Products = 10, InitialProducts = 10, BatchSize = 100,
                WeeklyPiecesFactor = 1, YearEndCarryoverPercent = 0
            };

    private MauwDbContext Context() => postgres.CreateDbContext();

    private static SimulationGenerator Generator(MauwDbContext db) => new(
        db, new ItalianAccountingCalculator(), new CatalogPriceCalculator(),
        NullLogger<SimulationGenerator>.Instance);
}
