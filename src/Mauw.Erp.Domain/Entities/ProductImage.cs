namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Represents a single image in a product's gallery.
/// </summary>
public class ProductImage : Entity<Guid>
{
    public Guid ProductId { get; private set; }
    public string Url { get; private set; }
    public string? StorageKey { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsMain { get; private set; }
    public Guid? ProductVariantId { get; private set; }

    // Navigation
    public Product Product { get; private set; } = null!;

    public ProductImage(
        Guid productId,
        string url,
        string? storageKey = null,
        int displayOrder = 0,
        bool isMain = false,
        Guid? productVariantId = null
    ) : base()
    {
        ProductId = productId;
        Url = url;
        StorageKey = storageKey;
        DisplayOrder = displayOrder;
        IsMain = isMain;
        ProductVariantId = productVariantId;
    }

    public void SetMain(bool value) => IsMain = value;
}
