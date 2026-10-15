using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasMany(m => m.Composition)
            .WithOne(mc => mc.Material)
            .HasForeignKey(mc => mc.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
