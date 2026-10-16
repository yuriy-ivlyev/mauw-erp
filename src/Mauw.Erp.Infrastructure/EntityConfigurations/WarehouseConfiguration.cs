using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("warehouses");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Code).IsRequired().HasMaxLength(20);
        builder.Property(w => w.Name).IsRequired().HasMaxLength(200);
        builder.Property(w => w.Description).HasMaxLength(500);
        builder.Property(w => w.DeletedBy).HasMaxLength(200);
        builder.Property(w => w.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(w => w.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(w => w.Code)
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasQueryFilter(w => !w.IsDeleted);

        builder.HasOne(w => w.Address)
            .WithMany()
            .HasForeignKey(w => w.ContactAddressId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
