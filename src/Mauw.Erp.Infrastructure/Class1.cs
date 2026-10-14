using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure;

public sealed class MauwDbContext(DbContextOptions<MauwDbContext> options) : DbContext(options)
{
}
