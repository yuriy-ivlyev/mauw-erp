namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// ProductVariant entity representing a specific variant of a product (size/color combination).
/// </summary>
public class ProductVariant : Entity<Guid>
{
    public Guid ProductId { get; set; }
    public Guid SupplierId { get; set; }
    public string? SupplierCode { get; set; }
    public string? ColorId { get; set; }
    public string? SizeId { get; set; }
    public string Barcode { get; set; }
    public decimal PurchaseCost { get; set; }
    public decimal? SellingPrice { get; set; }
    // Legacy data only; never used to resolve or update selling prices.
    public decimal? MarkupRate { get; set; }
    public decimal? WeightGrams { get; set; }
    /// <summary>Variant whose images represent the product in lists. At most one per product.</summary>
    public bool VarianteVetrina { get; set; }
    public Product Product { get; set; } = null!;
    public Color? Color { get; set; }
    public Size? Size { get; set; }
    public ICollection<StockMovement> StockMovements { get; set; } = [];

    public ProductVariant(
        Guid productId,
        string barcode,
        decimal purchaseCost,
        Guid supplierId,
        string? colorId = null,
        string? sizeId = null,
        decimal? weightGrams = null,
        string? supplierCode = null
    ) : base()
    {
        ProductId = productId;
        SupplierId = supplierId;
        SupplierCode = supplierCode;
        Barcode = barcode;
        PurchaseCost = purchaseCost;
        ColorId = colorId;
        SizeId = sizeId;
        WeightGrams = weightGrams;
    }

    public void Update(
        string? colorId,
        string? sizeId,
        string barcode,
        decimal purchaseCost,
        decimal? weightGrams = null,
        string? supplierCode = null)
    {
        SupplierCode = supplierCode;
        ColorId = colorId;
        SizeId = sizeId;
        Barcode = barcode;
        PurchaseCost = purchaseCost;
        WeightGrams = weightGrams;
    }
}
