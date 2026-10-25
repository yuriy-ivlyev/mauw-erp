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
        builder.Property(v => v.SellingPrice).HasPrecision(22, 8);
        builder.Property(v => v.MarkupRate).HasPrecision(6, 2);

        builder.Property(v => v.Barcode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.SupplierCode)
            .HasMaxLength(100);

        builder.HasIndex(v => new { v.SupplierId, v.SupplierCode })
            .IsUnique();

        builder.Property(v => v.PurchaseCost)
            .HasPrecision(18, 4);

        builder.Property(v => v.WeightGrams)
            .HasPrecision(10, 2)
            .IsRequired(false);

        builder.Property(v => v.VarianteVetrina)
            .HasDefaultValue(false);

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

        builder.HasIndex(v => v.SupplierCode)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("i_x_product_variants_supplier_code_trgm");
    }
}