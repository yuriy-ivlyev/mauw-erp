namespace Mauw.Erp.Contracts.Requests;

public class OrderFilterParams : PageSortRequest
{
    public OrderFilterParams()
    {
    }

    public OrderFilterParams(
        string? search = null,
        string? status = null,
        string? orderType = null,
        int page = 1,
        int pageSize = 20,
        string? sortBy = null,
        bool sortDescending = false)
    {
        Search = search;
        Status = status;
        OrderType = orderType;
        Page = page;
        PageSize = pageSize;
        SortBy = sortBy;
        SortDescending = sortDescending;
    }

    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? OrderType { get; init; }
}
