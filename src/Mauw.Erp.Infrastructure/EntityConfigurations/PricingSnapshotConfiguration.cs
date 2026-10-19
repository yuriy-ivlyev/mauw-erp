using Mauw.Erp.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

internal static class PricingSnapshotConfiguration
{
    public static void Configure<TOwner>(
        OwnedNavigationBuilder<TOwner, PricingSnapshot> builder, string tableName) where TOwner : class
    {
        builder.ToTable(tableName);
        builder.Property(snapshot => snapshot.PriceListId).HasColumnName("pricing_price_list_id");
        builder.Property(snapshot => snapshot.RuleId).HasColumnName("pricing_rule_id");
        builder.Property(snapshot => snapshot.ListUnitPrice).HasColumnName("pricing_list_unit_price").HasPrecision(22, 8);
        builder.Property(snapshot => snapshot.AgreedUnitNetPrice).HasColumnName("pricing_agreed_unit_net_price").HasPrecision(22, 8);
        builder.Property(snapshot => snapshot.IsManualOverride).HasColumnName("pricing_is_manual_override");
        builder.Property(snapshot => snapshot.OverrideReason).HasColumnName("pricing_override_reason").HasMaxLength(500);
        builder.Property(snapshot => snapshot.IsCustomerReturn).HasColumnName("pricing_is_customer_return");
        builder.Property(snapshot => snapshot.SourceLineId).HasColumnName("pricing_source_line_id");
    }
}
