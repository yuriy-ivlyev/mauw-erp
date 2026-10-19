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
    public ICollection<ProductImage> Images { get; private set; } = [];

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

    /// <summary>
    /// Adds an image to the gallery. If it's the first image or isMain is true,
    /// demotes all existing images and marks the new one as main.
    /// </summary>
    public void AddImage(string url, string? storageKey = null, bool isMain = false, Guid? variantId = null)
    {
        var shouldBeMain = isMain || Images.Count == 0;
        if (shouldBeMain)
        {
            foreach (var img in Images)
                img.SetMain(false);
        }

        var displayOrder = Images.Count != 0 ? Images.Max(i => i.DisplayOrder) + 1 : 0;
        var image = new ProductImage(Id, url, storageKey, displayOrder, shouldBeMain, variantId);
        Images.Add(image);
        UpdateTimestamp();
    }

    /// <summary>
    /// Removes an image. If it was the main image, promotes the first remaining image.
    /// </summary>
    public void RemoveImage(Guid imageId)
    {
        var image = Images.FirstOrDefault(i => i.Id == imageId);
        if (image is null) return;

        var wasMain = image.IsMain;
        Images.Remove(image);

        if (wasMain)
        {
            var first = Images.OrderBy(i => i.DisplayOrder).FirstOrDefault();
            first?.SetMain(true);
        }

        UpdateTimestamp();
    }

    /// <summary>
    /// Sets the specified image as the main image, demoting all others.
    /// </summary>
    public void SetMainImage(Guid imageId)
    {
        foreach (var img in Images)
            img.SetMain(img.Id == imageId);

        UpdateTimestamp();
    }
}