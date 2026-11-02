using Mauw.Erp.Application.Commands.CreditNotes;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class CreditNotePostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task RejectsIssuanceWhenOriginInvoiceHasNotBeenTransmitted()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedInvoiceAsync(database, transmitted: false, [new(2m, 10m, 22m)]);

        await using var db = database.CreateDbContext();
        var result = await CreateHandler(db).Handle(
            new CreateCreditNoteCommand(Request(seed, [(seed.InvoiceItemIds[0], 1m)])),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("trasmesse o accettate", result.Error, StringComparison.OrdinalIgnoreCase);
        await using var verify = database.CreateDbContext();
        Assert.Empty(await verify.CreditNotes.ToListAsync());
        Assert.Empty(await verify.DocumentAuditEntries.ToListAsync());
        Assert.Empty(await verify.StockMovements.ToListAsync());
    }

    [Fact]
    public async Task IssuingWithPhysicalReturnIncrementsStockAndRecordsCustomerReturn()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedInvoiceAsync(database, transmitted: true, [new(2m, 10m, 22m)], initialStock: 4m);

        await using (var db = database.CreateDbContext())
        {
            var result = await CreateHandler(db).Handle(
                new CreateCreditNoteCommand(
                    Request(seed, [(seed.InvoiceItemIds[0], 1m)], returnToWarehouse: true)),
                CancellationToken.None);

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.Data!.CourtesyPdf);
            Assert.Equal($"NC-{DateTime.UtcNow:yyyyMM}-00001", result.Data.CreditNote.CreditNoteNumber);
            Assert.Equal("NotTransmitted", result.Data.CreditNote.SdiStatus);
        }

        await using var verify = database.CreateDbContext();
        Assert.Equal(5m, await verify.StockItems
            .Where(item => item.ProductVariantId == seed.VariantId)
            .Select(item => item.QuantityOnHand)
            .SingleAsync());
        var movement = Assert.Single(await verify.StockMovements.ToListAsync());
        Assert.Equal(StockMovementType.CustomerReturn, movement.Type);
        Assert.Equal(StockMovementReason.CustomerReturn, movement.Reason);
        Assert.Equal(1m, movement.QuantityDelta);
        Assert.Equal(seed.WarehouseId, movement.WarehouseId);
        var note = await verify.CreditNotes.Include(item => item.Items).SingleAsync();
        Assert.Equal(seed.InvoiceId, note.InvoiceId);
        Assert.Equal(note.CreditNoteNumber, movement.ReferenceDoc);
        var sourceInvoice = await verify.Invoices
            .Include(invoice => invoice.CreditNotes)
            .SingleAsync(invoice => invoice.Id == seed.InvoiceId);
        Assert.Equal(sourceInvoice.InvoiceNumber, note.RelatedInvoiceNumber);
        Assert.Equal(sourceInvoice.InvoiceDate, note.RelatedInvoiceDate);
        Assert.Single(InvoiceMapper.MapToDto(sourceInvoice).CreditNotes!);
        var audit = await verify.DocumentAuditEntries.SingleAsync();
        Assert.Equal("Invoice", audit.DocumentType);
        Assert.Equal(AuditActions.CreditNoteIssued, audit.Action);
        Assert.Equal(seed.InvoiceId, audit.DocumentId);
        Assert.Contains("12.20 EUR", audit.Details);
        Assert.Equal(1m, Assert.Single(note.Items).Quantity);
        Assert.Null(Assert.Single(note.Items).LineVatTotal);
        Assert.Null(Assert.Single(note.Items).LineGrossTotal);
    }

    [Fact]
    public async Task FinancialAllowanceDoesNotChangeWarehouseStock()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedInvoiceAsync(database, transmitted: true, [new(2m, 10m, 22m)], initialStock: 4m);

        await using (var db = database.CreateDbContext())
        {
            var result = await CreateHandler(db).Handle(
                new CreateCreditNoteCommand(Request(seed, [(seed.InvoiceItemIds[0], 1m)])),
                CancellationToken.None);

            Assert.True(result.Success, result.Error);
        }

        await using var verify = database.CreateDbContext();
        Assert.Equal(4m, await verify.StockItems
            .Where(item => item.ProductVariantId == seed.VariantId)
            .Select(item => item.QuantityOnHand)
            .SingleAsync());
        Assert.Empty(await verify.StockMovements.ToListAsync());
        Assert.False((await verify.CreditNotes.SingleAsync()).ReturnToWarehouse);
    }

    [Fact]
    public async Task SplitPaymentAllocatesCreditNoteAndPreventsReusingItsBalance()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedInvoiceAsync(database, transmitted: true,
        [
            new(1m, 60m, 0m, "N3.2"),
            new(1m, 440m, 0m, "N3.2")
        ]);
        Guid creditNoteId;

        await using (var issuanceDb = database.CreateDbContext())
        {
            var result = await CreateHandler(issuanceDb).Handle(
                new CreateCreditNoteCommand(Request(seed, [(seed.InvoiceItemIds[0], 1m)])),
                CancellationToken.None);

            Assert.True(result.Success, result.Error);
            Assert.Equal(60.00m, result.Data!.CreditNote.GrossTotal);
            creditNoteId = result.Data.CreditNote.Id;
        }

        await using (var paymentDb = database.CreateDbContext())
        {
            var unitOfWork = new UnitOfWork(paymentDb, new MemoryCache(new MemoryCacheOptions()));
            var creditNoteService = new CreditNoteService(unitOfWork, new ItalianAccountingCalculator());
            var available = await creditNoteService.GetAvailableForContactAsync(seed.ContactId);
            Assert.True(available.Success, available.Error);
            Assert.Equal(60.00m, Assert.Single(available.Data!).RemainingCredit);

            var result = await CreateInvoiceService(unitOfWork).RecordPaymentsAsync(seed.InvoiceId,
            [
                new()
                {
                    Amount = 60.00m,
                    PaymentMethod = nameof(PaymentMethod.CreditNoteOffset),
                    SourceCreditNoteId = creditNoteId
                },
                new() { Amount = 440.00m, PaymentMethod = "POS" }
            ]);

            Assert.True(result.Success, result.Error);
            Assert.Equal("Paid", result.Data!.Status);
            Assert.Equal(500.00m, result.Data.PaidAmount);
            Assert.Equal(0.00m, result.Data.BalanceDue);
            Assert.Equal(2, result.Data.Payments.Count);
            Assert.Equal(creditNoteId, result.Data.Payments[0].SourceCreditNoteId);
        }

        var secondInvoiceId = await CreateOpenInvoiceAsync(database, seed.ContactId, seed.VariantId);
        await using (var reuseDb = database.CreateDbContext())
        {
            var service = CreateInvoiceService(
                new UnitOfWork(reuseDb, new MemoryCache(new MemoryCacheOptions())));
            var result = await service.RecordPaymentsAsync(secondInvoiceId,
            [
                new()
                {
                    Amount = 1.00m,
                    PaymentMethod = nameof(PaymentMethod.CreditNoteOffset),
                    SourceCreditNoteId = creditNoteId
                }
            ]);

            Assert.False(result.Success);
            Assert.Contains("residuo insufficiente", result.Error, StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = database.CreateDbContext();
        var paidInvoice = await verify.Invoices.SingleAsync(invoice => invoice.Id == seed.InvoiceId);
        var creditNote = await verify.CreditNotes.SingleAsync(note => note.Id == creditNoteId);
        Assert.Equal(500.00m, paidInvoice.PaidAmount);
        Assert.Equal(0.00m, paidInvoice.GrossTotal - paidInvoice.PaidAmount);
        Assert.Equal(60.00m, creditNote.AllocatedAmount);
        Assert.Equal(0.00m, creditNote.RemainingCredit);
        Assert.Equal(2, await verify.Payments.CountAsync(payment => payment.InvoiceId == seed.InvoiceId));
        var invoiceDetails = await CreateInvoiceService(
            new UnitOfWork(verify, new MemoryCache(new MemoryCacheOptions())))
            .GetInvoiceAsync(seed.InvoiceId);
        Assert.True(invoiceDetails.Success, invoiceDetails.Error);
        Assert.Equal(creditNote.CreditNoteNumber, Assert.Single(
            invoiceDetails.Data!.Payments,
            payment => payment.SourceCreditNoteId == creditNoteId).SourceCreditNoteNumber);
        Assert.False((await new CreditNoteService(
            new UnitOfWork(verify, new MemoryCache(new MemoryCacheOptions())),
            new ItalianAccountingCalculator())
            .GetAvailableForContactAsync(seed.ContactId)).Data!.Any());
    }

    [Fact]
    public async Task PreviewCalculatesVatByRateAndNatureUsingCentralCalculator()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedInvoiceAsync(database, transmitted: true,
        [
            new(1m, 100m, 22m),
            new(1m, 100m, 10m),
            new(1m, 100m, 0m, "N3.2")
        ]);

        await using var db = database.CreateDbContext();
        var unitOfWork = new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions()));
        var service = new CreditNoteService(unitOfWork, new ItalianAccountingCalculator());
        var request = Request(seed, seed.InvoiceItemIds.Select(id => (id, 1m)).ToArray());
        var result = await service.PreviewAsync(request);

        Assert.True(result.Success, result.Error);
        Assert.Equal(300.00m, result.Data!.TaxableTotal);
        Assert.Equal(32.00m, result.Data.VatTotal);
        Assert.Equal(332.00m, result.Data.GrossTotal);
        Assert.Equal(3, result.Data.VatSummary.Count);
        Assert.Contains(result.Data.VatSummary, summary =>
            summary.VatRate == 22m && summary.NatureCode is null &&
            summary.TaxableAmount == 100m && summary.VatAmount == 22m);
        Assert.Contains(result.Data.VatSummary, summary =>
            summary.VatRate == 10m && summary.NatureCode is null &&
            summary.TaxableAmount == 100m && summary.VatAmount == 10m);
        Assert.Contains(result.Data.VatSummary, summary =>
            summary.VatRate == 0m && summary.NatureCode == "N3.2" &&
            summary.TaxableAmount == 100m && summary.VatAmount == 0m);
    }

    [Fact]
    public async Task ConcurrentIssuanceCannotCreditTheSameInvoiceQuantityTwice()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedInvoiceAsync(database, transmitted: true, [new(1m, 10m, 22m)]);

        var outcomes = await Task.WhenAll(
            IssueAsync(database, seed),
            IssueAsync(database, seed));

        Assert.Single(outcomes, outcome => outcome.Success);
        Assert.Single(outcomes, outcome => !outcome.Success);
        await using var verify = database.CreateDbContext();
        Assert.Single(await verify.CreditNotes.ToListAsync());
    }

    private static async Task<OperationResult<CreditNoteIssuedDto>> IssueAsync(
        PostgreSqlTestDatabase database,
        Seed seed)
    {
        await using var db = database.CreateDbContext();
        return await CreateHandler(db).Handle(
            new CreateCreditNoteCommand(Request(seed, [(seed.InvoiceItemIds[0], 1m)])),
            CancellationToken.None);
    }

    private static CreateCreditNoteCommandHandler CreateHandler(MauwDbContext db)
    {
        var unitOfWork = new UnitOfWork(db, new MemoryCache(new MemoryCacheOptions()));
        var invoiceService = new Mock<IInvoiceService>();
        invoiceService.Setup(service => service.GetInvoicePdfAsync(It.IsAny<Guid>()))
            .ReturnsAsync(OperationResult<byte[]>.Ok([1, 2, 3]));
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(service => service.UserId).Returns("test-user");
        user.SetupGet(service => service.Email).Returns("test@example.invalid");
        user.SetupGet(service => service.FullName).Returns("Test User");
        return new CreateCreditNoteCommandHandler(
            unitOfWork,
            new CreditNoteService(unitOfWork, new ItalianAccountingCalculator()),
            invoiceService.Object,
            user.Object);
    }

    private static InvoiceService CreateInvoiceService(UnitOfWork unitOfWork) =>
        new(
            unitOfWork,
            new ItalianAccountingCalculator(),
            Mock.Of<ICompanySettingsService>(),
            Mock.Of<IInvoicePdfGenerator>(),
            Mock.Of<ISdiDocumentStorage>());

    private static async Task<Guid> CreateOpenInvoiceAsync(
        PostgreSqlTestDatabase database,
        Guid contactId,
        Guid variantId)
    {
        await using var db = database.CreateDbContext();
        var invoice = new Invoice(contactId, $"FAT-{Guid.NewGuid():N}");
        var calculator = new ItalianAccountingCalculator();
        Assert.True(invoice.AddItem(
            new(invoice.Id, variantId, 1m, 100m, 0m, "N3.2"), calculator).Success);
        Assert.True(invoice.Confirm(calculator).Success);
        await db.Invoices.AddAsync(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private static CreateCreditNoteRequest Request(
        Seed seed,
        IReadOnlyList<(Guid ItemId, decimal Quantity)> lines,
        bool returnToWarehouse = false) =>
        new(
            seed.InvoiceId,
            CreditNoteReasons.GoodsReturn,
            lines.Select(line => new CreateCreditNoteLineRequest(line.ItemId, false, line.Quantity)).ToArray(),
            returnToWarehouse,
            returnToWarehouse ? seed.WarehouseId : null);

    private static async Task<Seed> SeedInvoiceAsync(
        PostgreSqlTestDatabase database,
        bool transmitted,
        IReadOnlyList<InvoiceLineSeed> lines,
        decimal initialStock = 0m)
    {
        await using var db = database.CreateDbContext();
        var contact = new Contact($"Cliente {Guid.NewGuid():N}");
        SetId(contact);
        var address = new ContactAddress(
            contact.Id, AddressPurpose.WarehouseSite, "Via Test 1", "20100", "Milano");
        SetId(address);
        var warehouse = new Warehouse(
            $"CN-{Guid.NewGuid():N}"[..16], "Magazzino nota di credito",
            WarehouseType.Standard, address.Id, isSellable: true, isDefault: true);
        SetId(warehouse);
        var product = new Product(contact.Id, $"CN-{Guid.NewGuid():N}", $"BC-{Guid.NewGuid():N}",
            "Articolo prova nota credito", 22m, 5m);
        SetId(product);
        var variant = new ProductVariant(product.Id, $"VAR-{Guid.NewGuid():N}", 5m, contact.Id)
        {
            Product = product
        };
        SetId(variant);
        var invoice = new Invoice(contact.Id, $"FAT-{Guid.NewGuid():N}")
        {
            Contact = contact
        };
        SetId(invoice);

        var calculator = new ItalianAccountingCalculator();
        foreach (var line in lines)
        {
            var item = new InvoiceItem(
                invoice.Id, variant.Id, line.Quantity, line.UnitNetPrice, line.VatRate, line.NatureCode)
            {
                Variant = variant
            };
            SetId(item);
            invoice.Items.Add(item);
        }

        Assert.True(invoice.Confirm(calculator).Success);
        if (transmitted)
            Assert.True(invoice.MarkAsTransmitted(
                "FAT00001.xml", "test/fat00001.xml", "TST-001", DateTime.UtcNow).Success);

        var stock = new StockItem(warehouse.Id, variant.Id, initialStock);
        SetId(stock);
        db.AddRange(contact, address, warehouse, product, variant, invoice, stock);
        await db.SaveChangesAsync();

        return new Seed(
            invoice.Id,
            contact.Id,
            warehouse.Id,
            variant.Id,
            invoice.Items.Select(item => item.Id).ToArray());
    }

    private static void SetId(Entity<Guid> entity) =>
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!
            .SetValue(entity, Guid.NewGuid());

    private sealed record Seed(
        Guid InvoiceId,
        Guid ContactId,
        Guid WarehouseId,
        Guid VariantId,
        Guid[] InvoiceItemIds);

    private sealed record InvoiceLineSeed(
        decimal Quantity,
        decimal UnitNetPrice,
        decimal VatRate,
        string? NatureCode = null);
}
