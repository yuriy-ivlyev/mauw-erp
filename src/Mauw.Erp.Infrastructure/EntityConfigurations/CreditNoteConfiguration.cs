using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

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
        builder.Property(c => c.AllocatedAmount)
            .HasPrecision(18, 2)
            .HasDefaultValue(0.00m)
            .IsRequired();
        builder.Ignore(c => c.RemainingCredit);
        builder.Property(c => c.Reason).HasMaxLength(500);
        builder.Property(c => c.RelatedInvoiceNumber).HasMaxLength(50);
        builder.Property(c => c.ReturnToWarehouse).IsRequired();
        builder.Property(c => c.SdiFileName).HasMaxLength(50);
        builder.Property(c => c.SdiStorageKey).HasMaxLength(255);
        builder.Property(c => c.SdiTransmissionId).HasMaxLength(100);
        builder.Property(c => c.SdiStatus)
            .HasConversion<int>()
            .HasDefaultValue(SdiStatus.NotTransmitted)
            .IsRequired();
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(c => c.Invoice)
            .WithMany(i => i.CreditNotes)
            .HasForeignKey(c => c.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Warehouse)
            .WithMany()
            .HasForeignKey(c => c.WarehouseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Items)
            .WithOne(item => item.CreditNote)
            .HasForeignKey(item => item.CreditNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.InvoiceId);
        builder.HasIndex(c => c.CreditNoteNumber).IsUnique();
    }
}
