using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class CreditNoteItemConfiguration : IEntityTypeConfiguration<CreditNoteItem>
{
    public void Configure(EntityTypeBuilder<CreditNoteItem> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Description).IsRequired().HasMaxLength(500);
        builder.Property(item => item.IsShippingCharge).IsRequired();
        builder.Property(item => item.Quantity).HasPrecision(22, 8).IsRequired();
        builder.Property(item => item.UnitNetPrice).HasPrecision(22, 8).IsRequired();
        builder.Property(item => item.LineNetTotal).HasPrecision(18, 2).IsRequired();
        builder.Property(item => item.VatRate).HasPrecision(5, 2).IsRequired();
        builder.Property(item => item.LineVatTotal).HasPrecision(18, 2);
        builder.Property(item => item.LineGrossTotal).HasPrecision(18, 2);
        builder.Property(item => item.NatureCode).HasMaxLength(4);
        builder.Property(item => item.DiscountPercent).HasColumnType("numeric").IsRequired();
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(item => item.SourceInvoiceItem)
            .WithMany()
            .HasForeignKey(item => item.SourceInvoiceItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Variant)
            .WithMany()
            .HasForeignKey(item => item.VariantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.CreditNoteId, item.SourceInvoiceItemId });
    }
}
