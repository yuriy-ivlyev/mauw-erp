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
}