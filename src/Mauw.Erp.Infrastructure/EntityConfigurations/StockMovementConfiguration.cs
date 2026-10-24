using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity configuration for StockMovement entities.
/// </summary>
public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.HasKey(sm => sm.Id);

        builder.Property(sm => sm.Type)
            .IsRequired();

        builder.Property(sm => sm.Quantity)
            .IsRequired();

        builder.Property(sm => sm.UnitCost)
            .HasPrecision(23, 8);

        builder.Property(sm => sm.ReferenceDoc)
            .IsRequired(false)
            .HasMaxLength(100);

        builder.Property(sm => sm.Reason)
            .IsRequired(false);

        builder.Property(sm => sm.MovementDateUtc)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(sm => sm.Notes)
            .HasMaxLength(500);

        builder.Property(sm => sm.CreatedByUserId)
            .HasMaxLength(200);

        builder.Property(sm => sm.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(sm => sm.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(sm => sm.Variant)
            .WithMany(v => v.StockMovements)
            .HasForeignKey(sm => sm.VariantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sm => sm.Warehouse)
            .WithMany()
            .HasForeignKey(sm => sm.WarehouseId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // Index for faster variant lookups
        builder.HasIndex(sm => new { sm.VariantId, sm.CreatedAt, sm.Id })
            .IsDescending(false, true, true);
        builder.HasIndex(sm => new { sm.WarehouseId, sm.VariantId });
        builder.HasIndex(sm => sm.MovementDateUtc);
        builder.HasOne(sm => sm.SupplierDeliveryNoteLine)
            .WithOne(line => line.StockMovement)
            .HasForeignKey<StockMovement>(sm => sm.SupplierDeliveryNoteLineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}