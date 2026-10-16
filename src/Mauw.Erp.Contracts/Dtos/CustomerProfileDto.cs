namespace Mauw.Erp.Contracts.Dtos;

public record CustomerProfileDto(
    Guid? PriceListId,
    Guid? PaymentTermId,
    decimal FinancialDiscountRate,
    decimal CreditLimit,
    bool IsBlocked,
    string? BlockReason,
    Guid? DefaultAgentId,
    string? DeliveryNotes
);
