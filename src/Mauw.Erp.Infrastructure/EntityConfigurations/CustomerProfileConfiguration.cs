using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.ToTable("customer_profiles");
        builder.HasKey(cp => cp.ContactId);

        builder.Property(cp => cp.FinancialDiscountRate).HasPrecision(5, 2);
        builder.Property(cp => cp.CreditLimit).HasPrecision(18, 2);
        builder.Property(cp => cp.BlockReason).HasMaxLength(500);
        builder.Property(cp => cp.DeliveryNotes).HasMaxLength(1000);
        builder.HasOne(cp => cp.PriceList).WithMany().HasForeignKey(cp => cp.PriceListId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(cp => cp.Contact)
            .WithOne(c => c.CustomerProfile)
            .HasForeignKey<CustomerProfile>(cp => cp.ContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
