using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// Generic repository interface for data access.
/// </summary>
public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Func<T, bool> predicate);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task SaveChangesAsync();
}

/// <summary>
/// Product-specific repository operations.
/// </summary>
public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetByInternalCodeAsync(string internalCode);
    Task<IEnumerable<Product>> GetPaginatedAsync(int page, int pageSize, string? search = null);
    Task<int> GetTotalCountAsync(string? search = null);
}

/// <summary>
/// ProductVariant-specific repository operations.
/// </summary>
public interface IProductVariantRepository : IRepository<ProductVariant>
{
    Task<ProductVariant?> GetByBarcodeAsync(string barcode);
}

/// <summary>
/// Contact-specific repository operations.
/// </summary>
public interface IContactRepository : IRepository<Contact>
{
    Task<Contact?> GetByVatNumberAsync(string vatNumber);
    Task<IEnumerable<Contact>> GetPaginatedAsync(int page, int pageSize, string? search = null);
    Task<int> GetTotalCountAsync(string? search = null);
}

/// <summary>
/// Order-specific repository operations.
/// </summary>
public interface IOrderRepository : IRepository<Order>
{
    Task<IEnumerable<Order>> GetByContactIdAsync(Guid contactId);
}

/// <summary>
/// Invoice-specific repository operations.
/// </summary>
public interface IInvoiceRepository : IRepository<Invoice>
{
    Task<Invoice?> GetByNumberAsync(string invoiceNumber);
    Task<IEnumerable<Invoice>> GetByContactIdAsync(Guid contactId);
}

/// <summary>
/// StockMovement-specific repository operations.
/// </summary>
public interface IStockMovementRepository : IRepository<StockMovement>
{
    Task<IEnumerable<StockMovement>> GetByVariantIdAsync(Guid variantId);
}

/// <summary>
/// DdtDocument-specific repository operations.
/// </summary>
public interface IDdtDocumentRepository : IRepository<DdtDocument>
{
    Task<IEnumerable<DdtDocument>> GetByContactIdAsync(Guid contactId);
    Task<DdtDocument?> GetByNumberAsync(string ddtNumber);
}

/// <summary>
/// Unit of Work for transaction management.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IProductRepository Products { get; }
    IProductVariantRepository ProductVariants { get; }
    IContactRepository Contacts { get; }
    IOrderRepository Orders { get; }
    IInvoiceRepository Invoices { get; }
    IStockMovementRepository StockMovements { get; }
    IDdtDocumentRepository DdtDocuments { get; }
    
    Task SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
}
