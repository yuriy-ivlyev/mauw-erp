using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Services;

/// <summary>Signed on-hand change of a variant produced by an order adjustment.</summary>
public sealed record OrderAdjustmentStockDelta(Guid VariantId, int DeltaOnHand);

/// <summary>
/// Pure calculation of the stock effect of an order and of the delta between two states of the same order.
/// Negative amounts leave the warehouse (sales), positive amounts come back (returns).
/// </summary>
public static class OrderAdjustmentStockCalculator
{
    public static IReadOnlyDictionary<Guid, int> GetStockEffects(IEnumerable<OrderItem> items) =>
        items
            .Where(item => item.AffectsStock && item.Quantity != 0)
            .GroupBy(item => item.VariantId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.IsCustomerReturnLine
                    ? Math.Abs(item.Quantity)
                    : -Math.Abs(item.Quantity)));

    /// <summary>
    /// A quantity rising from Q1 to Q2 yields a further discharge of Q2 - Q1 pieces (negative delta);
    /// a quantity dropping from Q1 to Q2 yields a return of Q1 - Q2 pieces (positive delta).
    /// Variants are returned in a stable order so stock rows are always locked in the same sequence.
    /// </summary>
    public static IReadOnlyList<OrderAdjustmentStockDelta> CalculateDeltas(
        IReadOnlyDictionary<Guid, int> before,
        IReadOnlyDictionary<Guid, int> after) =>
        before.Keys.Union(after.Keys)
            .Select(variantId => new OrderAdjustmentStockDelta(
                variantId,
                checked(after.GetValueOrDefault(variantId) - before.GetValueOrDefault(variantId))))
            .Where(delta => delta.DeltaOnHand != 0)
            .OrderBy(delta => delta.VariantId)
            .ToList();
}
