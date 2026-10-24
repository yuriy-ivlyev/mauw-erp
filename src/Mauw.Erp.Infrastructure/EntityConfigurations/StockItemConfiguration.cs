using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("stock_items");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.QuantityOnHand)
            .HasPrecision(18, 3);

        builder.Property(s => s.QuantityReserved)
            .HasPrecision(18, 3);

        builder.Property(s => s.UnitCost)
            .HasPrecision(23, 8)
            .IsRequired(false);

        builder.Property(s => s.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(s => s.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(s => !s.Warehouse.IsDeleted);

        builder.Ignore(s => s.QuantityAvailable);

        builder.HasIndex(s => s.WarehouseId);
        builder.HasIndex(s => new { s.WarehouseId, s.ProductVariantId })
            .IsUnique();

        builder.HasOne(s => s.Warehouse)
            .WithMany()
            .HasForeignKey(s => s.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ProductVariant)
            .WithMany()
            .HasForeignKey(s => s.ProductVariantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
