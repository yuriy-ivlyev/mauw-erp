using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity configuration for ProductVariant entities.
/// </summary>
public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Barcode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.PurchaseCost)
            .HasPrecision(18, 4);

        builder.Property(v => v.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(v => v.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.Color)
            .WithMany()
            .HasForeignKey(v => v.ColorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(v => v.Size)
            .WithMany()
            .HasForeignKey(v => v.SizeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(v => v.StockMovements)
            .WithOne(sm => sm.Variant)
            .HasForeignKey(sm => sm.VariantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique case-insensitive index on Barcode
        builder.HasIndex(v => v.Barcode)
            .IsUnique();
    }
}