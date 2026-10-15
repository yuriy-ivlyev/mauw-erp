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
    ISizeRepository Sizes { get; }
    IColorRepository Colors { get; }
    ICategoryRepository Categories { get; }
    ICollectionRepository Collections { get; }
    IMaterialRepository Materials { get; }
    IMacroCategoryRepository MacroCategories { get; }
    
    Task SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
}