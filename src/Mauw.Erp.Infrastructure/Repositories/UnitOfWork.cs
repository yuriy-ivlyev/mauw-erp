using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Domain.Exceptions;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// Unit of Work implementation for transaction management.
/// </summary>
public class UnitOfWork(MauwDbContext dbContext) : IUnitOfWork
{
    private IProductRepository? _productRepository;
    private IProductVariantRepository? _productVariantRepository;
    private IContactRepository? _contactRepository;
    private IOrderRepository? _orderRepository;
    private IInvoiceRepository? _invoiceRepository;
    private IStockMovementRepository? _stockMovementRepository;
    private IDdtDocumentRepository? _ddtDocumentRepository;

    public IProductRepository Products => _productRepository ??= new ProductRepository(dbContext);
    public IProductVariantRepository ProductVariants => _productVariantRepository ??= new ProductVariantRepository(dbContext);
    public IContactRepository Contacts => _contactRepository ??= new ContactRepository(dbContext);
    public IOrderRepository Orders => _orderRepository ??= new OrderRepository(dbContext);
    public IInvoiceRepository Invoices => _invoiceRepository ??= new InvoiceRepository(dbContext);
    public IStockMovementRepository StockMovements => _stockMovementRepository ??= new StockMovementRepository(dbContext);
    public IDdtDocumentRepository DdtDocuments => _ddtDocumentRepository ??= new DdtDocumentRepository(dbContext);

    public async Task SaveChangesAsync()
    {
        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            dbContext.ChangeTracker.Clear();
            throw new ConcurrencyException(ex.Message);
        }
    }

    public async Task BeginTransactionAsync()
    {
        await dbContext.Database.BeginTransactionAsync();
    }

    public async Task CommitAsync()
    {
        await dbContext.Database.CommitTransactionAsync();
    }

    public async Task RollbackAsync()
    {
        await dbContext.Database.RollbackTransactionAsync();
    }

    public void Dispose()
    {
        dbContext?.Dispose();
    }
}