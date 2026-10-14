namespace Mauw.Erp.Api.Endpoints;

/// <summary>
/// Generic page response DTO.
/// </summary>
public record Page<T>(List<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}