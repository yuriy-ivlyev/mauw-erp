using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        builder.HasKey(document => document.Id);
        builder.Property(document => document.Kind).HasConversion<int>().IsRequired();
        builder.Property(document => document.Title).HasMaxLength(150).IsRequired();
        builder.Property(document => document.DocumentNumber).HasMaxLength(100);
        builder.Property(document => document.IssuerOrProvider).HasMaxLength(150);
        builder.Property(document => document.Notes).HasMaxLength(1000);
        builder.Property(document => document.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(document => document.FileName).HasMaxLength(255).IsRequired();
        builder.Property(document => document.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(document => document.FileLength).IsRequired();
        builder.Property(document => document.CreatedByUserId).HasMaxLength(200);
        builder.Property(document => document.Version).IsConcurrencyToken();
        builder.Property(document => document.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(document => document.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(document => new { document.EmployeeId, document.CreatedAt });
    }
}
