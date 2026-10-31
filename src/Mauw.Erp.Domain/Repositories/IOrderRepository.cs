using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Repositories;

/// <summary>
/// Order-specific repository operations.
/// </summary>
public interface IOrderRepository : IRepository<Order, Guid>
{
    Task<IEnumerable<Order>> GetByContactIdAsync(Guid contactId);
    Task<bool> IsUnderAdjustmentAsync(Guid orderId);
    Task<(IEnumerable<Order> Items, int Total)> GetByContactIdPagedAsync(Guid contactId, int page, int pageSize);
    Task<int> CountCassaSessionsAsync();
    Task<IEnumerable<Order>> GetSuspendedCassaSessionsAsync();
    Task<Order?> GetCassaSessionWithItemsAsync(Guid id);
    Task AddWithNextNumberAsync(Order order, string prefix);
    Task<(IEnumerable<Order> Items, int Total)> GetAllPagedAsync(
        string? search, string? status, string? orderType, int page, int pageSize, string? sortBy = null, bool sortDescending = false);
    Task<(IEnumerable<Order> Items, int Total)> GetFulfillmentQueueAsync(int page, int pageSize);
    Task<int> CountOrdersByPrefixAsync(string prefix);
}
