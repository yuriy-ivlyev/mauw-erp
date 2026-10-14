namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating a new product.
/// </summary>
public class CreateProductRequest
{
    public Guid SupplierId { get; set; }
    public string SupplierCode { get; set; } = "";
    public string InternalCode { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal DefaultMarkupRate { get; set; }
    public decimal VatRate { get; set; }
    public string? ImageUrl { get; set; }
    public List<CreateProductVariantRequest> Variants { get; set; } = new();
}