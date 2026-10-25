using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity configuration for Product entities.
/// </summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.SellingPrice).HasPrecision(22, 8);

        builder.Property(p => p.SupplierCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Barcode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.VatRate)
            .HasPrecision(5, 2);

        builder.Property(p => p.HasVariants)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(p => p.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasMany(p => p.Variants)
            .WithOne(v => v.Product)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Collection)
            .WithMany()
            .HasForeignKey(p => p.CollectionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Material)
            .WithMany()
            .HasForeignKey(p => p.MaterialId)
            .OnDelete(DeleteBehavior.SetNull);

        // Product barcodes are unique across the catalog.
        builder.HasIndex(p => p.Barcode)
            .IsUnique();

        // Unique index for SupplierCode per Supplier
        builder.HasIndex(p => new { p.SupplierId, p.SupplierCode })
            .IsUnique();

        builder.HasIndex(p => p.SupplierCode)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("i_x_products_supplier_code_trgm");

        builder.HasIndex(p => p.Name)
            .HasMethod("gin")
            .HasOperators("gin_trgm_ops")
            .HasDatabaseName("i_x_products_name_trgm");
    }
}