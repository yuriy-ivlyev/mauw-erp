using System.Text;
using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Interfaces;
using Mauw.Erp.Contracts.Requests;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Services;
using Mauw.Erp.Infrastructure.DbContext;
using Mauw.Erp.Infrastructure.Repositories;
using Mauw.Erp.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Mauw.Erp.UnitTests.Application;

[Collection(PostgreSqlTestCollection.Name)]
public sealed class PayableDocumentPostgreSqlTests(PostgreSqlContainerFixture postgres)
{
    [Fact]
    public async Task SupplierInvoiceCanBeApprovedPaidInPartsAndDoesNotMoveStock()
    {
        var database = postgres.CreateDatabase();
        Guid supplierId;
        await using (var seed = database.CreateDbContext())
        {
            var supplier = new Contact("Fornitore passivo test") { Status = ContactStatus.Active };
            supplier.EnableAsSupplier();
            seed.Contacts.Add(supplier);
            await seed.SaveChangesAsync();
            supplierId = supplier.Id;
        }

        await using var db = database.CreateDbContext();
        var storage = new MemoryPayableDocumentStorage();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(user => user.FullName).Returns("Operatore contabile");
        var service = Service(db, storage, currentUser.Object);
        var created = await service.CreateSupplierInvoiceAsync(
            InvoiceRequest(supplierId, "FT-UTENZE-001"),
            "accounting-user",
            CancellationToken.None);

        Assert.True(created.Success, created.Error);
        Assert.Equal(PayableDocumentStatus.Draft.ToString(), created.Data!.Status);
        Assert.Equal(122m, created.Data.GrossTotal);
        var duplicate = await service.CreateSupplierInvoiceAsync(
            InvoiceRequest(supplierId, "FT-UTENZE-001"),
            "accounting-user",
            CancellationToken.None);
        Assert.False(duplicate.Success);
        Assert.Contains("già registrata", duplicate.Error, StringComparison.OrdinalIgnoreCase);

        var updatedDraft = await service.UpdateDraftAsync(
            created.Data.Id,
            InvoiceRequest(supplierId, "FT-UTENZE-001") with
            {
                Notes = "Importo riportato dalla fattura originale.",
                Version = created.Data.Version
            },
            "accounting-user",
            canManageAll: true,
            CancellationToken.None);
        Assert.True(updatedDraft.Success, updatedDraft.Error);
        Assert.Equal("Importo riportato dalla fattura originale.", updatedDraft.Data!.Notes);

        var file = Encoding.ASCII.GetBytes("%PDF-1.4\ninvoice");
        var attachment = await service.UploadAttachmentAsync(
            created.Data.Id,
            updatedDraft.Data.Version,
            new MemoryStream(file),
            "bolletta.pdf",
            "application/pdf",
            file.Length,
            "accounting-user",
            canManageAll: true,
            CancellationToken.None);
        Assert.True(attachment.Success, attachment.Error);

        var submitted = await service.SubmitAsync(
            created.Data.Id,
            attachment.Data!.Version,
            "accounting-user",
            canManageAll: true,
            CancellationToken.None);
        Assert.True(submitted.Success, submitted.Error);
        var approved = await service.ApproveAsync(
            created.Data.Id,
            submitted.Data!.Version,
            "approver-user",
            CancellationToken.None);
        Assert.True(approved.Success, approved.Error);

        var partialPayment = await service.RecordPaymentAsync(
            created.Data.Id,
            new(approved.Data!.Version, 50m, new DateTime(2026, 11, 4), "Bonifico bancario", "CRO-1"),
            "accounting-user",
            CancellationToken.None);
        Assert.True(partialPayment.Success, partialPayment.Error);
        Assert.Equal(PayablePaymentStatus.PartiallyPaid.ToString(), partialPayment.Data!.PaymentStatus);
        Assert.Equal(72m, partialPayment.Data.BalanceDue);
        Assert.Equal(
            partialPayment.Data.Version,
            await db.PayableDocuments.AsNoTracking()
                .Where(document => document.Id == created.Data.Id)
                .Select(document => document.Version)
                .SingleAsync());
        db.ChangeTracker.Clear();

        var finalPayment = await service.RecordPaymentAsync(
            created.Data.Id,
            new(partialPayment.Data.Version, 72m, DateTime.UtcNow, "Bonifico bancario", "CRO-2"),
            "accounting-user",
            CancellationToken.None);
        Assert.True(finalPayment.Success, finalPayment.Error);
        Assert.Equal(PayablePaymentStatus.Paid.ToString(), finalPayment.Data!.PaymentStatus);
        Assert.Equal(0m, finalPayment.Data.BalanceDue);

        var persisted = await db.PayableDocuments
            .Include(document => document.Payments)
            .SingleAsync(document => document.Id == created.Data.Id);
        Assert.Equal(122m, persisted.GrossTotal);
        Assert.Equal(122m, persisted.PaidAmount);
        Assert.All(persisted.Payments, payment => Assert.Equal(DateTimeKind.Utc, payment.PaymentDateUtc.Kind));
        Assert.Equal(0, await db.StockMovements.CountAsync());
        Assert.Equal(7, await db.DocumentAuditEntries.CountAsync(
            entry => entry.DocumentType == AuditDocumentTypes.PayableDocument));
        Assert.Single(storage.UploadedKeys);
    }

