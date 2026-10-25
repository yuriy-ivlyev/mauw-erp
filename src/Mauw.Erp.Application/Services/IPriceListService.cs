using Mauw.Erp.Contracts.Pricing;

namespace Mauw.Erp.Application.Services;

public interface IPriceListService
{
    Task<IReadOnlyList<PriceListResponse>> GetAllAsync(CancellationToken ct);
    Task<PriceListResponse?> GetAsync(Guid id, CancellationToken ct);
    /// <param name="keepHiddenMargins">
    /// True when the caller cannot see margins: existing CostMarkup rules always keep their stored value
    /// (the sent one is ignored), and creating a CostMarkup rule or changing a rule type to or from
    /// CostMarkup is rejected.
    /// </param>
    Task<OperationResult<PriceListResponse>> SaveAsync(
        Guid? id, SavePriceListRequest request, CancellationToken ct, bool keepHiddenMargins = false);
    Task<OperationResult<PriceListResponse>> SetActivationAsync(Guid id, bool isActive, CancellationToken ct);
    Task<OperationResult<bool>> DeleteAsync(Guid id, CancellationToken ct);
    Task<OperationResult<ResolvedPricesResponse>> ResolveAsync(ResolvePricesRequest request, CancellationToken ct);
}
