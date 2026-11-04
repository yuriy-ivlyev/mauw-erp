using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class PayableDocumentConfiguration : IEntityTypeConfiguration<PayableDocument>
{
    public void Configure(EntityTypeBuilder<PayableDocument> builder)
    {
        builder.HasKey(document => document.Id);
        builder.Property(document => document.Kind).HasConversion<int>().IsRequired();
        builder.Property(document => document.DocumentCode).HasMaxLength(19).IsRequired();
        builder.Property(document => document.SourceDocumentNumber).HasMaxLength(100);
        builder.Property(document => document.DocumentDate).IsRequired();
        builder.Property(document => document.DueDate).IsRequired();
        builder.Property(document => document.BeneficiaryName).HasMaxLength(200);
        builder.Property(document => document.CreatedByUserId).HasMaxLength(200);
        builder.Property(document => document.ApprovedByUserId).HasMaxLength(200);
        builder.Property(document => document.SubmittedAtUtc);
        builder.Property(document => document.ApprovedAtUtc);
        builder.Property(document => document.RejectionReason).HasMaxLength(500);
        builder.Property(document => document.Notes).HasMaxLength(2000);
        builder.Property(document => document.Status).HasConversion<int>().IsRequired();
        builder.Property(document => document.TaxableTotal).HasPrecision(18, 2);
        builder.Property(document => document.VatTotal).HasPrecision(18, 2);
        builder.Property(document => document.GrossTotal).HasPrecision(18, 2);
        builder.Property(document => document.PaidAmount).HasPrecision(18, 2);
        builder.Property(document => document.AttachmentKey).HasMaxLength(500);
        builder.Property(document => document.AttachmentFileName).HasMaxLength(255);
        builder.Property(document => document.AttachmentContentType).HasMaxLength(100);
        builder.Property(document => document.Version).IsConcurrencyToken();
        builder.Property(document => document.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(document => document.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(document => document.Supplier)
            .WithMany()
            .HasForeignKey(document => document.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(document => document.Lines)
            .WithOne(line => line.PayableDocument)
            .HasForeignKey(line => line.PayableDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(document => document.Payments)
            .WithOne(payment => payment.PayableDocument)
            .HasForeignKey(payment => payment.PayableDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(document => document.DocumentCode).IsUnique();
        builder.HasIndex(document => new
            {
                document.SupplierId,
                document.SourceDocumentNumber,
                document.DocumentDate
            })
            .IsUnique()
            .HasFilter("supplier_id IS NOT NULL AND source_document_number IS NOT NULL");
        builder.HasIndex(document => new { document.Status, document.DueDate });
        builder.HasIndex(document => new { document.Kind, document.DocumentDate });
        builder.HasIndex(document => document.CreatedByUserId);
    }
}
