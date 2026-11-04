using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class PayableDocumentLineConfiguration : IEntityTypeConfiguration<PayableDocumentLine>
{
    public void Configure(EntityTypeBuilder<PayableDocumentLine> builder)
    {
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Description).HasMaxLength(500).IsRequired();
        builder.Property(line => line.Category).HasConversion<int>().IsRequired();
        builder.Property(line => line.CostCenter).HasMaxLength(100);
        builder.Property(line => line.Project).HasMaxLength(100);
        builder.Property(line => line.TaxableAmount).HasPrecision(18, 2);
        builder.Property(line => line.VatRate).HasPrecision(5, 2);
        builder.Property(line => line.NatureCode).HasMaxLength(4);
        builder.Property(line => line.VatAmount).HasPrecision(18, 2);
        builder.Property(line => line.GrossAmount).HasPrecision(18, 2);
        builder.Property(line => line.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(line => line.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(line => line.PayableDocumentId);
    }
}
