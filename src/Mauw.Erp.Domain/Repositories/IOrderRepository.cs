using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// Order-specific repository operations.
/// </summary>
public interface IOrderRepository : IRepository<Order, Guid>
{
    Task<IEnumerable<Order>> GetByContactIdAsync(Guid contactId);
}