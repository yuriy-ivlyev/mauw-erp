using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

public interface IPriceFormulaRepository
{
    /// <summary>Lettura senza tracking con passaggi ordinati per posizione; null se non configurata.</summary>
    Task<PriceFormula?> GetAsync(CancellationToken ct);

    /// <summary>Come <see cref="GetAsync"/>, ma tracciata per l'aggiornamento.</summary>
    Task<PriceFormula?> GetForUpdateAsync(CancellationToken ct);

    Task AddAsync(PriceFormula formula, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
