namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request to add an image to a product's gallery.
/// </summary>
public class AddProductImageRequest
{
    public string Url { get; set; } = "";
    public string? StorageKey { get; set; }
    public bool IsMain { get; set; }
    public Guid? ProductVariantId { get; set; }
}
