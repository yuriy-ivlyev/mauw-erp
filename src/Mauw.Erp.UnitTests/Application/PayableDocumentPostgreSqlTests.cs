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
    public async Task ReimbursementIsOwnedByEmployeeAndAdminOnly()
    {
        var database = postgres.CreateDatabase();
        Guid employeeId;
        await using (var seed = database.CreateDbContext())
        {
            var employee = NewEmployee();
            seed.Employees.Add(employee);
            await seed.SaveChangesAsync();
            employeeId = employee.Id;
        }

        await using var db = database.CreateDbContext();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(user => user.FullName).Returns("Amministratore");
        currentUser.Setup(user => user.HasRole("Admin")).Returns(true);
        var service = Service(db, new MemoryPayableDocumentStorage(), currentUser.Object);
        var request = new CreatePayableDocumentRequest(
            PayableDocumentKind.ExpenseReimbursement.ToString(),
            null,
            "RIMB-001",
            new DateOnly(2026, 11, 4),
            new DateOnly(2026, 11, 10),
            "Nome ignorato dal backend",
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
            ],
            EmployeeId: employeeId);

        var result = await service.CreateReimbursementAsync(request, "admin-user", CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Null(result.Data!.SupplierId);
        Assert.Equal(employeeId, result.Data.EmployeeId);
        Assert.Equal("Mario Rossi", result.Data.BeneficiaryName);
        var employeeDetails = await db.Employees.AsNoTracking().SingleAsync(item => item.Id == employeeId);
        Assert.Equal("Via Roma 1", employeeDetails.StreetAddress);
        Assert.Equal("mario.rossi@example.it", employeeDetails.Email);
        var accountant = new Mock<ICurrentUserService>();
        accountant.Setup(user => user.HasRole("Admin")).Returns(false);
        var accountingService = Service(db, new MemoryPayableDocumentStorage(), accountant.Object);
        Assert.Null(await accountingService.GetAsync(
            result.Data.Id, "accounting-user", canManageAll: true, CancellationToken.None));
        Assert.NotNull(await service.GetAsync(result.Data.Id, null, canManageAll: true, CancellationToken.None));
    }

    [Fact]
    public async Task EmployeePayrollCostIsMonthlyNonPayableAndVisibleOnlyToAdmin()
    {
        var database = postgres.CreateDatabase();
        Guid employeeId;
        await using (var seed = database.CreateDbContext())
        {
            var employee = NewEmployee();
            seed.Employees.Add(employee);
            await seed.SaveChangesAsync();
            employeeId = employee.Id;
        }

        await using var db = database.CreateDbContext();
        var admin = new Mock<ICurrentUserService>();
        admin.Setup(user => user.FullName).Returns("Amministratore");
        admin.Setup(user => user.HasRole("Admin")).Returns(true);
        var adminService = Service(db, new MemoryPayableDocumentStorage(), admin.Object);
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\npayroll");
        var created = await adminService.CreateEmployeePayrollCostAsync(
            employeeId,
            new(new DateOnly(2026, 11, 1), 3_250.75m, "Costo complessivo mensile"),
            new MemoryStream(pdf),
            "cedolino.pdf",
            "application/pdf",
            pdf.Length,
            "admin-user",
            CancellationToken.None);

        Assert.True(created.Success, created.Error);
        Assert.Equal(PayableDocumentKind.EmployeePayroll.ToString(), created.Data!.Kind);
        Assert.Equal("2026-11", created.Data.SourceDocumentNumber);
        Assert.Equal(3_250.75m, created.Data.GrossTotal);
        Assert.Equal(PayablePaymentStatus.NotPayable.ToString(), created.Data.PaymentStatus);
        Assert.Equal(0m, created.Data.BalanceDue);
        Assert.Equal("Mario Rossi", created.Data.BeneficiaryName);
        Assert.Equal(PayableDocumentStatus.Approved.ToString(), created.Data.Status);
        Assert.Equal("cedolino.pdf", created.Data.AttachmentFileName);
        var duplicate = await adminService.CreateEmployeePayrollCostAsync(
            employeeId,
            new(new DateOnly(2026, 11, 1), 3_250.75m, null),
            new MemoryStream(pdf),
            "cedolino.pdf",
            "application/pdf",
            pdf.Length,
            "admin-user",
            CancellationToken.None);
        Assert.False(duplicate.Success);
        Assert.Contains("già registrato", duplicate.Error, StringComparison.OrdinalIgnoreCase);

        var payment = await adminService.RecordPaymentAsync(
            created.Data.Id,
            new(created.Data.Version, 100m, DateTime.UtcNow, "Bonifico bancario", null),
            "admin-user",
            CancellationToken.None);
        Assert.False(payment.Success);
        Assert.Contains("non gestisce pagamenti", payment.Error, StringComparison.OrdinalIgnoreCase);

        var accounting = new Mock<ICurrentUserService>();
        accounting.Setup(user => user.HasRole("Admin")).Returns(false);
        var accountingService = Service(db, new MemoryPayableDocumentStorage(), accounting.Object);
        Assert.Null(await accountingService.GetAsync(
            created.Data.Id, "accounting-user", canManageAll: true, CancellationToken.None));
        Assert.Empty((await accountingService.ListAsync(
            1, 100, null, null, null, null, null, CancellationToken.None)).Items);
        Assert.Null(await accountingService.DownloadAttachmentAsync(
            created.Data.Id, "accounting-user", canManageAll: true, CancellationToken.None));
        Assert.Single((await adminService.ListEmployeePayrollCostsAsync(
            employeeId, 1, 100, CancellationToken.None)).Items);
    }

    private static Employee NewEmployee()
    {
        var employee = new Employee();
        var result = employee.Update(
            "MAT-001",
            "Mario",
            "Rossi",
            "Impiegato",
            "4",
            "Commercio",
            null,
            45_000m,
            new DateOnly(2024, 1, 1),
            null,
            true,
            null,
            "Via Roma 1",
            "20100",
            "Milano",
            "MI",
            "IT",
            "mario.rossi@example.it",
            "+39 02 1234567",
            "+39 333 1234567");
        Assert.True(result.Success, result.Error);
        return employee;
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
