using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mauw.Erp.Domain.Entities;

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

/// <summary>
/// Entity configuration for OrderItem entities.
/// </summary>
public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.HasKey(oi => oi.Id);

        builder.Property(oi => oi.Quantity)
            .IsRequired();

        builder.Property(oi => oi.UnitNetPrice)
            .HasPrecision(18, 4);

        builder.Property(oi => oi.LineNetTotal)
            .HasPrecision(18, 2);

        builder.Property(oi => oi.VatRate)
            .HasPrecision(5, 2);

        builder.Property(oi => oi.LineVatTotal)
            .HasPrecision(18, 2);

        builder.Property(oi => oi.LineGrossTotal)
            .HasPrecision(18, 2);

        builder.Property(oi => oi.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(oi => oi.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(oi => oi.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(oi => oi.Variant)
            .WithMany()
            .HasForeignKey(oi => oi.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Entity configuration for DdtDocument entities.
/// </summary>
public class DdtDocumentConfiguration : IEntityTypeConfiguration<DdtDocument>
{
    public void Configure(EntityTypeBuilder<DdtDocument> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DdtNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(d => d.DdtDate)
            .IsRequired();

        builder.Property(d => d.TaxableTotal)
            .HasPrecision(18, 2);

        builder.Property(d => d.VatTotal)
            .HasPrecision(18, 2);

        builder.Property(d => d.GrossTotal)
            .HasPrecision(18, 2);

        builder.Property(d => d.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(d => d.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(d => d.Contact)
            .WithMany()
            .HasForeignKey(d => d.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.Items)
            .WithOne(di => di.DdtDocument)
            .HasForeignKey(di => di.DdtDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique index on DdtNumber
        builder.HasIndex(d => d.DdtNumber)
            .IsUnique();
    }
}

/// <summary>
/// Entity configuration for DdtItem entities.
/// </summary>
public class DdtItemConfiguration : IEntityTypeConfiguration<DdtItem>
{
    public void Configure(EntityTypeBuilder<DdtItem> builder)
    {
        builder.HasKey(di => di.Id);

        builder.Property(di => di.Quantity)
            .IsRequired();

        builder.Property(di => di.UnitNetPrice)
            .HasPrecision(18, 4);

        builder.Property(di => di.LineNetTotal)
            .HasPrecision(18, 2);

        builder.Property(di => di.VatRate)
            .HasPrecision(5, 2);

        builder.Property(di => di.LineVatTotal)
            .HasPrecision(18, 2);

        builder.Property(di => di.LineGrossTotal)
            .HasPrecision(18, 2);

        builder.Property(di => di.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(di => di.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(di => di.DdtDocument)
            .WithMany(d => d.Items)
            .HasForeignKey(di => di.DdtDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(di => di.Variant)
            .WithMany()
            .HasForeignKey(di => di.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
