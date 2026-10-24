using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public class SupplierProfileConfiguration : IEntityTypeConfiguration<SupplierProfile>
{
    public void Configure(EntityTypeBuilder<SupplierProfile> builder)
    {
        builder.ToTable("supplier_profiles");
        builder.HasKey(sp => sp.ContactId);

        builder.Property(sp => sp.Iban).HasMaxLength(34);
        builder.Property(sp => sp.BicSwift).HasMaxLength(11);

        builder.HasQueryFilter(sp => !sp.Contact.IsDeleted);

        builder.HasOne(sp => sp.Contact)
            .WithOne(c => c.SupplierProfile)
            .HasForeignKey<SupplierProfile>(sp => sp.ContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
