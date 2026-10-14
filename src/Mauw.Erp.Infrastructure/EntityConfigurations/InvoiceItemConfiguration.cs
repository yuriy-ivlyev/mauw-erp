using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity configuration for InvoiceItem entities.
/// </summary>
public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.HasKey(ii => ii.Id);

        builder.Property(ii => ii.Quantity)
            .IsRequired();

        builder.Property(ii => ii.UnitNetPrice)
            .HasPrecision(18, 4);

        builder.Property(ii => ii.LineNetTotal)
            .HasPrecision(18, 2);

        builder.Property(ii => ii.VatRate)
            .HasPrecision(5, 2);

        builder.Property(ii => ii.LineVatTotal)
            .HasPrecision(18, 2);

        builder.Property(ii => ii.LineGrossTotal)
            .HasPrecision(18, 2);

        builder.Property(ii => ii.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(ii => ii.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(ii => ii.Invoice)
            .WithMany(i => i.Items)
            .HasForeignKey(ii => ii.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ii => ii.Variant)
            .WithMany()
            .HasForeignKey(ii => ii.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}