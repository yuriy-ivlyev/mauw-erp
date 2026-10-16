using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// Contact-specific repository operations supporting Party-Role pattern.
/// </summary>
public interface IContactRepository : IRepository<Contact, Guid>
{
    Task<Contact?> GetByVatNumberAsync(string vatNumber);

    Task<(IEnumerable<Contact> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        string? search = null,
        bool? isCustomer = null,
        bool? isSupplier = null,
        bool? isAgent = null,
        string? status = null,
        string? sortBy = null,
        bool sortDescending = false);

    Task<Contact?> GetByIdWithDetailsAsync(Guid id);

    Task<bool> AddressExistsAsync(Guid addressId);

    /// <summary>Restituisce il contatto "Nostra Azienda" (IsInternalCompany == true), ignorando i filtri globali.</summary>
    Task<Contact?> GetInternalCompanyAsync();
}