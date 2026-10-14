namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Product entity representing a product in the catalog.
/// </summary>
public class Product : Entity
{
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; }
    public string InternalCode { get; set; }
    public string Name { get; set; }
    public decimal DefaultMarkupRate { get; set; }
    public decimal VatRate { get; set; }
    public string? ImageUrl { get; set; }

    // Navigation
    public ICollection<ProductVariant> Variants { get; set; } = [];

    public Product(
        Guid supplierId,
        string supplierCode,
        string internalCode,
        string name,
        decimal defaultMarkupRate,
        decimal vatRate,
        string? imageUrl = null
    ) : base()
    {
        SupplierId = supplierId;
        SupplierCode = supplierCode;
        InternalCode = internalCode;
        Name = name;
        DefaultMarkupRate = defaultMarkupRate;
        VatRate = vatRate;
        ImageUrl = imageUrl;
    }

    public void Update(
        Guid supplierId,
        string supplierCode,
        string internalCode,
        string name,
        decimal defaultMarkupRate,
        decimal vatRate,
        string? imageUrl = null)
    {
        SupplierId = supplierId;
        SupplierCode = supplierCode;
        InternalCode = internalCode;
        Name = name;
        DefaultMarkupRate = defaultMarkupRate;
        VatRate = vatRate;
        ImageUrl = imageUrl;
        UpdateTimestamp();
    }

    private Product() { } // For EF Core
}

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

    private ProductVariant() { } // For EF Core

    /// <summary>
    /// Updates the stock quantity atomically.
    /// </summary>
    public void UpdateStockQuantity(int quantity)
    {
        StockQuantity += quantity;
        UpdateTimestamp();
    }
}
