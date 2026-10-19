using Mauw.Erp.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Mauw.Erp.Infrastructure.DbContext;

public sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.ToTable("price_lists");
        builder.HasKey(list => list.Id);
        builder.Property(list => list.Name).HasMaxLength(200).IsRequired();
        builder.Property(list => list.Currency).HasMaxLength(3).IsRequired();
        builder.HasMany(list => list.Rules).WithOne().HasForeignKey(rule => rule.PriceListId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(list => list.Rules).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class PriceListRuleConfiguration : IEntityTypeConfiguration<PriceListRule>
{
    public void Configure(EntityTypeBuilder<PriceListRule> builder)
    {
        builder.ToTable("price_list_rules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Id).ValueGeneratedNever();
        builder.Property(rule => rule.CalculationType).HasConversion<string>().HasMaxLength(30);
        builder.Property(rule => rule.Value).HasPrecision(22, 8);
        builder.Property(rule => rule.MinimumQuantity).HasPrecision(22, 8);
        builder.HasIndex(rule => new { rule.PriceListId, rule.VariantId });
        builder.HasIndex(rule => new { rule.PriceListId, rule.ProductId });
        builder.HasIndex(rule => new { rule.PriceListId, rule.CategoryId });
        builder.HasOne<ProductVariant>().WithMany().HasForeignKey(rule => rule.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>().WithMany().HasForeignKey(rule => rule.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(rule => rule.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
