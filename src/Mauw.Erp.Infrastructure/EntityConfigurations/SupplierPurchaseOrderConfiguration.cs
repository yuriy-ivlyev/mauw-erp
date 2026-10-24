using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class SupplierPurchaseOrderConfiguration : IEntityTypeConfiguration<SupplierPurchaseOrder>
{
    public void Configure(EntityTypeBuilder<SupplierPurchaseOrder> builder)
    {
        builder.ToTable("supplier_purchase_orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.OrderDate).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasIndex(x => x.OrderNumber).IsUnique();
        builder.HasIndex(x => new { x.Status, x.SupplierId, x.OrderDate });
        builder.HasQueryFilter(x => !x.Supplier.IsDeleted);
        builder.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne(x => x.SupplierPurchaseOrder)
            .HasForeignKey(x => x.SupplierPurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SupplierPurchaseOrderLineConfiguration : IEntityTypeConfiguration<SupplierPurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<SupplierPurchaseOrderLine> builder)
    {
        builder.ToTable("supplier_purchase_order_lines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuantityOrdered).IsRequired();
        builder.Property(x => x.QuantityReceived).IsRequired();
        builder.Property(x => x.UnitCost).HasPrecision(18, 8).IsRequired();
        builder.HasOne(x => x.ArticleVariant).WithMany().HasForeignKey(x => x.ArticleVariantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SupplierPurchaseOrderId, x.ArticleVariantId }).IsUnique();
    }
}

public sealed class SupplierReturnDraftConfiguration : IEntityTypeConfiguration<SupplierReturnDraft>
{
    public void Configure(EntityTypeBuilder<SupplierReturnDraft> builder)
    {
        builder.ToTable("supplier_return_drafts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne(x => x.SupplierPurchaseOrder).WithMany().HasForeignKey(x => x.SupplierPurchaseOrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SupplierDeliveryNote).WithMany().HasForeignKey(x => x.SupplierDeliveryNoteId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne(x => x.SupplierReturnDraft)
            .HasForeignKey(x => x.SupplierReturnDraftId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SupplierReturnDraftLineConfiguration : IEntityTypeConfiguration<SupplierReturnDraftLine>
{
    public void Configure(EntityTypeBuilder<SupplierReturnDraftLine> builder)
    {
        builder.ToTable("supplier_return_draft_lines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).IsRequired();
        builder.HasOne(x => x.SupplierDeliveryNoteLine).WithMany().HasForeignKey(x => x.SupplierDeliveryNoteLineId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ArticleVariant).WithMany().HasForeignKey(x => x.ArticleVariantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SupplierReturnDraftId, x.SupplierDeliveryNoteLineId }).IsUnique();
    }
}
