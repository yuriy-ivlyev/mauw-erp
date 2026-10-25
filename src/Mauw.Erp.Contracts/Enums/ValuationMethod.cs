namespace Mauw.Erp.Contracts.Enums;

public enum ValuationMethod
{
    LastPurchaseCost,
    WeightedAverageCost
}

public static class InventoryStockValuationLabels
{
    public static string GetLabel(ValuationMethod method) => method switch
    {
        ValuationMethod.LastPurchaseCost => "Ultimo costo",
        ValuationMethod.WeightedAverageCost => "Costo medio ponderato",
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Metodo di valorizzazione non supportato.")
    };
}

public static class InventoryStockCostSources
{
    public const string StockMovements = "StockMovements";
    public const string PurchaseCost = "PurchaseCost";
}
