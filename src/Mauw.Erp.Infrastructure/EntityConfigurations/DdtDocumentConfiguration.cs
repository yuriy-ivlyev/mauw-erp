using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity configuration for DdtDocument entities.
/// </summary>
public class DdtDocumentConfiguration : IEntityTypeConfiguration<DdtDocument>
{
    public void Configure(EntityTypeBuilder<DdtDocument> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.OrderId);

        builder.Property(d => d.DdtNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.DdtDate)
            .IsRequired();

        builder.Property(d => d.TaxableTotal)
            .HasPrecision(18, 2);

        builder.Property(d => d.VatTotal)
            .HasPrecision(18, 2);

        builder.Property(d => d.GrossTotal)
            .HasPrecision(18, 2);

        builder.Property(d => d.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(d => d.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(d => d.Contact)
            .WithMany()
            .HasForeignKey(d => d.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Items)
            .WithOne(di => di.DdtDocument)
            .HasForeignKey(di => di.DdtDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.OrderId);
        builder.HasIndex(d => d.DdtNumber)
            .IsUnique();
    }
}
