namespace Mauw.Erp.Contracts;

public class CreateProductRequest
{
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string InternalCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal DefaultMarkupRate { get; set; }
    public decimal VatRate { get; set; }
    public string? ImageUrl { get; set; }
    public List<CreateProductVariantRequest> Variants { get; set; } = new();
}

public class CreateProductVariantRequest
{
    public string Size { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public decimal PurchaseCost { get; set; }
    public int StockQuantity { get; set; }
}
