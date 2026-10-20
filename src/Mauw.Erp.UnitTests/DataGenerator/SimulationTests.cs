using Mauw.Erp.DataGenerator;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mauw.Erp.UnitTests.DataGenerator;

public class SimulationTests
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
        Assert.Equal(summary.PurchasedUnits * 4 / 5, summary.DominantSupplierUnits);
        Assert.InRange(summary.RegularSoldUnits * 100m / summary.PurchasedUnits, 85m, 95m);
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

        var products = await db.Products.AsNoTracking().OrderBy(item => item.InternalCode).ToListAsync();
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
        Assert.Equal(products.Select(item => (item.InternalCode, item.BasePurchaseCost, item.SellingPrice)),
            (await repeated.Products.AsNoTracking().OrderBy(item => item.InternalCode).ToListAsync())
            .Select(item => (item.InternalCode, item.BasePurchaseCost, item.SellingPrice)));
    }

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
            var existing = new Mauw.Erp.Domain.Entities.Warehouse("DELETED", "Deleted",
                WarehouseType.Standard, Guid.NewGuid(), true);
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
        Assert.Equal(summary.PurchasedUnits * 4 / 5, summary.DominantSupplierUnits);
        Assert.Equal(options.ActiveCustomers, summary.ActiveCustomers);
        Assert.True(summary.StockReconciled);
        Assert.True(summary.CommercialStockUnits > 0);

        var movements = await db.StockMovements.AsNoTracking().ToListAsync();
        Assert.All(movements, movement => Assert.InRange(DateOnly.FromDateTime(movement.MovementDateUtc),
            summary.StartDate, options.EndDate));
        var receipts = movements.Where(item => item.Type == StockMovementType.PurchaseReceipt).ToList();
        var opening = receipts.Where(item => DateOnly.FromDateTime(item.MovementDateUtc) == summary.StartDate).ToList();
        Assert.Equal(options.InitialProducts, opening.Count);
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
                WeeklyPiecesFactor = 1
            };

    private static MauwDbContext Context() => new(new DbContextOptionsBuilder<MauwDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static SimulationGenerator Generator(MauwDbContext db) => new(
        db, new ItalianAccountingCalculator(), new CatalogPriceCalculator(),
        NullLogger<SimulationGenerator>.Instance);
}
