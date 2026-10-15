namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// ProductVariant entity representing a specific variant of a product (size/color combination).
/// </summary>
public class ProductVariant : Entity
{
    public Guid ProductId { get; set; }
    public Guid? ColorId { get; set; }
    public Guid? SizeId { get; set; }
    public string Barcode { get; set; }
    public decimal PurchaseCost { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
    public Color? Color { get; set; }
    public Size? Size { get; set; }
    public ICollection<StockMovement> StockMovements { get; set; } = [];

    public ProductVariant(
        Guid productId,
        string barcode,
        decimal purchaseCost,
        Guid? colorId = null,
        Guid? sizeId = null
    ) : base()
    {
        ProductId = productId;
        Barcode = barcode;
        PurchaseCost = purchaseCost;
        ColorId = colorId;
        SizeId = sizeId;
    }
}

