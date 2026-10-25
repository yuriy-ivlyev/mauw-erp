namespace Mauw.Erp.Api.Authorization;

/// <summary>Names of the authorization policies registered by <c>AddAppAuthorization</c>.</summary>
public static class AppPolicies
{
    /// <summary>Any of the four application roles.</summary>
    public const string AnyRole = nameof(AnyRole);

    /// <summary>Admin only.</summary>
    public const string AdminOnly = nameof(AdminOnly);

    /// <summary>Catalog, support tables and warehouse registry writes: Admin, Warehouse.</summary>
    public const string CatalogManagement = nameof(CatalogManagement);

    /// <summary>Stock movements, transfers, scans and DDT management: Admin, Warehouse.</summary>
    public const string WarehouseOperations = nameof(WarehouseOperations);

    /// <summary>Orders, cassa, contact and price list writes: Admin, Sales.</summary>
    public const string SalesOperations = nameof(SalesOperations);

    /// <summary>Invoices and credit notes: Admin, Accounting, Sales.</summary>
    public const string Billing = nameof(Billing);

    /// <summary>DDT created or downloaded from the cassa and the order list: Admin, Warehouse, Sales.</summary>
    public const string DdtDocuments = nameof(DdtDocuments);

    /// <summary>Stock posted by the cassa (showroom sale) besides the warehouse: Admin, Warehouse, Sales.</summary>
    public const string StockMovementWrite = nameof(StockMovementWrite);

    /// <summary>Stock reads: any role, but historical (atDate) requests only for Admin and Accounting.</summary>
    public const string InventoryStockRead = nameof(InventoryStockRead);
}
