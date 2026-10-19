namespace Mauw.Erp.Contracts.Requests;

/// <summary>Transient price-entry aid; neither input is saved as a pricing policy.</summary>
public sealed record CalculateSellingPriceRequest(decimal? PurchaseCost, decimal? MarkupRate);
