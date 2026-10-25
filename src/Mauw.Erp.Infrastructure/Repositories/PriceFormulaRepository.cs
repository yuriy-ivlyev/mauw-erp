using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public sealed class PriceFormulaRepository(MauwDbContext db) : IPriceFormulaRepository
{
    public Task<PriceFormula?> GetAsync(CancellationToken ct) =>
        db.PriceFormulas.AsNoTracking().Include(formula => formula.Steps.OrderBy(step => step.Position))
            .FirstOrDefaultAsync(ct);

    public Task<PriceFormula?> GetForUpdateAsync(CancellationToken ct) =>
        db.PriceFormulas.Include(formula => formula.Steps.OrderBy(step => step.Position))
            .FirstOrDefaultAsync(ct);

    public async Task AddAsync(PriceFormula formula, CancellationToken ct) =>
        await db.PriceFormulas.AddAsync(formula, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
