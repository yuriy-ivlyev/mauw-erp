namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Query parameters for paginated contact list with role/status filtering.
/// </summary>
public class ContactFilterRequest : PageSortRequest
{
    public string? Search { get; set; }
    public bool? IsCustomer { get; set; }
    public bool? IsSupplier { get; set; }
    public bool? IsAgent { get; set; }
    public string? Status { get; set; }
}
