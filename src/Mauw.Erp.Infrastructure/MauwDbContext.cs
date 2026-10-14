using Mauw.Erp.Domain;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure;

public class MauwDbContext : DbContext
{
    public MauwDbContext(DbContextOptions<MauwDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasCollation(
            "case_insensitive",
            locale: "und-u-ks-level2",
            provider: "icu",
            deterministic: false);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MauwDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
