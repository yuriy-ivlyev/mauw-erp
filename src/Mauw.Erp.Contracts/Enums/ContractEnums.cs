namespace Mauw.Erp.Contracts.Enums;

/// <summary>
/// Contact type enumeration shared with UI.
/// </summary>
public enum ContactTypeEnum
{
    Customer,
    Supplier,
    Agent
}

/// <summary>
/// Order status enumeration shared with UI.
/// </summary>
public enum OrderStatusEnum
{
    Draft,
    Confirmed
}

/// <summary>
/// Stock movement type enumeration shared with UI.
/// </summary>
public enum StockMovementTypeEnum
{
    PurchaseReceipt,
    SalesDelivery,
    InventoryAdjustment,
    CustomerReturn
}

/// <summary>
/// Commission status enumeration shared with UI.
/// </summary>
public enum CommissionStatusEnum
{
    Pending,
    Accrued,
    Paid
}
