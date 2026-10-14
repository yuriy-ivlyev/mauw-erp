namespace Mauw.Erp.Contracts;

public class ProductDto
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string InternalCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal DefaultMarkupRate { get; set; }
    public decimal VatRate { get; set; }
    public string? ImageUrl { get; set; }
    public List<ProductVariantDto> Variants { get; set; } = new();
}

public class ProductVariantDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Size { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal PurchaseCost { get; set; }
    public int StockQuantity { get; set; }
}
