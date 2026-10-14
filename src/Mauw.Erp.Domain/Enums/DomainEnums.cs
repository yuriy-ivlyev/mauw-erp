namespace Mauw.Erp.Domain.Enums;

public enum ContactType
{
    Customer,
    Supplier,
    Agent
}

public enum OrderStatus
{
    Draft,
    Confirmed
}

public enum StockMovementType
{
    PurchaseReceipt,
    SalesDelivery,
    InventoryAdjustment,
    CustomerReturn
}

public enum CommissionStatus
{
    Pending,
    Accrued,
    Paid
}
