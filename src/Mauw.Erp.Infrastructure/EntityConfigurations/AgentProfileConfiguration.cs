using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public class AgentProfileConfiguration : IEntityTypeConfiguration<AgentProfile>
{
    public void Configure(EntityTypeBuilder<AgentProfile> builder)
    {
        builder.ToTable("agent_profiles");
        builder.HasKey(ap => ap.ContactId);

        builder.Property(ap => ap.DefaultCommissionRate).HasPrecision(5, 2);
        builder.Property(ap => ap.OperatingArea).HasMaxLength(255);

        builder.HasOne(ap => ap.Contact)
            .WithOne(c => c.AgentProfile)
            .HasForeignKey<AgentProfile>(ap => ap.ContactId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
