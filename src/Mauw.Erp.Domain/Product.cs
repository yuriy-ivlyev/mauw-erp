namespace Mauw.Erp.Domain;

public class Product
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string InternalCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal DefaultMarkupRate { get; set; }
    public decimal VatRate { get; set; }
    public string? ImageUrl { get; set; }

    public ICollection<ProductVariant> Variants { get; private set; } = new List<ProductVariant>();

    public void AddVariant(ProductVariant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);

        variant.ProductId = Id;
        variant.Product = this;
        Variants.Add(variant);
    }
}
