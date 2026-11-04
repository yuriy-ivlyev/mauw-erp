namespace Mauw.Erp.Contracts.Requests;

public sealed record PayableDocumentLineRequest(
    string Description,
    string Category,
    decimal TaxableAmount,
    decimal? VatRate,
    string? NatureCode,
    decimal VatAmount,
    decimal GrossAmount,
    string? CostCenter,
    string? Project);

public sealed record CreatePayableDocumentRequest(
    string Kind,
    Guid? SupplierId,
    string? SourceDocumentNumber,
    DateOnly DocumentDate,
    DateOnly DueDate,
    string? BeneficiaryName,
    string? Notes,
    IReadOnlyList<PayableDocumentLineRequest>? Lines,
    Guid? Version = null,
    Guid? EmployeeId = null);

public sealed record CreateEmployeePayrollCostRequest(
    DateOnly Period,
    decimal MonthlyCompanyCost,
    string? Notes);

public sealed record PayableDocumentPaymentRequest(
    Guid Version,
    decimal Amount,
    DateTime? PaymentDate,
    string PaymentMethod,
    string? TransactionDetails);

public sealed record PayableDocumentActionRequest(Guid Version);

public sealed record RejectPayableDocumentRequest(Guid Version, string Reason);
