using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mauw.Erp.Infrastructure.DbContext;

/// <summary>
/// Design-time DbContext factory for EF Core migrations.
/// </summary>
public class MauwDbContextFactory : IDesignTimeDbContextFactory<MauwDbContext>
{
    public MauwDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MauwDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=mauw_erp;Username=postgres;Password=postgres;Port=5432");
        return new(optionsBuilder.Options);
    }
}
