namespace Mauw.Erp.Contracts.Dtos;

public record SupplierProfileDto(
    Guid? PaymentTermId,
    string? Iban,
    string? BicSwift
);