    [Fact]
    public async Task ReimbursementIsPrivateToItsSubmitterAndHasNoSupplier()
    {
        var database = postgres.CreateDatabase();
        await using var db = database.CreateDbContext();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(user => user.FullName).Returns("Richiedente autenticato");
        var service = Service(db, new MemoryPayableDocumentStorage(), currentUser.Object);
        var request = new CreatePayableDocumentRequest(
            PayableDocumentKind.ExpenseReimbursement.ToString(),
            null,
            "RIMB-001",
            new DateOnly(2026, 11, 4),
            new DateOnly(2026, 11, 10),
            "Altro beneficiario",
            "Spese trasferta",
            [
                new(
                    "Trasferta cliente",
                    PayableExpenseCategory.Travel.ToString(),
                    80m,
                    null,
                    null,
                    0m,
                    80m,
                    "Vendite",
                    "Visite clienti")
            ]);

        var result = await service.CreateReimbursementAsync(request, "user-1", CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Null(result.Data!.SupplierId);
        Assert.Equal("Richiedente autenticato", result.Data.BeneficiaryName);
        Assert.Null(await service.GetAsync(result.Data.Id, "user-2", canManageAll: false, CancellationToken.None));
        Assert.NotNull(await service.GetAsync(result.Data.Id, "user-1", canManageAll: false, CancellationToken.None));
        Assert.NotNull(await service.GetAsync(result.Data.Id, null, canManageAll: true, CancellationToken.None));
    }

    private static PayableDocumentService Service(
        MauwDbContext db,
        IPayableDocumentStorage storage,
        ICurrentUserService currentUser) =>
        new(db, storage, new ItalianAccountingCalculator(), currentUser,
            NullLogger<PayableDocumentService>.Instance);

    private static CreatePayableDocumentRequest InvoiceRequest(Guid supplierId, string invoiceNumber) =>
        new(
            PayableDocumentKind.SupplierInvoice.ToString(),
            supplierId,
            invoiceNumber,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 12, 1),
            null,
            "Utenze",
            [
                new(
                    "Bolletta energia",
                    PayableExpenseCategory.Utilities.ToString(),
                    100m,
                    22m,
                    null,
                    22m,
                    122m,
                    "Sede",
                    null)
            ]);

    private sealed class MemoryPayableDocumentStorage : IPayableDocumentStorage
    {
        public List<string> UploadedKeys { get; } = [];

        public Task<string> UploadAsync(
            Guid id,
            Stream stream,
            string contentType,
            CancellationToken cancellationToken)
        {
            var key = $"payables/{id:N}/test.pdf";
            UploadedKeys.Add(key);
            return Task.FromResult(key);
        }

        public Task<(Stream Stream, string ContentType)> DownloadAsync(
            string key,
            CancellationToken cancellationToken) =>
            Task.FromResult<(Stream Stream, string ContentType)>((new MemoryStream(), "application/pdf"));

        public Task DeleteAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
