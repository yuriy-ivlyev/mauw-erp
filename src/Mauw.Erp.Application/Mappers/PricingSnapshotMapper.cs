using Mauw.Erp.Contracts.Pricing;
using Mauw.Erp.Domain.Pricing;

namespace Mauw.Erp.Application.Mappers;

internal static class PricingSnapshotMapper
{
    public static PricingSnapshotDto? Map(PricingSnapshot? snapshot) => snapshot is null ? null : new(
        snapshot.PriceListId, snapshot.RuleId, snapshot.ListUnitPrice, snapshot.AgreedUnitNetPrice,
        snapshot.IsManualOverride, snapshot.OverrideReason, snapshot.IsCustomerReturn, snapshot.SourceLineId,
        snapshot.Discounts?.Select(discount => new AppliedDiscountDto(
            discount.Source.ToString(), discount.Reason, discount.Percent, discount.Amount)).ToList());
}
