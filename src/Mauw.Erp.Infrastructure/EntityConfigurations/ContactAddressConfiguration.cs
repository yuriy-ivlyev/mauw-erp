using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public class ContactAddressConfiguration : IEntityTypeConfiguration<ContactAddress>
{
    public void Configure(EntityTypeBuilder<ContactAddress> builder)
    {
        builder.ToTable("contact_addresses");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Purpose).IsRequired();
        builder.Property(a => a.SiteName).HasMaxLength(150);
        builder.Property(a => a.Street).IsRequired().HasMaxLength(255);
        builder.Property(a => a.ZipCode).IsRequired().HasMaxLength(10);
        builder.Property(a => a.City).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Province).HasMaxLength(100);  // supporta stati esteri (es. "California")
        builder.Property(a => a.CountryCode).IsRequired().HasMaxLength(3).HasDefaultValue("IT");
        builder.Property(a => a.Phone).HasMaxLength(30);
        builder.Property(a => a.Email).HasMaxLength(255);
        builder.Property(a => a.AccessNotes).HasMaxLength(500);
        builder.Property(a => a.FormattedAddress).HasMaxLength(500);
        builder.Property(a => a.ExternalPlaceId).HasMaxLength(100);

        builder.Property(a => a.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(a => a.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasQueryFilter(a => !a.Contact.IsDeleted);

        builder.HasOne(a => a.Contact)
            .WithMany(c => c.Addresses)
            .HasForeignKey(a => a.ContactId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.ContactId, a.Purpose });
        builder.HasIndex(a => new { a.Latitude, a.Longitude })
            .HasFilter("latitude IS NOT NULL AND longitude IS NOT NULL");
    }
}
