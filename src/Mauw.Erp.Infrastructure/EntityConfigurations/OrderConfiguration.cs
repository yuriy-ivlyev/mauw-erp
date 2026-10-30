using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity configuration for Order entities.
/// </summary>
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.Status)
            .HasConversion<int>()
            .HasDefaultValue(OrderStatus.Draft)
            .IsRequired();

        builder.Property(o => o.FulfillmentType)
            .HasConversion<int>()
            .HasDefaultValue(OrderFulfillmentType.StandardShipping)
            .IsRequired();

        builder.Property(o => o.StatusBeforeHold)
            .HasConversion<int?>();

        builder.Property(o => o.WarehouseOperatorId);
        builder.Property(o => o.FulfillmentWarehouseId);
        builder.Property(o => o.BoxCount);
        builder.Property(o => o.WeightKg).HasPrecision(10, 3);
        builder.Property(o => o.CarrierName).HasMaxLength(200);
        builder.Property(o => o.TrackingCode).HasMaxLength(200);
        builder.Property(o => o.DdtNumber).HasMaxLength(50);
        builder.Property(o => o.PaymentMethod).HasMaxLength(50);
        builder.Property(o => o.PaymentTransactionDetails).HasMaxLength(500);
        builder.Property(o => o.ApprovalReason).HasMaxLength(500);
        builder.Property(o => o.HoldReason).HasMaxLength(500);

        builder.Property(o => o.OrderDate)
            .IsRequired();

        builder.Property(o => o.CustomerDiscountRate)
            .HasPrecision(5, 2);

        builder.Property(o => o.TaxableTotal)
            .HasPrecision(18, 2);

        builder.Property(o => o.VatTotal)
            .HasPrecision(18, 2);

        builder.Property(o => o.GrossTotal)
            .HasPrecision(18, 2);

        builder.Property(o => o.ShippingCost)
            .HasPrecision(18, 2)
            .HasDefaultValue(0.00m);

        builder.Property(o => o.ShippingVatRate)
            .HasPrecision(5, 2);

        builder.Property(o => o.ShippingNatureCode)
            .HasMaxLength(4);

        builder.Property(o => o.InternalNotes)
            .HasMaxLength(2000);

        builder.Property(o => o.TerminalId)
            .HasMaxLength(100);

        builder.Property(o => o.DocumentReference)
            .HasMaxLength(100);

        builder.Property(o => o.OrderType)
            .IsRequired()
            .HasDefaultValue(OrderType.Standard);

        builder.Property(o => o.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(o => o.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(o => !o.Contact.IsDeleted);

        // Relationships
        builder.HasOne(o => o.Contact)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.FulfillmentWarehouse)
            .WithMany()
            .HasForeignKey(o => o.FulfillmentWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.Shipments)
            .WithOne(shipment => shipment.Order)
            .HasForeignKey(shipment => shipment.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Shipments)
            .HasField("_shipments")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Index for order lookup by contact
        builder.HasIndex(o => new { o.ContactId, o.OrderDate })
            .IsDescending(false, true);

        // Unique index on OrderNumber
        builder.HasIndex(o => o.OrderNumber)
            .IsUnique();
    }
}