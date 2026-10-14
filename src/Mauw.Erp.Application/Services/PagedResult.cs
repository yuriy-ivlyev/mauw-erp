namespace Mauw.Erp.Application.Services;

/// <summary>
/// Paginated result.
/// </summary>
public record PagedResult<T>(List<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}