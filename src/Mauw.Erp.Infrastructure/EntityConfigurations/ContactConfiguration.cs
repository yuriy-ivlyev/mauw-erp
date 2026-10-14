using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity configuration for Contact entities.
/// </summary>
public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.BusinessName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.VatNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.SdiCode)
            .HasMaxLength(50);

        builder.Property(c => c.Type)
            .IsRequired();

        builder.Property(c => c.DefaultDiscountRate)
            .HasPrecision(5, 2);

        builder.Property(c => c.DefaultCommissionRate)
            .HasPrecision(5, 2);

        builder.Property(c => c.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.Property(c => c.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Self-referencing foreign key for Agent
        builder.HasOne(c => c.Agent)
            .WithMany()
            .HasForeignKey(c => c.AgentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationships
        builder.HasMany(c => c.Orders)
            .WithOne(o => o.Contact)
            .HasForeignKey(o => o.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Commissions)
            .WithOne(ac => ac.Agent)
            .HasForeignKey(ac => ac.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique index for VAT Number
        builder.HasIndex(c => c.VatNumber)
            .IsUnique();
    }
}