using System.Text.Json.Serialization;

namespace Mauw.Erp.Contracts.Enums;

/// <summary>
/// Order status enumeration shared with UI.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatusEnum
{
    Draft = 0,
    Confirmed = 1,
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