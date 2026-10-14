using Mauw.Erp.Domain;

namespace Mauw.Erp.Application;

public record ScanStockMovementCommand(
    string Barcode,
    int Quantity,
    StockMovementType Type,
    string? ReferenceDoc);
