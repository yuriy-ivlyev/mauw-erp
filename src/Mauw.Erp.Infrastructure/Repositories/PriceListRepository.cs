using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

public sealed class PriceListRepository(MauwDbContext db) : IPriceListRepository
{
    public async Task<IReadOnlyList<PriceList>> GetAllAsync(CancellationToken ct) =>
        await db.PriceLists.AsNoTracking().Include(list => list.Rules).OrderBy(list => list.Name).ToListAsync(ct);

    public Task<PriceList?> GetAsync(Guid id, CancellationToken ct) =>
        db.PriceLists.Include(list => list.Rules).FirstOrDefaultAsync(list => list.Id == id, ct);

    public async Task AddAsync(PriceList list, CancellationToken ct) => await db.PriceLists.AddAsync(list, ct);
    public void Delete(PriceList list) => db.PriceLists.Remove(list);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<ProductVariant>> GetVariantsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.ProductVariants.AsNoTracking().Include(variant => variant.Product)
            .Where(variant => ids.Contains(variant.Id)).ToListAsync(ct);

    public Task<CustomerProfile?> GetCustomerAsync(Guid contactId, CancellationToken ct) =>
        db.CustomerProfiles.AsNoTracking().FirstOrDefaultAsync(profile => profile.ContactId == contactId, ct);

    public async Task<bool> TargetsExistAsync(IReadOnlyCollection<Guid> variantIds,
        IReadOnlyCollection<Guid> productIds, IReadOnlyCollection<Guid> categoryIds, CancellationToken ct) =>
        await db.ProductVariants.CountAsync(v => variantIds.Contains(v.Id), ct) == variantIds.Count &&
        await db.Products.CountAsync(p => productIds.Contains(p.Id), ct) == productIds.Count &&
        await db.Categories.CountAsync(c => categoryIds.Contains(c.Id), ct) == categoryIds.Count;

    public async Task<IReadOnlyList<InvoiceItem>> GetSourceInvoiceLinesAsync(
        IReadOnlyCollection<Guid> lineIds, Guid contactId, CancellationToken ct) =>
        await db.InvoiceItems.AsNoTracking().Where(line =>
            lineIds.Contains(line.Id) && line.Invoice.ContactId == contactId).ToListAsync(ct);

    public async Task<IReadOnlyList<OrderItem>> GetSourceOrderLinesAsync(
        IReadOnlyCollection<Guid> lineIds, Guid contactId, CancellationToken ct) =>
        await db.OrderItems.AsNoTracking().Where(line =>
            lineIds.Contains(line.Id) && line.Order.ContactId == contactId &&
            line.Order.Status != Mauw.Erp.Domain.Enums.OrderStatus.Terminated).ToListAsync(ct);
}
