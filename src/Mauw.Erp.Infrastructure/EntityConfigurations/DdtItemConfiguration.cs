using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity configuration for DdtItem entities.
/// </summary>
public class DdtItemConfiguration : IEntityTypeConfiguration<DdtItem>
{
    public void Configure(EntityTypeBuilder<DdtItem> builder)
    {
        builder.HasKey(di => di.Id);

        builder.Property(di => di.Quantity)
            .IsRequired();

        builder.Property(di => di.UnitNetPrice)
            .HasPrecision(18, 4);

        builder.Property(di => di.LineNetTotal)
            .HasPrecision(18, 2);

        builder.Property(di => di.VatRate)
            .HasPrecision(5, 2);

        builder.Property(di => di.LineVatTotal)
            .HasPrecision(18, 2);

        builder.Property(di => di.LineGrossTotal)
            .HasPrecision(18, 2);

        builder.Property(di => di.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(di => di.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(di => di.DdtDocument)
            .WithMany(d => d.Items)
            .HasForeignKey(di => di.DdtDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(di => di.Variant)
            .WithMany()
            .HasForeignKey(di => di.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}