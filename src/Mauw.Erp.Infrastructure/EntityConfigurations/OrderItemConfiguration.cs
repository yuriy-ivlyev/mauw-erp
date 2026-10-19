using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity configuration for OrderItem entities.
/// </summary>
public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(oi => oi.Id);
        builder.OwnsOne(item => item.PricingSnapshot, snapshot => PricingSnapshotConfiguration.Configure(snapshot, "order_items"));

        builder.Property(oi => oi.Quantity)
            .IsRequired();

        builder.Property(oi => oi.UnitNetPrice)
            .HasPrecision(22, 8);

        builder.Property(oi => oi.DiscountPercent).HasColumnType("numeric");
        builder.Property(oi => oi.NatureCode).HasMaxLength(4);

        builder.Property(oi => oi.LineNetTotal)
            .HasPrecision(18, 2);

        builder.Property(oi => oi.VatRate)
            .HasPrecision(5, 2);

        builder.Property(oi => oi.LineVatTotal)
            .HasPrecision(18, 2);

        builder.Property(oi => oi.LineGrossTotal)
            .HasPrecision(18, 2);

        builder.Property(oi => oi.MaxQuantity)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(oi => oi.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(oi => oi.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(oi => oi.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(oi => oi.Variant)
            .WithMany()
            .HasForeignKey(oi => oi.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}