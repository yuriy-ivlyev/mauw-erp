using Mauw.Erp.Contracts.Pricing;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Pricing;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Services;

namespace Mauw.Erp.Application.Services;

public sealed class PriceListService(IPriceListRepository repository, PriceListResolver resolver,
    CatalogPriceCalculator catalogPrices) : IPriceListService
{
    public async Task<IReadOnlyList<PriceListResponse>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(Map).ToList();

    public async Task<PriceListResponse?> GetAsync(Guid id, CancellationToken ct)
    {
        var list = await repository.GetAsync(id, ct);
        return list is null ? null : Map(list);
    }

    public async Task<OperationResult<PriceListResponse>> SaveAsync(
        Guid? id, SavePriceListRequest request, CancellationToken ct)
    {
        var list = id.HasValue ? await repository.GetAsync(id.Value, ct) : null;
        if (id.HasValue && list is null)
            return OperationResult<PriceListResponse>.Fail("Listino non trovato.");
        if (request.Rules is null || request.Rules.Any(rule => rule is null))
            return OperationResult<PriceListResponse>.Fail("Le regole non possono essere null.");
        if (request.Rules.Any(rule => rule.MinimumQuantity != decimal.Truncate(rule.MinimumQuantity)))
            return OperationResult<PriceListResponse>.Fail("La quantita minima della regola deve essere un numero intero.");
        if (request.Rules.Any(rule => rule.Id.HasValue && (list is null || list.Rules.All(old => old.Id != rule.Id))))
            return OperationResult<PriceListResponse>.Fail("Identificativo regola estraneo al listino.");
        var rules = request.Rules.Select(rule => new PriceRuleDefinition(
            rule.Id, rule.CalculationType, rule.Value, rule.VariantId, rule.ProductId,
            rule.CategoryId, rule.MinimumQuantity, rule.ValidFrom, rule.ValidTo, rule.Priority,
            rule.Description)).ToList();

        var validation = PriceList.Create(request.Name, request.Currency, request.IsActive, rules);
        if (!validation.Success)
            return OperationResult<PriceListResponse>.Fail(validation.Error);
        if (!await repository.TargetsExistAsync(
                rules.Where(r => r.VariantId.HasValue).Select(r => r.VariantId!.Value).Distinct().ToArray(),
                rules.Where(r => r.ProductId.HasValue).Select(r => r.ProductId!.Value).Distinct().ToArray(),
                rules.Where(r => r.CategoryId.HasValue).Select(r => r.CategoryId!.Value).Distinct().ToArray(), ct))
            return OperationResult<PriceListResponse>.Fail("Un bersaglio della regola non esiste.");

        if (list is null)
        {
            list = validation.Data!;
            await repository.AddAsync(list, ct);
        }
        else
        {
            var update = list.Update(request.Name, request.Currency, request.IsActive, rules);
            if (!update.Success)
                return OperationResult<PriceListResponse>.Fail(update.Error);
        }
        await repository.SaveChangesAsync(ct);
        return OperationResult<PriceListResponse>.Ok(Map(list));
    }

    public async Task<OperationResult<PriceListResponse>> SetActivationAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var list = await repository.GetAsync(id, ct);
        if (list is null)
            return OperationResult<PriceListResponse>.Fail("Listino non trovato.");
        list.SetActivation(isActive);
        await repository.SaveChangesAsync(ct);
        return OperationResult<PriceListResponse>.Ok(Map(list));
    }

    public async Task<OperationResult<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var list = await repository.GetAsync(id, ct);
        if (list is null)
            return OperationResult<bool>.Fail("Listino non trovato.");
        repository.Delete(list);
        await repository.SaveChangesAsync(ct);
        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<ResolvedPricesResponse>> ResolveAsync(ResolvePricesRequest request, CancellationToken ct)
    {
        if (request.Lines is null || request.Lines.Count < 1 ||
            request.Lines.Any(line => line is null || line.VariantId == Guid.Empty ||
                line.Quantity <= 0m || line.Quantity != decimal.Truncate(line.Quantity)) ||
            request.At == default)
            return OperationResult<ResolvedPricesResponse>.Fail("Specificare data e almeno una riga con quantita positiva.");
        var priceListId = request.PriceListId;
        if (request.ContactId.HasValue)
        {
            var customer = await repository.GetCustomerAsync(request.ContactId.Value, ct);
            if (customer is null)
                return OperationResult<ResolvedPricesResponse>.Fail("Profilo cliente non trovato.");
            priceListId ??= customer.PriceListId;
        }
        var list = priceListId.HasValue ? await repository.GetAsync(priceListId.Value, ct) : null;
        if (priceListId.HasValue && list is null)
            return OperationResult<ResolvedPricesResponse>.Fail("Listino non trovato.");
        var variants = (await repository.GetVariantsAsync(
            request.Lines.Select(line => line.VariantId).Distinct().ToArray(), ct)).ToDictionary(v => v.Id);
        var results = new List<ResolvedPriceLineResponse>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            if (!variants.TryGetValue(line.VariantId, out var variant))
                return OperationResult<ResolvedPricesResponse>.Fail("Variante non trovata.");
            var basePrice = catalogPrices.GetStoredPrice(variant);
            if (!basePrice.Success)
                return OperationResult<ResolvedPricesResponse>.Fail(basePrice.Error);
            var result = resolver.Resolve(list, new PriceResolutionInput(
                variant.Id, variant.ProductId, variant.Product.CategoryId,
                line.Quantity, basePrice.Data!.Value, request.At));
            if (!result.Success)
                return OperationResult<ResolvedPricesResponse>.Fail(result.Error);
            var price = result.Data!;
            results.Add(new ResolvedPriceLineResponse(variant.Id, price.PriceListId, price.RuleId,
                price.ListUnitPrice, price.DiscountPercent, price.AgreedUnitNetPrice, variant.Product.VatRate,
                price.DiscountReason));
        }
        return OperationResult<ResolvedPricesResponse>.Ok(new ResolvedPricesResponse(priceListId, results));
    }

    private static PriceListResponse Map(PriceList list) => new(
        list.Id, list.Name, list.Currency, list.IsActive,
        list.Rules.OrderByDescending(rule => rule.Priority).ThenBy(rule => rule.Id).Select(rule => new PriceRuleRequest
        {
            Id = rule.Id, CalculationType = rule.CalculationType, Value = rule.Value,
            VariantId = rule.VariantId, ProductId = rule.ProductId, CategoryId = rule.CategoryId,
            MinimumQuantity = rule.MinimumQuantity, ValidFrom = rule.ValidFrom, ValidTo = rule.ValidTo, Priority = rule.Priority,
            Description = rule.Description
        }).ToList());
}
