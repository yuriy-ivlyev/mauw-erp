using Bogus;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mauw.Erp.DataGenerator;

/// <summary>A single-use, append-only simulation for an empty business database.</summary>
public sealed class SimulationGenerator(
    MauwDbContext db,
    ItalianAccountingCalculator accounting,
    PriceFormulaCalculator prices,
    ILogger<SimulationGenerator> logger)
{
    private int pendingEntities;
    // Fixed demo formula (not read from the database): +40%, +10%, rounded up to 0.10 EUR.
    private static readonly PriceFormulaDefinition DemoPriceFormula = new(
        [new PriceFormulaStepDefinition("Ricarico", 40m), new PriceFormulaStepDefinition("Provvigione", 10m)], 0.10m);
    private static readonly string[] SizeCodes = ["XXS", "XS", "S", "M", "L", "XL", "XXL"];
    private static readonly (string Name, string Html)[] Palette =
    [
        ("Nero", "#1A1A1A"), ("Bianco", "#FFFFFF"), ("Blu", "#1E40AF"),
        ("Rosso", "#DC2626"), ("Verde", "#16A34A"), ("Beige", "#D8C8A8")
    ];

    // Each delivery of a model brings one color in a size run (pieces per size, XXS..XXL) summing to LotSize.
    private const int LotSize = 24;
    private static readonly int[] SizeRun = BuildSizeRun([1, 3, 5, 6, 5, 3, 1]);
    private const long InternalProductCodeSequenceStart = 1_000_000_000L;
    private const long VariantBarcodeSequenceStart = 3_000_000_000L;

    private static int[] BuildSizeRun(int[] pieces)
    {
        // Interleaved so a delivery split across two receipts is not skewed towards the smallest sizes.
        var run = new List<int>();
        for (var round = 0; round < pieces.Max(); round++)
            for (var size = 0; size < pieces.Length; size++)
                if (pieces[size] > round)
                    run.Add(size);
        return [.. run];
    }

    public async Task<AccountingResult<SimulationSummary>> GenerateAsync(
        SimulationOptions options, CancellationToken token)
    {
        var validation = options.Validate();
        if (!validation.Success)
            return AccountingResult<SimulationSummary>.Fail(validation.Error);
        if (db.ChangeTracker.HasChanges())
            return Fail("The generator requires a context without pending changes.");
        if (await db.Contacts.IgnoreQueryFilters().AnyAsync(token) || await db.Products.AnyAsync(token) ||
            await db.Warehouses.IgnoreQueryFilters().AnyAsync(token) || await db.StockMovements.AnyAsync(token) ||
            await db.Orders.AnyAsync(token) || await db.Invoices.AnyAsync(token) ||
            await db.DdtDocuments.AnyAsync(token) || await db.StockItems.AnyAsync(token))
            return Fail("The generator requires an empty business database; lookup data may already exist.");

        var start = options.EndDate.AddYears(-options.Years);
        var schedule = PlanReceipts(start, options);
        var totalPurchased = schedule.Sum(receipt => (long)receipt.Quantity);
        if (totalPurchased * 9 / 10 < options.ActiveCustomers * 2L)
            return Fail("The horizon supplies too few units to give every active customer movements.");

        var faker = new Faker("it") { Random = new Randomizer(options.Seed) };
        var dominantCount = Math.Max(1, options.Suppliers / 10);
        var state = new RunState(options, faker, dominantCount, totalPurchased);
        var originalDetectChanges = db.ChangeTracker.AutoDetectChangesEnabled;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            logger.LogInformation("Simulation: contacts, agents and warehouses");
            await CreateContactsAsync(state, start, token);
            await CreateLookupsAsync(token);
            logger.LogInformation("Simulation: {Models} models and their color/size variants", options.Products);
            var catalogResult = await CreateCatalogAsync(state, schedule, token);
            if (!catalogResult.Success)
                return Fail(catalogResult.Error);

            logger.LogInformation("Simulation: chronological receipts, returns, defects and sales");
            var year = start.Year;
            foreach (var receipt in schedule)
            {
                token.ThrowIfCancellationRequested();
                while (year < receipt.Date.Year)
                {
                    var clearance = await ClearYearAsync(state, year, token);
                    if (!clearance.Success)
                        return Fail(clearance.Error);
                    year++;
                }
                await ReceiveAsync(state, receipt, token);
                var cutoff = receipt.Date.AddDays(-options.SalesLagDays);
                while (state.LagIndex < schedule.Count && schedule[state.LagIndex].Date <= cutoff)
                    state.LagUnits += schedule[state.LagIndex++].Quantity;
                var target = Math.Max(0, state.LagUnits - state.PurchasedAtClear) * options.SellThroughPercent / 100;
                var due = target - (state.RegularSold - state.RegularSoldAtClear);
                var sales = await SellAsync(state,
                    Math.Clamp(due, 0, state.Commercial.Values.Sum(stock => stock.Quantity)),
                    receipt.Date, false, token);
                if (!sales.Success)
                    return Fail(sales.Error);
            }
            while (year < options.EndDate.Year)
            {
                var clearance = await ClearYearAsync(state, year, token);
                if (!clearance.Success)
                    return Fail(clearance.Error);
                year++;
            }
            await FlushAsync(token);
            logger.LogInformation("Simulation: persisting and reconciling closing stock");
            foreach (var stock in state.Commercial.Values.Concat(state.Defective.Values))
            {
                var item = new StockItem(stock.WarehouseId, stock.VariantId, stock.Quantity, stock.Cost);
                Track(item, options.EndDate);
                await FlushIfNeededAsync(options.BatchSize, token);
            }
            await FlushAsync(token);
            var commercial = state.Commercial.Values.Sum(stock => (long)stock.Quantity);
            var defective = state.Defective.Values.Sum(stock => (long)stock.Quantity);
            var persistedCommercial = await db.StockItems.AsNoTracking()
                .Where(item => item.WarehouseId == state.CommercialWarehouse)
                .SumAsync(item => item.QuantityOnHand, token);
            var persistedDefective = await db.StockItems.AsNoTracking()
                .Where(item => item.WarehouseId == state.DefectiveWarehouse)
                .SumAsync(item => item.QuantityOnHand, token);
            var commercialMovements = await db.StockMovements.AsNoTracking()
                .Where(item => item.WarehouseId == state.CommercialWarehouse)
                .SumAsync(item => (long)item.Quantity, token);
            var defectiveMovements = await db.StockMovements.AsNoTracking()
                .Where(item => item.WarehouseId == state.DefectiveWarehouse)
                .SumAsync(item => (long)item.Quantity, token);
            var hasInvalidMovementLedger = await db.StockMovements.AsNoTracking()
                .AnyAsync(item =>
                    !item.QuantityDelta.HasValue ||
                    item.QuantityDelta.Value != item.Quantity,
                    token);
            if (hasInvalidMovementLedger)
                return Fail("Generated stock movements must persist a signed quantity delta matching their quantity.");

            var reconciled = state.Purchased == state.Returned + state.RegularSold +
                state.Liquidated + commercial + defective &&
                defective == state.Defects && commercial == persistedCommercial &&
                defective == persistedDefective && commercial == commercialMovements &&
                defective == defectiveMovements && state.CustomersWithMovements.Count == options.ActiveCustomers;
            if (!reconciled)
                return Fail("Closing stock or active customer reconciliation failed.");
            var summary = new SimulationSummary(start, options.EndDate, options.Customers,
                state.CustomersWithMovements.Count, options.Suppliers, dominantCount, options.Agents,
                options.ActiveAgents, options.Customers * 3 / 10, options.Products,
                (int)Math.Min(options.Products, (state.Purchased + LotSize - 1) / LotSize), state.Variants, schedule.Count,
                state.Purchased, state.DominantPurchased, state.Returned, state.Defects, state.RegularSold,
                state.Liquidated, commercial, defective, state.Documents, state.Documents,
                state.Documents, state.Movements, reconciled);
            logger.LogInformation("Simulation complete: {@Summary}", summary);
            return AccountingResult<SimulationSummary>.Ok(summary);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = originalDetectChanges;
        }
    }

    private async Task CreateContactsAsync(RunState state, DateOnly date, CancellationToken token)
    {
        var options = state.Options;
        for (var i = 0; i < options.Agents; i++)
        {
            var first = state.Faker.Name.FirstName();
            var last = state.Faker.Name.LastName();
            var contact = new Contact($"Agente SIM {i + 1:D4} {first} {last}");
            contact.EnableAsAgent();
            if (i < options.ActiveAgents) contact.Activate();
            ContactDataFactory.Fill(contact, state.Faker, i + 1, person: true, first, last);
            Track(contact, date);
            TrackRegistry(contact, state.Faker, 1, date);
            state.Agents.Add(contact.Id);
            await FlushIfNeededAsync(options.BatchSize, token);
        }
        await FlushAsync(token);
        for (var i = 0; i < options.Suppliers; i++)
        {
            var contact = new Contact($"Fornitore SIM {i + 1:D4} {state.Faker.Company.CompanyName()}");
            contact.EnableAsSupplier();
            contact.Activate();
            ContactDataFactory.Fill(contact, state.Faker, options.Agents + i + 1, person: false);
            Track(contact, date);
            TrackRegistry(contact, state.Faker, state.Faker.Random.Int(1, 3), date);
            state.Suppliers.Add(contact.Id);
            await FlushIfNeededAsync(options.BatchSize, token);
        }
        await FlushAsync(token);
        for (var i = 0; i < options.Customers; i++)
        {
            var stockist = i >= options.ActiveCustomers - state.Stockists && i < options.ActiveCustomers;
            var contact = new Contact($"{(stockist ? "Stockista" : "Cliente")} SIM {i + 1:D5} {state.Faker.Company.CompanyName()}");
            contact.EnableAsCustomer();
            if (i < options.ActiveCustomers) contact.Activate();
            var activeAssigned = options.ActiveCustomers * 3 / 10;
            var assigned = i < activeAssigned ||
                (i >= options.ActiveCustomers && i - options.ActiveCustomers < (options.Customers - options.ActiveCustomers) * 3 / 10);
            if (assigned)
                contact.CustomerProfile!.DefaultAgentId = state.Agents[i % options.ActiveAgents];
            ContactDataFactory.Fill(contact, state.Faker, options.Agents + options.Suppliers + i + 1, person: false);
            Track(contact, date);
            TrackRegistry(contact, state.Faker, state.Faker.Random.Int(1, 3), date);
            state.Customers.Add(contact.Id);
            if (i == 0)
            {
                var address = new ContactAddress(contact.Id, AddressPurpose.WarehouseSite,
                    "Via Simulazione 1", "20100", "Milano", "MI");
                Track(address, date);
                var commercial = new Warehouse("SIM-COM", "Magazzino commerciale", WarehouseType.Standard,
                    address.Id, true, true);
                var defective = new Warehouse("SIM-DEF", "Merce difettosa", WarehouseType.Defective,
                    address.Id, false);
                Track(commercial, date);
                Track(defective, date);
                state.CommercialWarehouse = commercial.Id;
                state.DefectiveWarehouse = defective.Id;
            }
            await FlushIfNeededAsync(options.BatchSize, token);
        }
        await FlushAsync(token);
    }

    private void TrackRegistry(Contact contact, Faker faker, int persons, DateOnly date)
    {
        Track(ContactDataFactory.LegalHeadquarters(contact, faker), date);
        foreach (var person in ContactDataFactory.Persons(contact, faker, persons))
            Track(person, date);
    }

    private async Task CreateLookupsAsync(CancellationToken token)
    {
        var colors = await db.Colors.AsNoTracking().Select(color => color.Id).ToListAsync(token);
        for (var i = 0; i < Palette.Length; i++)
            if (!colors.Contains($"G{i:D2}"))
                db.Colors.Add(new Color($"G{i:D2}", Palette[i].Name, Palette[i].Html));
        var sizes = await db.Sizes.AsNoTracking().Select(size => size.Id).ToListAsync(token);
        foreach (var size in SizeCodes)
            if (!sizes.Contains(size))
                db.Sizes.Add(new Size(size, size));
        await FlushAsync(token);
    }

    private async Task<AccountingResult<SimulationOptions>> CreateCatalogAsync(
        RunState state, List<Receipt> schedule, CancellationToken token)
    {
        var introductions = new DateOnly[state.Options.Products];
        long cumulative = 0;
        var introduced = 0;
        foreach (var receipt in schedule)
        {
            cumulative += receipt.Quantity;
            // Model i receives its first delivery with global piece number i * LotSize.
            while (introduced < introductions.Length && (long)introduced * LotSize < cumulative)
                introductions[introduced++] = receipt.Date;
        }
        for (var i = 0; i < state.Options.Products; i++)
        {
            var cost = state.Faker.Random.Int(500, 4000) / 100m;
            var priced = prices.Calculate(cost, DemoPriceFormula);
            if (!priced.Success)
                return AccountingResult<SimulationOptions>.Fail(priced.Error);
            var selling = priced.Data!.FinalPrice;
            var supplier = i % 5 != 4
                ? (i / 5 * 4 + i % 5) % state.DominantCount
                : state.DominantCount + (i / 5) % (state.Options.Suppliers - state.DominantCount);
            var date = introductions[i] == default ? state.Options.EndDate : introductions[i];
            var product = new Product(state.Suppliers[supplier], $"S{i + 1:D6}",
                BarcodeFactory.Ean13(InternalProductCodeSequenceStart + i),
                $"Modello {i + 1:D6}", 22m, cost) { SellingPrice = selling };
            Track(product, date);
            var variantIds = new List<Guid>();
            var colors = state.Faker.Random.Int(5, 6);
            for (var color = 0; color < colors; color++)
                foreach (var size in SizeCodes)
                {
                    var variant = new ProductVariant(product.Id,
                        BarcodeFactory.Ean13(VariantBarcodeSequenceStart + state.Variants + 1L),
                        cost, product.SupplierId, $"G{color:D2}", size)
                    {
                        SupplierCode = $"{product.SupplierCode}-G{color:D2}-{size}",
                        SellingPrice = selling,
                        VarianteVetrina = variantIds.Count == 0
                    };
                    Track(variant, date);
                    variantIds.Add(variant.Id);
                    state.Variants++;
                    await FlushIfNeededAsync(state.Options.BatchSize, token);
                }
            state.Models.Add(new Model(variantIds.ToArray(), cost, selling));
        }
        await FlushAsync(token);
        if (introduced < state.Options.Products)
            logger.LogWarning("Only {Introduced} of {Products} models can receive stock within the requested horizon",
                introduced, state.Options.Products);
        return AccountingResult<SimulationOptions>.Ok(state.Options);
    }

    private async Task ReceiveAsync(RunState state, Receipt receipt, CancellationToken token)
    {
        state.ReceiptNumber++;
        for (var i = 0; i < receipt.Quantity; i++)
        {
            var lotPosition = (int)(state.Purchased % LotSize);
            if (lotPosition == 0)
                state.LotColor = state.Faker.Random.Int(0, state.Models[(int)(state.Purchased / LotSize % state.Models.Count)]
                    .Variants.Length / SizeCodes.Length - 1);
            var modelIndex = (int)(state.Purchased / LotSize % state.Models.Count);
            var model = state.Models[modelIndex];
            var variant = model.Variants[state.LotColor * SizeCodes.Length + SizeRun[lotPosition]];
            state.Purchased++;
            if (modelIndex % 5 != 4) state.DominantPurchased++;
            var reference = $"SIM-REC-{state.ReceiptNumber:D6}";
            AddMovement(state, variant, state.CommercialWarehouse, 1, model.Cost, receipt.Date,
                StockMovementType.PurchaseReceipt, StockMovementReason.SupplierReceipt, reference);
            AddStock(state.Commercial, variant, state.CommercialWarehouse, model, 1);
            // Two returns and one quarantined defective unit per complete hundred purchased units.
            var position = state.Purchased % 100;
            if (position is 98 or 99)
            {
                state.Commercial[variant].Quantity--;
                state.Returned++;
                AddMovement(state, variant, state.CommercialWarehouse, -1, model.Cost, receipt.Date,
                    StockMovementType.InventoryAdjustment, StockMovementReason.SupplierReturn, reference);
            }
            else if (position == 0)
            {
                state.Commercial[variant].Quantity--;
                state.Defects++;
                AddStock(state.Defective, variant, state.DefectiveWarehouse, model, 1);
                AddMovement(state, variant, state.CommercialWarehouse, -1, model.Cost, receipt.Date,
                    StockMovementType.InventoryAdjustment, StockMovementReason.InternalTransfer, reference);
                AddMovement(state, variant, state.DefectiveWarehouse, 1, model.Cost, receipt.Date,
                    StockMovementType.InventoryAdjustment, StockMovementReason.InternalTransfer, reference);
            }
            await FlushIfNeededAsync(state.Options.BatchSize, token);
        }
    }

    private async Task<AccountingResult<SimulationOptions>> ClearYearAsync(
        RunState state, int year, CancellationToken token)
    {
        var residual = state.Commercial.Values.Sum(stock => stock.Quantity);
        // Oldest stock is liquidated first (see SellAsync); the carried share stays on the shelves.
        var result = await SellAsync(state, residual * (100 - state.Options.YearEndCarryoverPercent) / 100,
            new DateOnly(year, 12, 31), true, token);
        if (!result.Success)
            return result;
        state.PurchasedAtClear = state.Purchased;
        state.RegularSoldAtClear = state.RegularSold;
        return result;
    }

    private async Task<AccountingResult<SimulationOptions>> SellAsync(
        RunState state, long quantity, DateOnly date, bool liquidation, CancellationToken token)
    {
        var remaining = quantity;
        var lines = new List<(Stock Stock, int Quantity)>();
        foreach (var stock in state.Commercial.Values)
        {
            if (remaining == 0) break;
            if (stock.Quantity == 0) continue;
            var units = (int)Math.Min(stock.Quantity, remaining);
            lines.Add((stock, units));
            remaining -= units;
            if (lines.Count >= state.LinesPerDocument)
            {
                var result = await SaveSaleAsync(state, lines, date, liquidation, token);
                if (!result.Success) return result;
                lines.Clear();
            }
        }
        if (lines.Count > 0)
        {
            var result = await SaveSaleAsync(state, lines, date, liquidation, token);
            if (!result.Success) return result;
        }
        return AccountingResult<SimulationOptions>.Ok(state.Options);
    }

    private async Task<AccountingResult<SimulationOptions>> SaveSaleAsync(
        RunState state, List<(Stock Stock, int Quantity)> lines, DateOnly date,
        bool liquidation, CancellationToken token)
    {
        var calculated = accounting.Calculate(lines.Select(line => new DocumentLineInput(
            line.Quantity, line.Stock.Price, 22m, DiscountPercent: liquidation ? 50m : 0m)));
        if (!calculated.Success)
            return AccountingResult<SimulationOptions>.Fail(calculated.Error);
        var customerIndex = liquidation
            ? state.Options.ActiveCustomers - state.Stockists + state.ClearanceNumber++ % state.Stockists
            : state.SaleNumber++ % (state.Options.ActiveCustomers - state.Stockists);
        var customer = state.Customers[customerIndex];
        var number = ++state.Documents;
        var documentDate = Utc(date).AddTicks(++state.EventSequence * 10);
        var order = new Order(customer, $"SIM-ORD-{number:D8}") { OrderDate = documentDate };
        Track(order, date);
        var ddt = new DdtDocument(customer, $"SIM-DDT-{number:D8}") { DdtDate = documentDate, OrderId = order.Id };
        var invoice = new Invoice(customer, $"SIM-INV-{number:D8}") { InvoiceDate = documentDate, OrderId = order.Id };
        Track(ddt, date);
        Track(invoice, date);
        for (var i = 0; i < lines.Count; i++)
        {
            var (stock, units) = lines[i];
            var fiscal = calculated.Data!.Lines[i];
            var orderItem = new OrderItem(order.Id, stock.VariantId, units, fiscal.UnitNetPrice, fiscal.VatRate,
                discountPercent: fiscal.DiscountPercent) { LineNetTotal = fiscal.LineNetTotal };
            var ddtItem = new DdtItem(ddt.Id, stock.VariantId, units, fiscal.UnitNetPrice, fiscal.VatRate,
                discountPercent: fiscal.DiscountPercent) { LineNetTotal = fiscal.LineNetTotal };
            var invoiceItem = new InvoiceItem(invoice.Id, stock.VariantId, units, fiscal.UnitNetPrice, fiscal.VatRate,
                discountPercent: fiscal.DiscountPercent) { LineNetTotal = fiscal.LineNetTotal };
            Track(orderItem, date);
            Track(ddtItem, date);
            Track(invoiceItem, date);
            stock.Quantity -= units;
            if (liquidation) state.Liquidated += units;
            else state.RegularSold += units;
            AddMovement(state, stock.VariantId, stock.WarehouseId, -units, stock.Cost, date,
                StockMovementType.SalesDelivery, StockMovementReason.OrderShipment, ddt.DdtNumber);
        }
        var totals = calculated.Data!;
        order.TaxableTotal = ddt.TaxableTotal = invoice.TaxableTotal = totals.TotalTaxable;
        order.VatTotal = ddt.VatTotal = invoice.VatTotal = totals.TotalVat;
        order.GrossTotal = ddt.GrossTotal = invoice.GrossTotal = totals.TotalGross;
        order.Terminate();
        Stamp(order, date);
        state.CustomersWithMovements.Add(customerIndex);
        await FlushIfNeededAsync(state.Options.BatchSize, token);
        return AccountingResult<SimulationOptions>.Ok(state.Options);
    }

    private void AddMovement(RunState state, Guid variant, Guid warehouse, int delta,
        decimal cost, DateOnly date, StockMovementType type, StockMovementReason reason, string reference)
    {
        // Keep the legacy quantity signed for reconciliation; the constructor persists the same ledger delta.
        var movement = new StockMovement(variant, warehouse, reason, delta, documentReference: reference)
        {
            Type = type,
            Quantity = delta,
            UnitCost = cost
        };
        Track(movement, date);
        db.Entry(movement).Property(item => item.MovementDateUtc).CurrentValue =
            Utc(date).AddTicks(++state.EventSequence * 10);
        state.Movements++;
    }

    private static void AddStock(Dictionary<Guid, Stock> stocks, Guid variant, Guid warehouse, Model model, int units)
    {
        if (!stocks.TryGetValue(variant, out var stock))
            stocks.Add(variant, stock = new Stock(variant, warehouse, model.Cost, model.Price));
        stock.Quantity += units;
    }

    private void Track<T>(T entity, DateOnly date) where T : Entity<Guid>
    {
        db.Entry(entity).Property(item => item.Id).CurrentValue = Guid.NewGuid();
        db.Add(entity);
        pendingEntities += entity is Contact ? 2 : 1;
        Stamp(entity, date);
    }

    private void Stamp<T>(T entity, DateOnly date) where T : Entity<Guid>
    {
        db.Entry(entity).Property(item => item.CreatedAt).CurrentValue = Utc(date);
        db.Entry(entity).Property(item => item.UpdatedAt).CurrentValue = Utc(date);
    }

    private async Task FlushIfNeededAsync(int batchSize, CancellationToken token)
    {
        if (pendingEntities >= batchSize)
            await FlushAsync(token);
    }

    private async Task FlushAsync(CancellationToken token)
    {
        await db.SaveChangesAsync(token);
        db.ChangeTracker.Clear();
        pendingEntities = 0;
    }

    private static DateTime Utc(DateOnly date) => date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    private static AccountingResult<SimulationSummary> Fail(string error) =>
        AccountingResult<SimulationSummary>.Fail(error);

    private static List<Receipt> PlanReceipts(DateOnly start, SimulationOptions options)
    {
        var result = new List<Receipt> { new(start, options.InitialProducts * LotSize) };
        var random = new Randomizer(options.Seed);
        for (var day = start.DayNumber + 1; day <= options.EndDate.DayNumber; day += 7)
        {
            var remainingDays = Math.Min(7, options.EndDate.DayNumber - day + 1);
            var receipts = Math.Min(remainingDays, random.Int(2, 3));
            var units = random.Int(3, 6) * 100 * options.WeeklyPiecesFactor;
            for (var i = 0; i < receipts; i++)
                result.Add(new Receipt(DateOnly.FromDayNumber(day + i * remainingDays / receipts),
                    units / receipts + (i < units % receipts ? 1 : 0)));
        }
        return result;
    }

    private sealed record Receipt(DateOnly Date, int Quantity);
    private sealed record Model(Guid[] Variants, decimal Cost, decimal Price);
    private sealed class Stock(Guid variantId, Guid warehouseId, decimal cost, decimal price)
    {
        public Guid VariantId { get; } = variantId;
        public Guid WarehouseId { get; } = warehouseId;
        public decimal Cost { get; } = cost;
        public decimal Price { get; } = price;
        public int Quantity { get; set; }
    }

    private sealed class RunState(SimulationOptions options, Faker faker, int dominantCount, long purchased)
    {
        public SimulationOptions Options { get; } = options;
        public Faker Faker { get; } = faker;
        public int DominantCount { get; } = dominantCount;
        public int Stockists { get; } = Math.Min(10, Math.Max(1,
            Math.Min(options.ActiveCustomers / 100, options.InitialProducts * 7 / 100)));
        public int LinesPerDocument { get; } = (int)Math.Min(10, Math.Max(1, purchased * 9 / 100 / options.ActiveCustomers));
        public List<Guid> Agents { get; } = [];
        public List<Guid> Suppliers { get; } = [];
        public List<Guid> Customers { get; } = [];
        public List<Model> Models { get; } = [];
        public Dictionary<Guid, Stock> Commercial { get; } = [];
        public Dictionary<Guid, Stock> Defective { get; } = [];
        public HashSet<int> CustomersWithMovements { get; } = [];
        public Guid CommercialWarehouse { get; set; }
        public Guid DefectiveWarehouse { get; set; }
        public long Purchased, DominantPurchased, Returned, Defects, RegularSold, Liquidated, Movements;
        // Year-end clearance empties the shelves: later regular sales only draw on goods received afterwards.
        public long PurchasedAtClear, RegularSoldAtClear, LagUnits;
        public int LagIndex, LotColor;
        public long EventSequence;
        public int Variants, ReceiptNumber, SaleNumber, ClearanceNumber, Documents;
    }
}
