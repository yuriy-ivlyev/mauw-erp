namespace Mauw.Erp.Contracts.Requests;

public record OrderFilterParams(
    string? Search = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20
);
