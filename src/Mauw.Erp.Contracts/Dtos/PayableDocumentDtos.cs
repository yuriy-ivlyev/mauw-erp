namespace Mauw.Erp.Contracts.Dtos;

public sealed record PayableDocumentLineDto(
    Guid Id,
    string Description,
    string Category,
    string? CostCenter,
    string? Project,
    decimal TaxableAmount,
    decimal? VatRate,
    string? NatureCode,
    decimal VatAmount,
    decimal GrossAmount);

public sealed record PayableDocumentPaymentDto(
    Guid Id,
    decimal Amount,
    DateTime PaymentDateUtc,
    string PaymentMethod,
    string? TransactionDetails,
    string? CreatedByUserId);

public sealed record PayableDocumentListItemDto(
    Guid Id,
    string DocumentCode,
    string Kind,
    string? SupplierName,
    string? SourceDocumentNumber,
    DateOnly DocumentDate,
    DateOnly DueDate,
    string? BeneficiaryName,
    string Status,
    string PaymentStatus,
    decimal TaxableTotal,
    decimal VatTotal,
    decimal GrossTotal,
    decimal PaidAmount,
    decimal BalanceDue,
    bool HasAttachment);

public sealed record PayableDocumentDto(
    Guid Id,
    string DocumentCode,
    string Kind,
    Guid? SupplierId,
    string? SupplierName,
    string? SourceDocumentNumber,
    DateOnly DocumentDate,
    DateOnly DueDate,
    string? BeneficiaryName,
    string? Notes,
    string Status,
    string PaymentStatus,
    decimal TaxableTotal,
    decimal VatTotal,
    decimal GrossTotal,
    decimal PaidAmount,
    decimal BalanceDue,
    string? AttachmentFileName,
    string? AttachmentContentType,
    long? AttachmentLength,
    string? CreatedByUserId,
    string? ApprovedByUserId,
    DateTime? SubmittedAtUtc,
    DateTime? ApprovedAtUtc,
    string? RejectionReason,
    Guid Version,
    IReadOnlyList<PayableDocumentLineDto> Lines,
    IReadOnlyList<PayableDocumentPaymentDto> Payments);
