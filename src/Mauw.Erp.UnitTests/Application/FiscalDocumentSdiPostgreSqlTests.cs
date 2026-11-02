using System.Text;
using Mauw.Erp.Application.Commands.CreditNotes;
using Mauw.Erp.Application.Commands.FiscalDocuments;
using Mauw.Erp.Application.Commands.Invoices;
using Mauw.Erp.Application.Mappers;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Dtos.Pdf;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Domain.Accounting;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class FiscalDocumentSdiPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task MixedBatchTransmitsAndFreezesInvoiceAndCreditNoteXmlIndependently()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedDocumentsAsync(database);
        await using var transmissionDb = database.CreateDbContext();
        var harness = CreateBatchHarness(transmissionDb, seed, failCreditNote: false);

        var result = await harness.Batch.Handle(
            new BulkTransmitFiscalDocumentsCommand(
            [
                new(seed.InvoiceId, "Invoice"),
                new(seed.CreditNoteId, "CreditNote")
            ]),
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.Data!.ProcessedCount);
        Assert.Equal(2, result.Data.SuccessCount);
        Assert.Equal(0, result.Data.FailedCount);
        Assert.Equal(["Invoice", "CreditNote"],
            result.Data.Results.Select(item => item.DocumentType).ToArray());
        Assert.NotEqual(
            SdiFileNameBuilder.Build(seed.InvoiceXml).Data,
            SdiFileNameBuilder.Build(seed.CreditNoteXml).Data);

        await using (var verify = database.CreateDbContext())
        {
            var invoice = await verify.Invoices.SingleAsync(item => item.Id == seed.InvoiceId);
            Assert.Equal(SdiStatus.Transmitted, invoice.SdiStatus);
            Assert.NotNull(invoice.SdiStorageKey);
            Assert.NotNull(invoice.SdiFileName);
            Assert.NotNull(invoice.SdiTransmittedAtUtc);

            var note = await verify.CreditNotes.SingleAsync(item => item.Id == seed.CreditNoteId);
            Assert.Equal(SdiStatus.Transmitted, note.SdiStatus);
            Assert.NotNull(note.SdiStorageKey);
            Assert.NotNull(note.SdiFileName);
            Assert.NotNull(note.SdiTransmittedAtUtc);

            var audit = await verify.DocumentAuditEntries.OrderBy(item => item.DocumentType).ToListAsync();
            Assert.Equal(2, audit.Count);
            Assert.Contains(audit, item =>
                item.DocumentType == AuditDocumentTypes.Invoice &&
                item.DocumentId == seed.InvoiceId &&
                item.Action == AuditActions.InvoiceTransmittedToSdi);
            Assert.Contains(audit, item =>
                item.DocumentType == AuditDocumentTypes.CreditNote &&
                item.DocumentId == seed.CreditNoteId &&
                item.Action == AuditActions.CreditNoteTransmittedToSdi);
        }

        var storage = harness.Storage.Object;
        await using (var verify = database.CreateDbContext())
        {
            var invoice = await verify.Invoices.SingleAsync(item => item.Id == seed.InvoiceId);
            var note = await verify.CreditNotes.SingleAsync(item => item.Id == seed.CreditNoteId);
            var invoiceService = new InvoiceService(
                new UnitOfWork(verify, new MemoryCache(new MemoryCacheOptions())),
                new ItalianAccountingCalculator(),
                Mock.Of<ICompanySettingsService>(),
                Mock.Of<IInvoicePdfGenerator>(),
                storage);

            var invoiceXml = await invoiceService.GetInvoiceXmlDownloadAsync(invoice.Id);
            var noteXml = await invoiceService.GetCreditNoteXmlDownloadAsync(note.Id);

            Assert.True(invoiceXml.Success, invoiceXml.Error);
            Assert.True(noteXml.Success, noteXml.Error);
            Assert.Equal(seed.InvoiceXmlBytes, invoiceXml.Data!.Content);
            Assert.Equal(seed.CreditNoteXmlBytes, noteXml.Data!.Content);
            Assert.Equal(invoice.SdiFileName, invoiceXml.Data.FileName);
            Assert.Equal(note.SdiFileName, noteXml.Data.FileName);
        }

        harness.Storage.Verify(storage => storage.GetInvoiceXmlAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        harness.Storage.Verify(storage => storage.GetCreditNoteXmlAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreditNoteFailureDoesNotRollBackTransmittedInvoiceInTheSameBatch()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedDocumentsAsync(database);
        await using var transmissionDb = database.CreateDbContext();
        var harness = CreateBatchHarness(transmissionDb, seed, failCreditNote: true);

        var result = await harness.Batch.Handle(
            new BulkTransmitFiscalDocumentsCommand(
            [
                new(seed.InvoiceId, "Invoice"),
                new(seed.CreditNoteId, "CreditNote")
            ]),
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, result.Data!.SuccessCount);
        Assert.Equal(1, result.Data.FailedCount);
        Assert.True(result.Data.Results[0].Success);
        Assert.False(result.Data.Results[1].Success);
        harness.Storage.Verify(storage => storage.DeleteCreditNoteXmlAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        await using var verify = database.CreateDbContext();
        var invoice = await verify.Invoices.SingleAsync(item => item.Id == seed.InvoiceId);
        var note = await verify.CreditNotes.SingleAsync(item => item.Id == seed.CreditNoteId);
        Assert.Equal(SdiStatus.Transmitted, invoice.SdiStatus);
        Assert.NotNull(invoice.SdiStorageKey);
        Assert.Equal(SdiStatus.NotTransmitted, note.SdiStatus);
        Assert.Null(note.SdiStorageKey);
        Assert.Single(await verify.DocumentAuditEntries.ToListAsync());
    }

    [Fact]
    public async Task DatabaseFailureAfterProviderSuccessDeletesUploadedCreditNoteXml()
    {
        var database = postgres.CreateDatabase();
        var seed = await SeedDocumentsAsync(database);
        await using var transmissionDb = database.CreateDbContext(new FailingSaveChangesInterceptor());
        var harness = CreateBatchHarness(transmissionDb, seed, failCreditNote: false);

        var result = await harness.Batch.Handle(
            new BulkTransmitFiscalDocumentsCommand(
                [new(seed.CreditNoteId, "CreditNote")]),
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(0, result.Data!.SuccessCount);
        Assert.Equal(1, result.Data.FailedCount);
        harness.Storage.Verify(storage => storage.UploadCreditNoteXmlAsync(
            It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        harness.Storage.Verify(storage => storage.DeleteCreditNoteXmlAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        await using var verify = database.CreateDbContext();
        var note = await verify.CreditNotes.SingleAsync(item => item.Id == seed.CreditNoteId);
        Assert.Equal(SdiStatus.NotTransmitted, note.SdiStatus);
        Assert.Null(note.SdiStorageKey);
        Assert.Empty(await verify.DocumentAuditEntries.ToListAsync());
    }

    private static BatchHarness CreateBatchHarness(
        MauwDbContext db,
        Seed seed,
        bool failCreditNote)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var unitOfWork = new UnitOfWork(db, cache);
        var invoiceService = new Mock<IInvoiceService>(MockBehavior.Strict);
        invoiceService.Setup(service => service.GetInvoiceXmlAsync(seed.InvoiceId))
            .ReturnsAsync(OperationResult<string>.Ok(seed.InvoiceXml));
        invoiceService.Setup(service => service.GetCreditNoteXmlAsync(seed.CreditNoteId))
            .ReturnsAsync(OperationResult<string>.Ok(seed.CreditNoteXml));

        var storage = new Mock<ISdiDocumentStorage>(MockBehavior.Strict);
        storage.Setup(service => service.UploadInvoiceXmlAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string fileName, byte[] _, CancellationToken _) =>
                $"invoices/2026/11/{fileName}");
        storage.Setup(service => service.UploadCreditNoteXmlAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string fileName, byte[] _, CancellationToken _) =>
                $"credit-notes/2026/11/{fileName}");
        storage.Setup(service => service.DeleteInvoiceXmlAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        storage.Setup(service => service.DeleteCreditNoteXmlAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        storage.Setup(service => service.GetInvoiceXmlAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seed.InvoiceXmlBytes);
        storage.Setup(service => service.GetCreditNoteXmlAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seed.CreditNoteXmlBytes);

        var provider = new Mock<ISdiTransmissionProvider>(MockBehavior.Strict);
        provider.Setup(service => service.TransmitAsync(
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string fileName, byte[] content, CancellationToken _) =>
                failCreditNote && content.SequenceEqual(seed.CreditNoteXmlBytes)
                    ? new SdiTransmissionResult(false, null, "Provider SDI non disponibile.")
                    : new SdiTransmissionResult(true, $"SDI-{fileName}", null));

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserId).Returns("test-user");
        currentUser.SetupGet(service => service.FullName).Returns("Test User");
        currentUser.SetupGet(service => service.Email).Returns("test@example.invalid");

        var invoiceHandler = new TransmitInvoiceToSdiCommandHandler(
            unitOfWork, invoiceService.Object, storage.Object, provider.Object, currentUser.Object);
        var creditNoteHandler = new TransmitCreditNoteToSdiCommandHandler(
            unitOfWork, invoiceService.Object, storage.Object, provider.Object, currentUser.Object);
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(value => value.Send(
                It.IsAny<IRequest<OperationResult<InvoiceDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((IRequest<OperationResult<InvoiceDto>> request, CancellationToken token) =>
                invoiceHandler.Handle((TransmitInvoiceToSdiCommand)request, token));
        sender.Setup(value => value.Send(
                It.IsAny<IRequest<OperationResult<CreditNoteSummaryDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((IRequest<OperationResult<CreditNoteSummaryDto>> request, CancellationToken token) =>
                creditNoteHandler.Handle((TransmitCreditNoteToSdiCommand)request, token));

        return new BatchHarness(
            new BulkTransmitFiscalDocumentsCommandHandler(
                sender.Object, NullLogger<BulkTransmitFiscalDocumentsCommandHandler>.Instance),
            storage);
    }

    private static async Task<Seed> SeedDocumentsAsync(PostgreSqlTestDatabase database)
    {
        await using var db = database.CreateDbContext();
        var contact = new Contact("Cliente SDI misto");
        SetId(contact);
        var product = new Product(
            Guid.NewGuid(), "FORN", $"MIX-{Guid.NewGuid():N}", "Articolo prova", 22m, 5m);
        SetId(product);
        var variant = new ProductVariant(
            product.Id, $"MIX-BC-{Guid.NewGuid():N}", 5m, product.SupplierId)
        {
            Product = product
        };
        SetId(variant);
        var invoice = new Invoice(contact.Id, "FAT-202611-00001")
        {
            Contact = contact,
            InvoiceDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        SetId(invoice);
        var invoiceItem = new InvoiceItem(invoice.Id, variant.Id, 1m, 10m, 22m)
        {
            Variant = variant
        };
        SetId(invoiceItem);
        Assert.True(invoice.AddItem(invoiceItem, new ItalianAccountingCalculator()).Success);
        Assert.True(invoice.Confirm(new ItalianAccountingCalculator()).Success);

        var creditNote = new CreditNote(
            invoice, "NC-202611-00001", "GoodsReturn", false, null,
            new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc))
        {
            TaxableTotal = 10m,
            VatTotal = 2.2m,
            GrossTotal = 12.2m
        };
        creditNote.Items.Add(new CreditNoteItem(
            creditNote,
            invoiceItem.Id,
            variant.Id,
            "Articolo prova",
            false,
            1m,
            10m,
            10m,
            22m,
            null,
            0m,
            invoiceItem,
            variant));

        db.AddRange(contact, product, variant, invoice, creditNote);
        await db.SaveChangesAsync();

        var invoiceXml = CreateInvoiceXml(invoice);
        var creditNoteXml = CreateCreditNoteXml(creditNote, invoice);
        return new Seed(
            invoice.Id,
            creditNote.Id,
            invoice.InvoiceNumber,
            creditNote.CreditNoteNumber,
            invoiceXml,
            creditNoteXml,
            Encoding.UTF8.GetBytes(invoiceXml),
            Encoding.UTF8.GetBytes(creditNoteXml));
    }

    private static string CreateInvoiceXml(Invoice invoice)
    {
        var result = FatturaPaInvoiceXmlBuilder.Build(
            CreateModel(invoice.InvoiceNumber, "TD01", 12.2m), new ItalianAccountingCalculator());
        Assert.True(result.Success, result.Error);
        Assert.Empty(FatturaPaSchemaValidator.Validate(result.Data!));
        return result.Data!;
    }

    private static string CreateCreditNoteXml(CreditNote note, Invoice invoice)
    {
        var result = FatturaPaInvoiceXmlBuilder.Build(
            note,
            invoice,
            CreateModel(note.CreditNoteNumber, "TD01", 12.2m),
            new ItalianAccountingCalculator());
        Assert.True(result.Success, result.Error);
        Assert.Empty(FatturaPaSchemaValidator.Validate(result.Data!));
        return result.Data!;
    }

    private static InvoicePdfModel CreateModel(string number, string type, decimal total) => new(
        DocumentNumber: number,
        DocumentType: type,
        IssueDate: new DateOnly(2026, 11, 1),
        Currency: "EUR",
        Seller: new InvoicePdfSellerDetails(
            "Mauw S.r.l.", "IT01234567890", "01234567890", "Via Roma 1", "Bologna", "40100", "BO",
            "BO-591450", "10.000,00", "RF01 - Ordinario", "KRRH6B9", "mauw@pec.example.test",
            IsShareCapitalFullyPaid: true, ReaOffice: "BO", IsSoleShareholder: true, IsInLiquidation: false),
        Customer: new InvoicePdfCustomerDetails(
            "Cliente Prova S.r.l.", "IT09876543210", null, "Via Milano 2", "Milano", "20100", "MI",
            "0000000", "cliente@pec.example.test"),
        Payment: new InvoicePdfPaymentInfo(
            "A vista", "MP01", new DateOnly(2026, 11, 1),
            "IT60X0542811101000000123456", "BPITITMM", "Banca Prova", total),
        Lines: [new InvoicePdfLineModel("SKU-1", "Articolo prova", null, 1m, 10m, null, 22m, 10m)],
        VatSummary: [new InvoiceVatBreakdownModel(22m, 10m, 2.2m)],
        Totals: new InvoicePdfTotals(1m, 10m, 2.2m, total));

    private static void SetId(Entity<Guid> entity) =>
        typeof(Entity<Guid>).GetProperty(nameof(Entity<Guid>.Id))!
            .SetValue(entity, Guid.NewGuid());

    private sealed record BatchHarness(
        BulkTransmitFiscalDocumentsCommandHandler Batch,
        Mock<ISdiDocumentStorage> Storage);

    private sealed record Seed(
        Guid InvoiceId,
        Guid CreditNoteId,
        string InvoiceNumber,
        string CreditNoteNumber,
        string InvoiceXml,
        string CreditNoteXml,
        byte[] InvoiceXmlBytes,
        byte[] CreditNoteXmlBytes);

    private sealed class FailingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult<int>>(
                new DbUpdateException("Injected database write failure."));
    }
}
