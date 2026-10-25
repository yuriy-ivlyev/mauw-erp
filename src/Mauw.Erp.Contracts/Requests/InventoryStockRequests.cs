using Mauw.Erp.Contracts.Enums;

namespace Mauw.Erp.Contracts.Requests;

public sealed class InventoryStockFilterRequest : PageSortRequest
{
    public string? Search { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? CategoryId { get; set; }
    public DateTime? AtDate { get; set; }
    public bool OnlyInStock { get; set; }
    public ValuationMethod ValuationMethod { get; set; } =
        global::Mauw.Erp.Contracts.Enums.ValuationMethod.WeightedAverageCost;
}
