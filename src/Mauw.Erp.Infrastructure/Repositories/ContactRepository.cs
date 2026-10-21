using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// Contact repository with advanced filtering, pagination, eager loading.
/// Normal paged queries exclude IsInternalCompany == true.
/// </summary>
public class ContactRepository(MauwDbContext dbContext) : Repository<Contact, Guid>(dbContext), IContactRepository
{
    public async Task<Contact?> GetByVatNumberAsync(string vatNumber) =>
        await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.VatNumber == vatNumber);

    public async Task<(IEnumerable<Contact> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        bool? isCustomer = null,
        bool? isSupplier = null,
        bool? isAgent = null,
        string? status = null,
        string? sortBy = null,
        bool sortDescending = false)
    {
        var query = _dbSet
            .Where(c => !c.IsInternalCompany)          // exclude "Nostra Azienda"
            .Include(c => c.CustomerProfile)
            .Include(c => c.SupplierProfile)
            .Include(c => c.AgentProfile)
            .Include(c => c.Addresses)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(c =>
                c.BusinessName.ToLower().Contains(term) ||
                (c.TradeName != null && c.TradeName.ToLower().Contains(term)) ||
                (c.VatNumber != null && c.VatNumber.ToLower().Contains(term)) ||
                (c.TaxCode   != null && c.TaxCode.ToLower().Contains(term)));
        }

        if (isCustomer == true) query = query.Where(c => c.CustomerProfile != null);
        if (isSupplier == true) query = query.Where(c => c.SupplierProfile != null);
        if (isAgent    == true) query = query.Where(c => c.AgentProfile    != null);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ContactStatus>(status, true, out var statusEnum))
            query = query.Where(c => c.Status == statusEnum);

        var total = await query.CountAsync();

        var orderedQuery = sortBy?.ToLowerInvariant() switch
        {
            "businessname" => sortDescending ? query.OrderByDescending(c => c.BusinessName) : query.OrderBy(c => c.BusinessName),
            "fiscalidentifier" => sortDescending
                ? query.OrderByDescending(c => c.VatNumber ?? c.TaxCode)
                : query.OrderBy(c => c.VatNumber ?? c.TaxCode),
            "taxcode" => sortDescending ? query.OrderByDescending(c => c.TaxCode) : query.OrderBy(c => c.TaxCode),
            "city" => sortDescending
                ? query.OrderByDescending(c => c.Addresses.Select(a => a.City).FirstOrDefault())
                : query.OrderBy(c => c.Addresses.Select(a => a.City).FirstOrDefault()),
            "roles" => sortDescending
                ? query.OrderByDescending(c =>
                    (c.CustomerProfile != null ? "1" : "0")
                    + (c.SupplierProfile != null ? "1" : "0")
                    + (c.AgentProfile != null ? "1" : "0"))
                : query.OrderBy(c =>
                    (c.CustomerProfile != null ? "1" : "0")
                    + (c.SupplierProfile != null ? "1" : "0")
                    + (c.AgentProfile != null ? "1" : "0")),
            "status" => sortDescending ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            "iscustomer" => sortDescending ? query.OrderByDescending(c => c.CustomerProfile != null) : query.OrderBy(c => c.CustomerProfile != null),
            "issupplier" => sortDescending ? query.OrderByDescending(c => c.SupplierProfile != null) : query.OrderBy(c => c.SupplierProfile != null),
            "isagent" => sortDescending ? query.OrderByDescending(c => c.AgentProfile != null) : query.OrderBy(c => c.AgentProfile != null),
            "createdat"    => sortDescending ? query.OrderByDescending(c => c.CreatedAt)    : query.OrderBy(c => c.CreatedAt),
            _              => query.OrderBy(c => c.BusinessName)
        };

        var items = await orderedQuery
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Contact?> GetByIdWithDetailsAsync(Guid id) =>
        await _dbSet
            .Include(c => c.CustomerProfile)
            .Include(c => c.SupplierProfile)
            .Include(c => c.AgentProfile)
            .Include(c => c.Addresses)
            .Include(c => c.Persons)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<bool> AddressExistsAsync(Guid addressId) =>
        await _dbSet.AnyAsync(c => c.Addresses.Any(a => a.Id == addressId));

    public async Task<Contact?> GetInternalCompanyAsync() =>
        await _dbSet
            .IgnoreQueryFilters()                       // bypass soft-delete filter
            .Include(c => c.Addresses)
            .Include(c => c.Persons)
            .FirstOrDefaultAsync(c => c.IsInternalCompany && !c.IsDeleted);
}