namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// ProductVariant entity representing a specific variant of a product (size/color combination).
/// </summary>
public class ProductVariant : Entity
{
    public Guid ProductId { get; set; }
    public string Size { get; set; }
    public string Color { get; set; }
    public string Barcode { get; set; }
    public decimal PurchaseCost { get; set; }
    public int StockQuantity { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
    public ICollection<StockMovement> StockMovements { get; set; } = [];

    public ProductVariant(
        Guid productId,
        string size,
        string color,
        string barcode,
        decimal purchaseCost,
        int stockQuantity = 0
    ) : base()
    {
        ProductId = productId;
        Size = size;
        Color = color;
        Barcode = barcode;
        PurchaseCost = purchaseCost;
        StockQuantity = stockQuantity;
    }

    /// <summary>
    /// Updates the stock quantity atomically.
    /// </summary>
    public void UpdateStockQuantity(int quantity)
    {
        StockQuantity += quantity;
        UpdateTimestamp();
    }
}