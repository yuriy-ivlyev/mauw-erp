using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class ShipmentLineConfiguration : IEntityTypeConfiguration<ShipmentLine>
{
    public void Configure(EntityTypeBuilder<ShipmentLine> builder)
    {
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Sku)
            .IsRequired()
            .HasMaxLength(100);
        builder.Property(line => line.QuantityShipped)
            .IsRequired();
        builder.Property(line => line.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(line => line.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(line => line.OrderLine)
            .WithMany()
            .HasForeignKey(line => line.OrderLineId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(line => line.Product)
            .WithMany()
            .HasForeignKey(line => line.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(line => new { line.ShipmentId, line.OrderLineId })
            .IsUnique();
        builder.HasIndex(line => line.OrderLineId);
    }
}
