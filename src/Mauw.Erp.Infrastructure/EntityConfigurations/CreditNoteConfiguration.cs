using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Infrastructure.DbContext;

public class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
{
    public void Configure(EntityTypeBuilder<CreditNote> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.CreditNoteNumber).IsRequired().HasMaxLength(50);
        builder.Property(c => c.CreditNoteDate).IsRequired();
        builder.Property(c => c.TaxableTotal).HasPrecision(18, 2);
        builder.Property(c => c.VatTotal).HasPrecision(18, 2);
        builder.Property(c => c.GrossTotal).HasPrecision(18, 2);
        builder.Property(c => c.Reason).HasMaxLength(500);
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(c => c.Invoice)
            .WithMany(i => i.CreditNotes)
            .HasForeignKey(c => c.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.InvoiceId);
        builder.HasIndex(c => c.CreditNoteNumber).IsUnique();
    }
}
