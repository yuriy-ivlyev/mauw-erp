namespace Mauw.Erp.Domain.Enums;

public enum OrderStatus
{
    Draft = 0,
    Confirmed = 1,

    // Keep these legacy values stable: existing Cassa sessions persist them as integers.
    Suspended = 2,
    Terminated = 3,

    PendingApproval = 4,
    Picking = 5,
    Packing = 6,
    Shipped = 7,
    Delivered = 8,
    Completed = 9,
    Cancelled = 10,
    OnHold = 11,
    PartiallyShipped = 12
}
