using Microsoft.EntityFrameworkCore;
using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Repositories;
using Mauw.Erp.Infrastructure.DbContext;
using System.Linq.Expressions;

namespace Mauw.Erp.Infrastructure.Repositories;

/// <summary>
/// Generic repository implementation with generic ID type.
/// </summary>
public class Repository<TEntity, TId>(MauwDbContext dbContext) : IRepository<TEntity, TId>
    where TEntity : Entity<TId>
    where TId : IEquatable<TId>
{
    protected readonly MauwDbContext _dbContext = dbContext;
    protected readonly DbSet<TEntity> _dbSet = dbContext.Set<TEntity>();

    public virtual async Task<TEntity?> GetByIdAsync(TId id)
    {
        return await _dbSet.FirstOrDefaultAsync(e => e.Id.Equals(id));
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync()
    {
        return await _dbSet.AsNoTracking().ToListAsync();
    }

    public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(predicate)
            .ToListAsync();
    }

    public virtual async Task AddAsync(TEntity entity)
    {
        await _dbSet.AddAsync(entity);
    }

    public virtual async Task UpdateAsync(TEntity entity)
    {
        _dbSet.Update(entity);
        await Task.CompletedTask;
    }

    public virtual async Task DeleteAsync(TEntity entity)
    {
        _dbSet.Remove(entity);
        await Task.CompletedTask;
    }

    public virtual async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}