using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity configuration for Invoice entities.
/// </summary>
public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(i => i.InvoiceDate)
            .IsRequired();

        builder.Property(i => i.TaxableTotal)
            .HasPrecision(18, 2);

        builder.Property(i => i.VatTotal)
            .HasPrecision(18, 2);

        builder.Property(i => i.GrossTotal)
            .HasPrecision(18, 2);

        builder.Property(i => i.PaidAmount)
            .HasPrecision(18, 2);

        builder.Property(i => i.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(i => i.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(i => i.Contact)
            .WithMany()
            .HasForeignKey(i => i.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.Items)
            .WithOne(ii => ii.Invoice)
            .HasForeignKey(ii => ii.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Payments)
            .WithOne(p => p.Invoice)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.Commissions)
            .WithOne(ac => ac.Invoice)
            .HasForeignKey(ac => ac.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for invoice lookup by contact
        builder.HasIndex(i => i.ContactId);

        // Unique index on InvoiceNumber
        builder.HasIndex(i => i.InvoiceNumber)
            .IsUnique();
    }
}

/// <summary>
/// Entity configuration for InvoiceItem entities.
/// </summary>
public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.HasKey(ii => ii.Id);

        builder.Property(ii => ii.Quantity)
            .IsRequired();

        builder.Property(ii => ii.UnitNetPrice)
            .HasPrecision(18, 4);

        builder.Property(ii => ii.LineNetTotal)
            .HasPrecision(18, 2);

        builder.Property(ii => ii.VatRate)
            .HasPrecision(5, 2);

        builder.Property(ii => ii.LineVatTotal)
            .HasPrecision(18, 2);

        builder.Property(ii => ii.LineGrossTotal)
            .HasPrecision(18, 2);

        builder.Property(ii => ii.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(ii => ii.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(ii => ii.Invoice)
            .WithMany(i => i.Items)
            .HasForeignKey(ii => ii.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ii => ii.Variant)
            .WithMany()
            .HasForeignKey(ii => ii.VariantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Entity configuration for Payment entities.
/// </summary>
public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount)
            .HasPrecision(18, 2);

        builder.Property(p => p.PaymentDate)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(p => p.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(p => p.Invoice)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for payment lookup by invoice
        builder.HasIndex(p => p.InvoiceId);
    }
}

/// <summary>
/// Entity configuration for AgentCommission entities.
/// Tracks commissions calculated on Net Taxable Amount (Imponibile Netto merce),
/// excluding VAT and transport fees.
/// </summary>
public class AgentCommissionConfiguration : IEntityTypeConfiguration<AgentCommission>
{
    public void Configure(EntityTypeBuilder<AgentCommission> builder)
    {
        builder.HasKey(ac => ac.Id);

        builder.Property(ac => ac.TaxableAmountBase)
            .HasPrecision(18, 2);

        builder.Property(ac => ac.CommissionRate)
            .HasPrecision(5, 2);

        builder.Property(ac => ac.CommissionAmount)
            .HasPrecision(18, 2);

        builder.Property(ac => ac.Status)
            .IsRequired()
            .HasDefaultValue("Pending");

        builder.Property(ac => ac.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(ac => ac.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Relationships
        builder.HasOne(ac => ac.Agent)
            .WithMany(c => c.Commissions)
            .HasForeignKey(ac => ac.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(ac => ac.Invoice)
            .WithMany(i => i.Commissions)
            .HasForeignKey(ac => ac.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for commission lookup by agent and status
        builder.HasIndex(ac => new { ac.AgentId, ac.Status });

        // Index for commission lookup by invoice
        builder.HasIndex(ac => ac.InvoiceId);
    }
}
