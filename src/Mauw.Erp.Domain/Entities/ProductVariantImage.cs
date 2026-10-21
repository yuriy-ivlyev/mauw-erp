using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Image of a product or variant. TargetId is polymorphic (product or variant id), hence no foreign key.
/// </summary>
public class ProductVariantImage : Entity<Guid>
{
    public Guid TargetId { get; private set; }
    public string FilePath { get; private set; }
    public ImageCategory Category { get; private set; }
    public int SortOrder { get; private set; }

    public void ChangeCategory(ImageCategory category)
    {
        Category = category;
        UpdateTimestamp();
    }

    public ProductVariantImage(Guid targetId, string filePath, ImageCategory category, int sortOrder) : base()
    {
        Id = Guid.NewGuid();
        TargetId = targetId;
        FilePath = filePath;
        Category = category;
        SortOrder = sortOrder;
    }
}
