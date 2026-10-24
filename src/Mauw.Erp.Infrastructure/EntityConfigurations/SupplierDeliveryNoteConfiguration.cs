using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class SupplierDeliveryNoteConfiguration : IEntityTypeConfiguration<SupplierDeliveryNote>
{
    public void Configure(EntityTypeBuilder<SupplierDeliveryNote> builder)
    {
        builder.ToTable("supplier_delivery_notes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DocumentNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasQueryFilter(x => !x.Supplier.IsDeleted);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SupplierPurchaseOrder).WithMany().HasForeignKey(x => x.SupplierPurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(x => x.Lines).WithOne(x => x.SupplierDeliveryNote)
            .HasForeignKey(x => x.SupplierDeliveryNoteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.UploadedAt, x.Id }).IsDescending(true, true);
    }
}

public sealed class SupplierDeliveryNoteLineConfiguration : IEntityTypeConfiguration<SupplierDeliveryNoteLine>
{
    public void Configure(EntityTypeBuilder<SupplierDeliveryNoteLine> builder)
    {
        builder.ToTable("supplier_delivery_note_lines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SupplierItemCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Quantity).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 8);
        builder.HasOne(x => x.ArticleVariant).WithMany().HasForeignKey(x => x.ArticleVariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SupplierPurchaseOrderLine).WithMany().HasForeignKey(x => x.SupplierPurchaseOrderLineId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(x => new { x.SupplierDeliveryNoteId, x.Position });
    }
}

public sealed class SupplierArticleMappingConfiguration : IEntityTypeConfiguration<SupplierArticleMapping>
{
    public void Configure(EntityTypeBuilder<SupplierArticleMapping> builder)
    {
        builder.ToTable("supplier_article_mappings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SupplierItemCode).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.SupplierId, x.SupplierItemCode }).IsUnique();
        builder.HasQueryFilter(x => !x.Supplier.IsDeleted);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ArticleVariant).WithMany().HasForeignKey(x => x.ArticleVariantId).OnDelete(DeleteBehavior.Restrict);
    }
}
