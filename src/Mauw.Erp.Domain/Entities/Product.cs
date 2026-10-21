namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Product entity representing a product in the catalog.
/// </summary>
public class Product : Entity<Guid>
{
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; }
    public string InternalCode { get; set; }
    public string Name { get; set; }
    // Legacy data only; never used to resolve or update selling prices.
    public decimal DefaultMarkupRate { get; set; }
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
        string internalCode,
        string name,
        decimal defaultMarkupRate,
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
        InternalCode = internalCode;
        Name = name;
        DefaultMarkupRate = defaultMarkupRate;
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
        string internalCode,
        string name,
        decimal defaultMarkupRate,
        decimal vatRate,
        decimal basePurchaseCost,
        Guid? categoryId = null,
        Guid? collectionId = null,
        Guid? materialId = null,
        bool? hasVariants = null)
    {
        SupplierId = supplierId;
        SupplierCode = supplierCode;
        InternalCode = internalCode;
        Name = name;
        DefaultMarkupRate = defaultMarkupRate;
        VatRate = vatRate;
        BasePurchaseCost = basePurchaseCost;
        CategoryId = categoryId;
        CollectionId = collectionId;
        MaterialId = materialId;
        if (hasVariants.HasValue) HasVariants = hasVariants.Value;
        UpdateTimestamp();
    }
}