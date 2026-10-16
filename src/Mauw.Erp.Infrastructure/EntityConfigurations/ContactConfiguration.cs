using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Entity configuration for Contact (Party) entity.
/// Implements soft-delete global query filter and unique partial index on VatNumber.
/// </summary>
public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("contacts");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.BusinessName).IsRequired().HasMaxLength(255);
        builder.Property(c => c.VatNumber).HasMaxLength(20);
        builder.Property(c => c.TaxCode).HasMaxLength(16);
        builder.Property(c => c.SdiCode).HasMaxLength(7);
        builder.Property(c => c.PecEmail).HasMaxLength(255);
        builder.Property(c => c.CountryCode).IsRequired().HasMaxLength(3).HasDefaultValue("IT");
        builder.Property(c => c.FiscalRegime).HasMaxLength(10);
        builder.Property(c => c.Status).IsRequired();
        builder.Property(c => c.DeletedBy).HasMaxLength(255);

        // Corporate / Internal-Company fields
        builder.Property(c => c.ReaNumber).HasMaxLength(20);
        builder.Property(c => c.ReaOffice).HasMaxLength(5);
        builder.Property(c => c.ShareCapital).HasPrecision(18, 2);
        builder.Property(c => c.LogoUrl).HasMaxLength(1000);

        builder.Property(c => c.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(c => c.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Soft delete global filter
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Relationships preserved from previous model
        builder.HasMany(c => c.Orders)
            .WithOne(o => o.Contact)
            .HasForeignKey(o => o.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Commissions)
            .WithOne(ac => ac.Agent)
            .HasForeignKey(ac => ac.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Partial unique index on VatNumber (PostgreSQL allows multiple NULLs)
        builder.HasIndex(c => c.VatNumber)
            .IsUnique()
            .HasFilter("vat_number IS NOT NULL");

        builder.HasIndex(c => c.TaxCode)
            .HasFilter("tax_code IS NOT NULL");

        builder.HasIndex(c => c.BusinessName);
        builder.HasIndex(c => c.Status);
    }
}