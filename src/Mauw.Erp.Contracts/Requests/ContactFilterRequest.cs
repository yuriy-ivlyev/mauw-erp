namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Query parameters for paginated contact list with role/status filtering.
/// </summary>
public class ContactFilterRequest
{
    public string? Search { get; set; }
    public bool? IsCustomer { get; set; }
    public bool? IsSupplier { get; set; }
    public bool? IsAgent { get; set; }
    public string? Status { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
