namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for scanning inventory by barcode.
/// </summary>
public class BarcodeScanRequest
{
    public string Barcode { get; set; } = "";
    public int Quantity { get; set; }
    public int Type { get; set; } // StockMovementType enum
    public string? ReferenceDoc { get; set; }
}