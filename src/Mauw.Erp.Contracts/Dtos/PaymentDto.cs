namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for Payment information (response).
/// </summary>
public record PaymentDto(
    Guid Id,
    decimal Amount,
    DateTime PaymentDate,
    string PaymentMethod
);