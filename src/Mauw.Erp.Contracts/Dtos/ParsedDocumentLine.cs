namespace Mauw.Erp.Contracts.Dtos;

public record ParsedDocumentLine(
    string SupplierCode,
    string Description,
    int Quantity);
