using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.UnitTests.Domain;

public sealed class PayableDocumentTests
{
    [Fact]
    public void SupplierInvoiceAggregatesSourceAmountsAndRequiresSupplierReference()
    {
        var document = NewSupplierInvoice();

        var result = document.UpdateDraft(
            Guid.NewGuid(),
            "F-2026-001",
            new DateOnly(2026, 11, 4),
            new DateOnly(2026, 12, 4),
            null,
            "Utenze novembre",
            [Line(document.Id)]);

        Assert.True(result.Success, result.Error);
        Assert.Equal(100m, document.TaxableTotal);
        Assert.Equal(22m, document.VatTotal);
        Assert.Equal(122m, document.GrossTotal);
        Assert.Equal(0m, document.BalanceDue);
        Assert.Equal(PayablePaymentStatus.NotPayable, document.PaymentStatus);

        var missingSupplier = document.UpdateDraft(
            null,
            "F-2026-001",
            new DateOnly(2026, 11, 4),
            new DateOnly(2026, 12, 4),
            null,
            null,
            [Line(document.Id)]);
        Assert.False(missingSupplier.Success);
    }

    [Fact]
    public void ApprovalAndPartialPaymentsKeepTheDueBalanceAndNormalizeDatesToUtc()
    {
        var document = NewSupplierInvoice();
        Assert.True(document.UpdateDraft(
            Guid.NewGuid(),
            "F-2026-002",
            new DateOnly(2026, 11, 4),
            new DateOnly(2026, 12, 4),
            null,
            null,
            [Line(document.Id)]).Success);
        Assert.True(document.SetAttachment("payables/test.pdf", "fattura.pdf", "application/pdf", 100).Success);
        Assert.True(document.SubmitForApproval(DateTime.UtcNow).Success);
        Assert.Equal(PayableDocumentStatus.PendingApproval, document.Status);
        Assert.True(document.Approve("approver", DateTime.UtcNow).Success);

        var firstPayment = document.RecordPayment(
            50m, new DateTime(2026, 11, 4), "Bonifico bancario", "CRO-1", "accounting");

        Assert.True(firstPayment.Success, firstPayment.Error);
        Assert.Equal(PayableDocumentStatus.Approved, document.Status);
        Assert.Equal(PayablePaymentStatus.PartiallyPaid, document.PaymentStatus);
        Assert.Equal(50m, document.PaidAmount);
        Assert.Equal(72m, document.BalanceDue);
        Assert.Equal(DateTimeKind.Utc, Assert.Single(document.Payments).PaymentDateUtc.Kind);
        Assert.Equal(new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc),
            document.Payments.Single().PaymentDateUtc);

        var overpayment = document.RecordPayment(72.01m, DateTime.UtcNow, "Bonifico bancario", null, "accounting");
        Assert.False(overpayment.Success);
        Assert.Equal(50m, document.PaidAmount);
        Assert.Single(document.Payments);

        Assert.True(document.RecordPayment(72m, DateTime.UtcNow, "Bonifico bancario", null, "accounting").Success);
        Assert.Equal(PayablePaymentStatus.Paid, document.PaymentStatus);
        Assert.Equal(0m, document.BalanceDue);
    }

    [Fact]
    public void ReimbursementNeedsABeneficiaryAndNeverRequiresASupplier()
    {
        var document = new PayableDocument(PayableDocumentKind.ExpenseReimbursement, "user-1");
        var employeeId = Guid.NewGuid();

        var missingBeneficiary = document.UpdateDraft(
            null,
            null,
            new DateOnly(2026, 11, 4),
            new DateOnly(2026, 11, 4),
            null,
            null,
            [new PayableDocumentLine(
                document.Id, "Trasferta", PayableExpenseCategory.Travel,
                100m, null, null, 0m, 100m, "Commerciale", "Visita clienti")],
            employeeId);
        Assert.False(missingBeneficiary.Success);

        var valid = document.UpdateDraft(
            null,
            "RIMB-01",
            new DateOnly(2026, 11, 4),
            new DateOnly(2026, 11, 4),
            "Mario Rossi",
            null,
            [new PayableDocumentLine(
                document.Id, "Trasferta", PayableExpenseCategory.Travel,
                100m, null, null, 0m, 100m, "Commerciale", "Visita clienti")],
            employeeId);

        Assert.True(valid.Success, valid.Error);
        Assert.Null(document.SupplierId);
        Assert.Equal(employeeId, document.EmployeeId);
        Assert.Equal("Mario Rossi", document.BeneficiaryName);
    }

    [Fact]
    public void RejectedDocumentCanBeCorrectedAndResubmitted()
    {
        var document = NewSupplierInvoice();
        var supplierId = Guid.NewGuid();
        Assert.True(document.UpdateDraft(
            supplierId,
            "F-2026-003",
            new DateOnly(2026, 11, 4),
            new DateOnly(2026, 12, 4),
            null,
            null,
            [Line(document.Id)]).Success);
        Assert.True(document.SetAttachment("key", "invoice.pdf", "application/pdf", 100).Success);
        Assert.True(document.SubmitForApproval(DateTime.UtcNow).Success);
        Assert.True(document.Reject("Verificare il centro di costo").Success);
        Assert.Equal(PayablePaymentStatus.NotPayable, document.PaymentStatus);
        Assert.Equal(0m, document.BalanceDue);

        var corrected = document.UpdateDraft(
            supplierId,
            "F-2026-003",
            new DateOnly(2026, 11, 4),
            new DateOnly(2026, 12, 4),
            null,
            "Corretto",
            [Line(document.Id)]);

        Assert.True(corrected.Success, corrected.Error);
        Assert.Equal(PayableDocumentStatus.Draft, document.Status);
        Assert.Null(document.RejectionReason);
        Assert.True(document.SubmitForApproval(DateTime.UtcNow).Success);
    }

    [Fact]
    public void AggregateTotalsAreValidatedBeforeReplacingExistingLines()
    {
        var document = NewSupplierInvoice();
        var supplierId = Guid.NewGuid();
        Assert.True(document.UpdateDraft(
            supplierId,
            "F-2026-004",
            new DateOnly(2026, 11, 4),
            new DateOnly(2026, 12, 4),
            null,
            null,
            [Line(document.Id)]).Success);
        var originalLine = Assert.Single(document.Lines);
        var maximumAmount = 9_999_999_999_999_999.99m;

        var result = document.UpdateDraft(
            supplierId,
            "F-2026-004",
            new DateOnly(2026, 11, 4),
            new DateOnly(2026, 12, 4),
            null,
            null,
            [
                new(document.Id, "Costo uno", PayableExpenseCategory.Other,
                    maximumAmount, null, null, 0m, maximumAmount, null, null),
                new(document.Id, "Costo due", PayableExpenseCategory.Other,
                    maximumAmount, null, null, 0m, maximumAmount, null, null)
            ]);

        Assert.False(result.Success);
        Assert.Equal(100m, document.TaxableTotal);
        Assert.Same(originalLine, Assert.Single(document.Lines));
    }

    private static PayableDocument NewSupplierInvoice() =>
        new(PayableDocumentKind.SupplierInvoice, "accounting");

    private static PayableDocumentLine Line(Guid documentId) =>
        new(documentId, "Bolletta energia", PayableExpenseCategory.Utilities,
            100m, 22m, null, 22m, 122m, "Sede", null);
}
