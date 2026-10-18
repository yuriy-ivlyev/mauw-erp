using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity configuration for Order entities.
/// </summary>
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.Status)
            .IsRequired();

        builder.Property(o => o.OrderDate)
            .IsRequired();

        builder.Property(o => o.CustomerDiscountRate)
            .HasPrecision(5, 2);

        builder.Property(o => o.TaxableTotal)
            .HasPrecision(18, 2);

        builder.Property(o => o.VatTotal)
            .HasPrecision(18, 2);

        builder.Property(o => o.GrossTotal)
            .HasPrecision(18, 2);

        builder.Property(o => o.TerminalId)
            .HasMaxLength(100);

        builder.Property(o => o.DocumentReference)
            .HasMaxLength(100);

        builder.Property(o => o.OrderType)
            .IsRequired()
            .HasDefaultValue(OrderType.Standard);

        builder.Property(o => o.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(o => o.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(o => o.Contact)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items)
            .WithOne(oi => oi.Order)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for order lookup by contact
        builder.HasIndex(o => o.ContactId);

        // Unique index on OrderNumber
        builder.HasIndex(o => o.OrderNumber)
            .IsUnique();
    }
}