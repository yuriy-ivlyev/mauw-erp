using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.HasKey(shipment => shipment.Id);

        builder.Property(shipment => shipment.ShipmentNumber)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(shipment => shipment.DdtNumber)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(shipment => shipment.CarrierName)
            .HasMaxLength(200);
        builder.Property(shipment => shipment.TrackingCode)
            .HasMaxLength(200);
        builder.Property(shipment => shipment.TotalWeightKg)
            .HasPrecision(10, 3);
        builder.Property(shipment => shipment.ShippedAtUtc)
            .IsRequired();
        builder.Property(shipment => shipment.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(shipment => shipment.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasMany(shipment => shipment.Lines)
            .WithOne(line => line.Shipment)
            .HasForeignKey(line => line.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(shipment => shipment.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(shipment => shipment.OrderId);
        builder.HasIndex(shipment => shipment.ShipmentNumber)
            .IsUnique();
        builder.HasIndex(shipment => shipment.DdtNumber)
            .IsUnique();
    }
}
