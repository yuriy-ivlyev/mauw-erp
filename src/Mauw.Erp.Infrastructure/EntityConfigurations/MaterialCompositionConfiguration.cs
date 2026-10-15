using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public class MaterialCompositionConfiguration : IEntityTypeConfiguration<MaterialComposition>
{
    public void Configure(EntityTypeBuilder<MaterialComposition> builder)
    {
        builder.HasKey(mc => mc.Id);

        builder.Property(mc => mc.ComponentName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(mc => mc.PercentageValue)
            .HasPrecision(5, 2);

        builder.HasOne(mc => mc.Material)
            .WithMany(m => m.Composition)
            .HasForeignKey(mc => mc.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
