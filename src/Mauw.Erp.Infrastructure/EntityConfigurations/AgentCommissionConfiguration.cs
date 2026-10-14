using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

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
            .HasDefaultValue(CommissionStatus.Pending);

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