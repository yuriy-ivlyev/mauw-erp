using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.EntityConfigurations;

public class CassaSessionConfiguration : IEntityTypeConfiguration<CassaSession>
{
    public void Configure(EntityTypeBuilder<CassaSession> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SessionCode).IsRequired().HasMaxLength(50);
        builder.Property(s => s.CartJson).IsRequired();
        builder.Property(s => s.ContactName).HasMaxLength(200);
        builder.Property(s => s.ContactId).HasMaxLength(100);
        builder.Property(s => s.DocumentNumber).HasMaxLength(100);
        builder.Property(s => s.TerminalId).HasMaxLength(100);
        builder.Property(s => s.Status).HasConversion<int>();
        builder.HasIndex(s => s.SessionCode).IsUnique();
        builder.HasIndex(s => s.Status);
    }
}
