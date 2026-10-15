using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public class MacroCategoryConfiguration : IEntityTypeConfiguration<MacroCategory>
{
    public void Configure(EntityTypeBuilder<MacroCategory> builder)
    {
        builder.HasKey(mc => mc.Id);

        builder.Property(mc => mc.Name)
            .IsRequired()
            .HasMaxLength(255);
    }
}
