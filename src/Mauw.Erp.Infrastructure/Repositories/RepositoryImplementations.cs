using Microsoft.EntityFrameworkCore;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// Generic repository implementation without reflection.
/// </summary>
public class Repository<T> : IRepository<T> where T : Entity
{
    protected readonly MauwDbContext _dbContext;
    protected readonly DbSet<T> _dbSet;

    public Repository(MauwDbContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = dbContext.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FirstOrDefaultAsync(e => e.Id == id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.AsNoTracking().ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Func<T, bool> predicate)
    {
        var all = await _dbSet.AsNoTracking().ToListAsync();
        return all.Where(predicate);
    }

    public virtual async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public virtual async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await Task.CompletedTask;
    }

    public virtual async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}

/// <summary>
/// Product repository implementation.
/// </summary>
public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(MauwDbContext dbContext) : base(dbContext) { }

    public async Task<Product?> GetByInternalCodeAsync(string internalCode)
    {
        return await _dbSet
            .Include(p => p.Variants)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.InternalCode == internalCode);
    }

    public async Task<IEnumerable<Product>> GetPaginatedAsync(int page, int pageSize, string? search = null)
    {
        var query = _dbSet.Include(p => p.Variants).AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(searchLower) || p.InternalCode.ToLower().Contains(searchLower));
        }

        return await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync(string? search = null)
    {
        var query = _dbSet.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(searchLower) || p.InternalCode.ToLower().Contains(searchLower));
        }

        return await query.CountAsync();
    }
}

/// <summary>
/// ProductVariant repository implementation.
/// </summary>
public class ProductVariantRepository : Repository<ProductVariant>, IProductVariantRepository
{
    public ProductVariantRepository(MauwDbContext dbContext) : base(dbContext) { }

    public async Task<ProductVariant?> GetByBarcodeAsync(string barcode)
    {
        return await _dbSet
            .Include(v => v.Product)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Barcode.ToLower() == barcode.ToLower());
    }
}

/// <summary>
/// Contact repository implementation.
/// </summary>
public class ContactRepository : Repository<Contact>, IContactRepository
{
    public ContactRepository(MauwDbContext dbContext) : base(dbContext) { }

    public async Task<Contact?> GetByVatNumberAsync(string vatNumber)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.VatNumber == vatNumber);
    }

    public async Task<IEnumerable<Contact>> GetPaginatedAsync(int page, int pageSize, string? search = null)
    {
        var query = _dbSet.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(c => c.BusinessName.ToLower().Contains(searchLower) || c.VatNumber.ToLower().Contains(searchLower));
        }

        return await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<int> GetTotalCountAsync(string? search = null)
    {
        var query = _dbSet.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(c => c.BusinessName.ToLower().Contains(searchLower) || c.VatNumber.ToLower().Contains(searchLower));
        }

        return await query.CountAsync();
    }
}

/// <summary>
/// Order repository implementation.
/// </summary>
public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(MauwDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<Order>> GetByContactIdAsync(Guid contactId)
    {
        return await _dbSet
            .Include(o => o.Items)
            .AsNoTracking()
            .Where(o => o.ContactId == contactId)
            .ToListAsync();
    }
}

/// <summary>
/// Invoice repository implementation.
/// </summary>
public class InvoiceRepository : Repository<Invoice>, IInvoiceRepository
{
    public InvoiceRepository(MauwDbContext dbContext) : base(dbContext) { }

    public async Task<Invoice?> GetByNumberAsync(string invoiceNumber)
    {
        return await _dbSet
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.InvoiceNumber == invoiceNumber);
    }

    public async Task<IEnumerable<Invoice>> GetByContactIdAsync(Guid contactId)
    {
        return await _dbSet
            .Include(i => i.Items)
            .Include(i => i.Payments)
            .AsNoTracking()
            .Where(i => i.ContactId == contactId)
            .ToListAsync();
    }
}

/// <summary>
/// StockMovement repository implementation.
/// </summary>
public class StockMovementRepository : Repository<StockMovement>, IStockMovementRepository
{
    public StockMovementRepository(MauwDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<StockMovement>> GetByVariantIdAsync(Guid variantId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(sm => sm.VariantId == variantId)
            .OrderByDescending(sm => sm.CreatedAt)
            .ToListAsync();
    }
}

/// <summary>
/// DdtDocument repository implementation.
/// </summary>
public class DdtDocumentRepository : Repository<DdtDocument>, IDdtDocumentRepository
{
    public DdtDocumentRepository(MauwDbContext dbContext) : base(dbContext) { }

    public async Task<IEnumerable<DdtDocument>> GetByContactIdAsync(Guid contactId)
    {
        return await _dbSet
            .Include(d => d.Items)
            .AsNoTracking()
            .Where(d => d.ContactId == contactId)
            .OrderByDescending(d => d.DdtDate)
            .ToListAsync();
    }

    public async Task<DdtDocument?> GetByNumberAsync(string ddtNumber)
    {
        return await _dbSet
            .Include(d => d.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DdtNumber == ddtNumber);
    }

    public override async Task<DdtDocument?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(d => d.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);
    }
}

/// <summary>
/// Unit of Work implementation for transaction management.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly MauwDbContext _dbContext;
    private IProductRepository? _productRepository;
    private IProductVariantRepository? _productVariantRepository;
    private IContactRepository? _contactRepository;
    private IOrderRepository? _orderRepository;
    private IInvoiceRepository? _invoiceRepository;
    private IStockMovementRepository? _stockMovementRepository;
    private IDdtDocumentRepository? _ddtDocumentRepository;

    public UnitOfWork(MauwDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IProductRepository Products => _productRepository ??= new ProductRepository(_dbContext);
    public IProductVariantRepository ProductVariants => _productVariantRepository ??= new ProductVariantRepository(_dbContext);
    public IContactRepository Contacts => _contactRepository ??= new ContactRepository(_dbContext);
    public IOrderRepository Orders => _orderRepository ??= new OrderRepository(_dbContext);
    public IInvoiceRepository Invoices => _invoiceRepository ??= new InvoiceRepository(_dbContext);
    public IStockMovementRepository StockMovements => _stockMovementRepository ??= new StockMovementRepository(_dbContext);
    public IDdtDocumentRepository DdtDocuments => _ddtDocumentRepository ??= new DdtDocumentRepository(_dbContext);

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        await _dbContext.Database.BeginTransactionAsync();
    }

    public async Task CommitAsync()
    {
        await _dbContext.Database.CommitTransactionAsync();
    }

    public async Task RollbackAsync()
    {
        await _dbContext.Database.RollbackTransactionAsync();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }
}
