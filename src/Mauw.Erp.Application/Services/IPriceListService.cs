using Mauw.Erp.Contracts.Pricing;

namespace Mauw.Erp.Application.Services;

public interface IPriceListService
{
    Task<IReadOnlyList<PriceListResponse>> GetAllAsync(CancellationToken ct);
    Task<PriceListResponse?> GetAsync(Guid id, CancellationToken ct);
    /// <param name="keepHiddenMargins">
    /// True when the caller cannot see margins: a CostMarkup rule sent without Value keeps its stored value,
    /// so saving a list read with nulled margins does not overwrite them.
    /// </param>
    Task<OperationResult<PriceListResponse>> SaveAsync(
        Guid? id, SavePriceListRequest request, CancellationToken ct, bool keepHiddenMargins = false);
    Task<OperationResult<PriceListResponse>> SetActivationAsync(Guid id, bool isActive, CancellationToken ct);
    Task<OperationResult<bool>> DeleteAsync(Guid id, CancellationToken ct);
    Task<OperationResult<ResolvedPricesResponse>> ResolveAsync(ResolvePricesRequest request, CancellationToken ct);
}
