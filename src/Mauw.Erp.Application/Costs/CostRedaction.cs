using Mauw.Erp.Application.Services;
using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Contracts.Pricing;
using Mauw.Erp.Domain.Pricing;

namespace Mauw.Erp.Application.Costs;

/// <summary>
/// The only place where cost-sensitive members of response DTOs are nulled. Every overload returns the same
/// instance when the caller may see the data, so Admin and Accounting responses are unchanged.
/// </summary>
public static class CostRedaction
{
    /// <summary>Response types that carry cost-sensitive members; a test checks this list against <c>[CostSensitive]</c>.</summary>
    public static readonly IReadOnlyList<Type> RedactedTypes =
    [
        typeof(ProductDto), typeof(ProductVariantDto), typeof(VariantSearchDto), typeof(StockMovementDto),
        typeof(StockItemDto), typeof(InventoryStockDto), typeof(InventoryStockTotalsDto),
        typeof(InventoryStockMovementDto), typeof(PriceRuleRequest)
    ];

    private static readonly HashSet<string> CostSortKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "purchasecost", "basepurchasecost", "unitcost", "totalvalue", "estimatedvalue"
    };

    /// <summary>Cost-based sort keys are ignored (default order) for roles without cost visibility.</summary>
    public static (string? SortBy, bool Descending) Sort(this ICostVisibility costs, string? sortBy, bool descending) =>
        !costs.CanSeeCosts && sortBy is not null && CostSortKeys.Contains(sortBy.Trim())
            ? (null, false)
            : (sortBy, descending);

    public static ProductVariantDto Redact(this ICostVisibility costs, ProductVariantDto dto) =>
        costs.CanSeeCosts ? dto : dto with { PurchaseCost = null };

    public static List<ProductVariantDto> Redact(this ICostVisibility costs, List<ProductVariantDto> dtos) =>
        costs.CanSeeCosts ? dtos : dtos.Select(dto => costs.Redact(dto)).ToList();

    public static ProductDto Redact(this ICostVisibility costs, ProductDto dto) =>
        costs.CanSeeCosts ? dto : dto with { BasePurchaseCost = null, Variants = costs.Redact(dto.Variants) };

    public static PagedResult<ProductDto> Redact(this ICostVisibility costs, PagedResult<ProductDto> page) =>
        costs.CanSeeCosts ? page : page with { Items = page.Items.Select(dto => costs.Redact(dto)).ToList() };

    public static List<VariantSearchDto> Redact(this ICostVisibility costs, List<VariantSearchDto> dtos) =>
        costs.CanSeeCosts ? dtos : dtos.Select(dto => dto with { PurchaseCost = null }).ToList();

    public static StockMovementDto Redact(this ICostVisibility costs, StockMovementDto dto) =>
        costs.CanSeeCosts ? dto : dto with { UnitCost = null };

    public static List<StockMovementDto> Redact(this ICostVisibility costs, List<StockMovementDto> dtos) =>
        costs.CanSeeCosts ? dtos : dtos.Select(dto => costs.Redact(dto)).ToList();

    public static StockItemDto Redact(this ICostVisibility costs, StockItemDto dto) =>
        costs.CanSeeCosts ? dto : dto with { UnitCost = null, EstimatedValue = null };

    public static PagedResult<StockItemDto> Redact(this ICostVisibility costs, PagedResult<StockItemDto> page) =>
        costs.CanSeeCosts ? page : page with { Items = page.Items.Select(dto => costs.Redact(dto)).ToList() };

    public static InventoryStockDto Redact(this ICostVisibility costs, InventoryStockDto dto) =>
        costs.CanSeeCosts ? dto : dto with { UnitCost = null, TotalValue = null, CostSource = null };

    public static InventoryStockTotalsDto Redact(this ICostVisibility costs, InventoryStockTotalsDto dto) =>
        costs.CanSeeCosts ? dto : dto with { TotalValue = null };

    public static InventoryStocksPageDto Redact(this ICostVisibility costs, InventoryStocksPageDto page) =>
        costs.CanSeeCosts ? page : page with { Items = page.Items.Select(dto => costs.Redact(dto)).ToList() };

    public static InventoryStocksSummaryDto Redact(this ICostVisibility costs, InventoryStocksSummaryDto summary) =>
        costs.CanSeeCosts ? summary : summary with { Totals = costs.Redact(summary.Totals) };

    public static InventoryStocksResponseDto Redact(this ICostVisibility costs, InventoryStocksResponseDto response) =>
        costs.CanSeeCosts
            ? response
            : response with
            {
                Items = response.Items.Select(dto => costs.Redact(dto)).ToList(),
                Totals = costs.Redact(response.Totals)
            };

    /// <summary>A redacted export has null totals value and null per-row values: CSV and PDF then omit cost columns.</summary>
    public static InventoryStockExportDto Redact(this ICostVisibility costs, InventoryStockExportDto export) =>
        costs.CanSeeCosts
            ? export
            : export with
            {
                Items = export.Items.Select(dto => costs.Redact(dto)).ToList(),
                Totals = costs.Redact(export.Totals)
            };

    public static InventoryStockMovementDto Redact(this ICostVisibility costs, InventoryStockMovementDto dto) =>
        costs.CanSeeCosts ? dto : dto with { UnitCost = null };

    public static InventoryStockMovementsResponseDto Redact(
        this ICostVisibility costs, InventoryStockMovementsResponseDto response) =>
        costs.CanSeeCosts
            ? response
            : response with { Items = response.Items.Select(dto => costs.Redact(dto)).ToList() };

    /// <summary>Price list rules of type CostMarkup expose the margin: the rule is kept but its Value is nulled.</summary>
    public static PriceRuleRequest Redact(this ICostVisibility costs, PriceRuleRequest rule) =>
        costs.CanSeeMargins || rule.CalculationType != PriceCalculationType.CostMarkup ? rule : rule with { Value = null };

    public static PriceListResponse Redact(this ICostVisibility costs, PriceListResponse list) =>
        costs.CanSeeMargins ? list : list with { Rules = list.Rules.Select(rule => costs.Redact(rule)).ToList() };

    public static IReadOnlyList<PriceListResponse> Redact(
        this ICostVisibility costs, IReadOnlyList<PriceListResponse> lists) =>
        costs.CanSeeMargins ? lists : lists.Select(list => costs.Redact(list)).ToList();
}
