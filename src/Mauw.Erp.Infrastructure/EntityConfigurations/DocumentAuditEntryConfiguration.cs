using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public class DocumentAuditEntryConfiguration : IEntityTypeConfiguration<DocumentAuditEntry>
{
    public void Configure(EntityTypeBuilder<DocumentAuditEntry> builder)
    {
        builder.ToTable("document_audit_entries");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.DocumentType).IsRequired().HasMaxLength(50);
        builder.Property(entry => entry.DocumentId).IsRequired();
        builder.Property(entry => entry.Action).IsRequired().HasMaxLength(100);
        builder.Property(entry => entry.Note).IsRequired().HasMaxLength(DocumentAuditEntry.NoteMaxLength);
        builder.Property(entry => entry.Details).HasMaxLength(DocumentAuditEntry.DetailsMaxLength);
        builder.Property(entry => entry.UserId).HasMaxLength(200);
        builder.Property(entry => entry.UserName).HasMaxLength(200);
        builder.Property(entry => entry.OccurredAtUtc).IsRequired();

        builder.Property(entry => entry.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(entry => entry.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(entry => new { entry.DocumentType, entry.DocumentId, entry.OccurredAtUtc });
    }
}
