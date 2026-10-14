namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating a product variant.
/// </summary>
public class CreateProductVariantRequest
{
    public string Size { get; set; } = "";
    public string Color { get; set; } = "";
    public string Barcode { get; set; } = "";
    public decimal PurchaseCost { get; set; }
    public int StockQuantity { get; set; }
}