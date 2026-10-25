namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Product entity representing a product in the catalog.
/// </summary>
public class Product : Entity<Guid>
{
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; }
    public string Barcode { get; set; }
    public string Name { get; set; }
    public decimal? SellingPrice { get; set; }
    public decimal VatRate { get; set; }
    public decimal BasePurchaseCost { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? CollectionId { get; set; }
    public Guid? MaterialId { get; set; }
    public bool HasVariants { get; set; } = true;

    // Navigation
    public Category? Category { get; set; }
    public Collection? Collection { get; set; }
    public Material? Material { get; set; }
    public ICollection<ProductVariant> Variants { get; set; } = [];

    public Product(
        Guid supplierId,
        string supplierCode,
        string barcode,
        string name,
        decimal vatRate,
        decimal basePurchaseCost,
        Guid? categoryId = null,
        Guid? collectionId = null,
        Guid? materialId = null,
        bool hasVariants = true
    ) : base()
    {
        SupplierId = supplierId;
        SupplierCode = supplierCode;
        Barcode = barcode;
        Name = name;
        VatRate = vatRate;
        BasePurchaseCost = basePurchaseCost;
        CategoryId = categoryId;
        CollectionId = collectionId;
        MaterialId = materialId;
        HasVariants = hasVariants;
    }

    public void Update(
        Guid supplierId,
        string supplierCode,
        string barcode,
        string name,
        decimal vatRate,
        decimal basePurchaseCost,
        Guid? categoryId = null,
        Guid? collectionId = null,
        Guid? materialId = null,
        bool? hasVariants = null)
    {
        SupplierId = supplierId;
        SupplierCode = supplierCode;
        Barcode = barcode;
        Name = name;
        VatRate = vatRate;
        BasePurchaseCost = basePurchaseCost;
        CategoryId = categoryId;
        CollectionId = collectionId;
        MaterialId = materialId;
        if (hasVariants.HasValue) HasVariants = hasVariants.Value;
        foreach (var variant in Variants)
            variant.SupplierId = supplierId;
        UpdateTimestamp();
    }
}