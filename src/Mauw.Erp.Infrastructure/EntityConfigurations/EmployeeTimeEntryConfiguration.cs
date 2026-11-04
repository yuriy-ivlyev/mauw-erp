using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class EmployeeTimeEntryConfiguration : IEntityTypeConfiguration<EmployeeTimeEntry>
{
    public void Configure(EntityTypeBuilder<EmployeeTimeEntry> builder)
    {
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Type).HasConversion<int>().IsRequired();
        builder.Property(entry => entry.WorkDate).IsRequired();
        builder.Property(entry => entry.StartTime);
        builder.Property(entry => entry.EndTime);
        builder.Property(entry => entry.BreakMinutes).IsRequired();
        builder.Property(entry => entry.Notes).HasMaxLength(1000);
        builder.Property(entry => entry.CreatedByUserId).HasMaxLength(200);
        builder.Property(entry => entry.Version).IsConcurrencyToken();
        builder.Property(entry => entry.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(entry => entry.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Ignore(entry => entry.WorkedMinutes);
        builder.HasIndex(entry => new { entry.EmployeeId, entry.WorkDate }).IsUnique();
        builder.HasIndex(entry => entry.WorkDate);
    }
}
