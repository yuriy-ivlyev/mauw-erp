namespace Mauw.Erp.Domain.Repositories;

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