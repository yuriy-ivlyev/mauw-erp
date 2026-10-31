namespace Mauw.Erp.Domain.Entities;

public sealed record OrderWorkflowResult(bool Success, string? Error = null)
{
    public static OrderWorkflowResult Ok() => new(true);

    public static OrderWorkflowResult Fail(string error) => new(false, error);
}

/// <summary>Desired quantity for an existing order line while the order is reopened for adjustment.</summary>
public sealed record OrderLineQuantityChange(Guid OrderItemId, int Quantity);
