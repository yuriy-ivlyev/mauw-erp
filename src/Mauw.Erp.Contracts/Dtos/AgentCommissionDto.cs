namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for agent commission (response).
/// </summary>
public record AgentCommissionDto(
    Guid Id,
    Guid AgentId,
    Guid InvoiceId,
    decimal TaxableAmountBase,
    decimal CommissionRate,
    decimal CommissionAmount,
    string Status
);