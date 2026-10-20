using Mauw.Erp.Domain.Entities;
using System.Linq.Expressions;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// Generic repository interface for data access with generic ID type.
/// </summary>
public interface IRepository<TEntity, TId> where TEntity : Entity<TId> where TId : IEquatable<TId>
{
    Task<TEntity?> GetByIdAsync(TId id);
    Task<IEnumerable<TEntity>> GetAllAsync();
    Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate);
    Task AddAsync(TEntity entity);
    Task UpdateAsync(TEntity entity);
    Task DeleteAsync(TEntity entity);
    Task SaveChangesAsync();
}