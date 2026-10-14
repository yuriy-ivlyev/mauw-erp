namespace Mauw.Erp.Domain;

public class ProductVariant
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public string Size { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal PurchaseCost { get; set; }
    public int StockQuantity { get; set; }
}
