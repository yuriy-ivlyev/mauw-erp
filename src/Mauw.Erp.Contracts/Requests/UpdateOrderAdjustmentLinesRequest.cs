namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// One line of the desired state of an order reopened for pre-SDI adjustment.
/// <see cref="OrderItemId"/> identifies an existing line; a new line is identified by <see cref="VariantId"/>.
/// </summary>
public sealed record OrderAdjustmentLineRequest(
    Guid? OrderItemId,
    Guid? VariantId,
    int Quantity,
    string? NatureCode = null);

/// <summary>Complete set of adjustable lines: existing lines that are not listed are removed.</summary>
public sealed record UpdateOrderAdjustmentLinesRequest(IReadOnlyList<OrderAdjustmentLineRequest> Lines);
