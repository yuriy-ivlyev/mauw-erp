using System.Text.Json;
using Mauw.Erp.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
        builder.Property(snapshot => snapshot.Discounts).HasColumnName("pricing_discounts").HasColumnType("jsonb")
            .HasConversion(
                new ValueConverter<IReadOnlyList<AppliedDiscount>?, string?>(
                    discounts => SerializeDiscounts(discounts), json => DeserializeDiscounts(json)),
                new ValueComparer<IReadOnlyList<AppliedDiscount>?>(
                    (left, right) => SameDiscounts(left, right),
                    discounts => HashDiscounts(discounts),
                    discounts => CopyDiscounts(discounts)));
    }

    private static string? SerializeDiscounts(IReadOnlyList<AppliedDiscount>? discounts) =>
        discounts is null ? null : JsonSerializer.Serialize(discounts);

    private static IReadOnlyList<AppliedDiscount>? DeserializeDiscounts(string? json) =>
        string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<AppliedDiscount>>(json);

    private static bool SameDiscounts(IReadOnlyList<AppliedDiscount>? left, IReadOnlyList<AppliedDiscount>? right) =>
        ReferenceEquals(left, right) || (left is not null && right is not null && left.SequenceEqual(right));

    private static int HashDiscounts(IReadOnlyList<AppliedDiscount>? discounts)
    {
        var hash = new HashCode();
        foreach (var discount in discounts ?? [])
            hash.Add(discount);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<AppliedDiscount>? CopyDiscounts(IReadOnlyList<AppliedDiscount>? discounts) =>
        discounts?.ToList();
}
