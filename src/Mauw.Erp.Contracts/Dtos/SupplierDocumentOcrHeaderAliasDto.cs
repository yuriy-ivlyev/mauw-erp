namespace Mauw.Erp.Contracts.Dtos;

public sealed record SupplierDocumentOcrHeaderAliasDto(
    Guid Id,
    string Column,
    string Alias,
    int Position);
