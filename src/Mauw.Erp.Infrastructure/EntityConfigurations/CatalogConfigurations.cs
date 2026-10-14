using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity configuration for Contact entities.
/// </summary>
public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.BusinessName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.VatNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.SdiCode)
            .HasMaxLength(50);

        builder.Property(c => c.Type)
            .IsRequired();

        builder.Property(c => c.DefaultDiscountRate)
            .HasPrecision(5, 2);

        builder.Property(c => c.DefaultCommissionRate)
            .HasPrecision(5, 2);

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Self-referencing foreign key for Agent
        builder.HasOne(c => c.Agent)
            .WithMany()
            .HasForeignKey(c => c.AgentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationships
        builder.HasMany(c => c.Orders)
            .WithOne(o => o.Contact)
            .HasForeignKey(o => o.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Commissions)
            .WithOne(ac => ac.Agent)
            .HasForeignKey(ac => ac.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique index for VAT Number
        builder.HasIndex(c => c.VatNumber)
            .IsUnique();
    }
}

/// <summary>
/// Entity configuration for Product entities.
/// </summary>
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.SupplierCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.InternalCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.DefaultMarkupRate)
            .HasPrecision(5, 2);

        builder.Property(p => p.VatRate)
            .HasPrecision(5, 2);

        builder.Property(p => p.ImageUrl)
            .HasMaxLength(500);

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(p => p.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasMany(p => p.Variants)
            .WithOne(v => v.Product)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique index for InternalCode
        builder.HasIndex(p => p.InternalCode)
            .IsUnique();

        // Unique index for SupplierCode per Supplier
        builder.HasIndex(p => new { p.SupplierId, p.SupplierCode })
            .IsUnique();
    }
}

/// <summary>
/// Entity configuration for ProductVariant entities.
/// </summary>
public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Size)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.Color)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.Barcode)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(v => v.PurchaseCost)
            .HasPrecision(18, 4);

        builder.Property(v => v.StockQuantity)
            .IsRequired();

        builder.Property(v => v.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(v => v.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.StockMovements)
            .WithOne(sm => sm.Variant)
            .HasForeignKey(sm => sm.VariantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique case-insensitive index on Barcode
        builder.HasIndex(v => v.Barcode)
            .IsUnique();
    }
}

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
            .HasPrecision(18, 4);

        builder.Property(sm => sm.ReferenceDoc)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(sm => sm.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(sm => sm.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(sm => sm.Variant)
            .WithMany(v => v.StockMovements)
            .HasForeignKey(sm => sm.VariantId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for faster variant lookups
        builder.HasIndex(sm => sm.VariantId);
    }
}
