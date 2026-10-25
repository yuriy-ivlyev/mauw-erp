using Mauw.Erp.Contracts.Pricing;

namespace Mauw.Erp.Application.Services;

public interface IPriceFormulaService
{
    Task<PriceFormulaDto> GetAsync(CancellationToken ct);
    Task<OperationResult<PriceFormulaDto>> SaveAsync(SavePriceFormulaRequest request, string? userId, CancellationToken ct);

    /// <summary>Calcola il prezzo finale dal costo d'acquisto senza salvare nulla.</summary>
    Task<OperationResult<PriceFormulaCalculationDto>> CalculateAsync(decimal purchaseCost, CancellationToken ct);
}
