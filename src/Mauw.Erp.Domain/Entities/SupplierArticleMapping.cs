namespace Mauw.Erp.Domain.Entities;

public sealed class SupplierArticleMapping : Entity<Guid>
{
    public Guid SupplierId { get; set; }
    public Contact Supplier { get; private set; } = null!;
    public string SupplierItemCode { get; set; } = "";
    public Guid ArticleVariantId { get; set; }
    public ProductVariant ArticleVariant { get; private set; } = null!;

    public SupplierArticleMapping() : base(Guid.NewGuid()) { }
}
