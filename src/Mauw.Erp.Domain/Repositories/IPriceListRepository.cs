using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

public interface IPriceListRepository
{
    Task<IReadOnlyList<PriceList>> GetAllAsync(CancellationToken ct);
    Task<PriceList?> GetAsync(Guid id, CancellationToken ct);
    Task AddAsync(PriceList list, CancellationToken ct);
    void Delete(PriceList list);
    Task SaveChangesAsync(CancellationToken ct);
    Task<IReadOnlyList<ProductVariant>> GetVariantsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task<CustomerProfile?> GetCustomerAsync(Guid contactId, CancellationToken ct);
    Task<bool> TargetsExistAsync(IReadOnlyCollection<Guid> variantIds,
        IReadOnlyCollection<Guid> productIds, IReadOnlyCollection<Guid> categoryIds, CancellationToken ct);
    Task<IReadOnlyList<InvoiceItem>> GetSourceInvoiceLinesAsync(
        IReadOnlyCollection<Guid> lineIds, Guid contactId, CancellationToken ct);
    Task<IReadOnlyList<OrderItem>> GetSourceOrderLinesAsync(
        IReadOnlyCollection<Guid> lineIds, Guid contactId, CancellationToken ct);
}
