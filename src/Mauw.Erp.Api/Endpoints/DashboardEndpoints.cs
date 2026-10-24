using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Enums;
using Mauw.Erp.Infrastructure.DbContext;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Mauw.Erp.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGroup("/api/v1/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization(policy => policy.RequireRole("Admin"))
            .MapGet("/", GetStatistics)
            .WithName("GetDashboardStatistics")
            .WithSummary("Get sales, contact, location, inventory, and movement statistics");
    }

    private static async Task<Ok<DashboardStatisticsDto>> GetStatistics(
        MauwDbContext db,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var firstDay = today.AddDays(-29);
        var dayAfterToday = today.AddDays(1);

        var salesRows = await db.Orders
            .AsNoTracking()
            .Where(order =>
                order.Status == OrderStatus.Confirmed &&
                order.OrderType != OrderType.Preventivo &&
                order.OrderDate >= firstDay &&
                order.OrderDate < dayAfterToday)
            .GroupBy(order => order.OrderDate.Date)
            .Select(group => new
            {
                Date = group.Key,
                OrderCount = group.Count(),
                GrossRevenue = group.Sum(order => order.GrossTotal)
            })
            .ToListAsync(cancellationToken);

        var salesByDate = salesRows.ToDictionary(row => row.Date);
        var dailySales = Enumerable.Range(0, 30)
            .Select(offset =>
            {
                var date = firstDay.AddDays(offset);
                return salesByDate.TryGetValue(date, out var row)
                    ? new DailySalesStatisticsDto(DateOnly.FromDateTime(date), row.OrderCount, row.GrossRevenue)
                    : new DailySalesStatisticsDto(DateOnly.FromDateTime(date), 0, 0.00m);
            })
            .ToArray();

        var contacts = db.Contacts
            .AsNoTracking()
            .Where(contact => !contact.IsDeleted && !contact.IsInternalCompany);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var contactStatistics = new ContactStatisticsDto(
            await contacts.CountAsync(cancellationToken),
            await contacts.CountAsync(contact => contact.CustomerProfile != null, cancellationToken),
            await contacts.CountAsync(contact => contact.SupplierProfile != null, cancellationToken),
            await contacts.CountAsync(contact => contact.AgentProfile != null, cancellationToken),
            await contacts.CountAsync(
                contact => contact.CreatedAt >= weekStart && contact.CreatedAt < dayAfterToday,
                cancellationToken),
            await contacts.CountAsync(
                contact => contact.CreatedAt >= monthStart && contact.CreatedAt < dayAfterToday,
                cancellationToken));

        var customerLocationRows = await db.ContactAddresses
            .AsNoTracking()
            .Where(address =>
                address.Contact.CustomerProfile != null &&
                !address.Contact.IsDeleted &&
                !address.Contact.IsInternalCompany &&
                address.City != "")
            .GroupBy(address => new { address.City, address.Province, address.CountryCode })
            .Select(group => new
            {
                group.Key.City,
                group.Key.Province,
                group.Key.CountryCode,
                CustomerCount = group.Select(address => address.ContactId).Distinct().Count()
            })
            .OrderByDescending(location => location.CustomerCount)
            .ThenBy(location => location.City)
            .Take(8)
            .ToListAsync(cancellationToken);
        var customerLocations = customerLocationRows
            .Select(location => new CustomerLocationStatisticsDto(
                location.City,
                location.Province,
                location.CountryCode,
                location.CustomerCount))
            .ToArray();

        var onHand = await db.StockItems
            .AsNoTracking()
            .SumAsync(item => (decimal?)item.QuantityOnHand, cancellationToken) ?? 0.00m;
        var reserved = await db.StockItems
            .AsNoTracking()
            .SumAsync(item => (decimal?)item.QuantityReserved, cancellationToken) ?? 0.00m;
        var inventoryStatistics = new InventoryStatisticsDto(
            await db.Warehouses.CountAsync(warehouse => warehouse.IsActive && !warehouse.IsDeleted, cancellationToken),
            onHand,
            reserved,
            onHand - reserved);

        var movementRows = await db.StockMovements
            .AsNoTracking()
            .Where(movement =>
                movement.MovementDateUtc >= firstDay &&
                movement.MovementDateUtc < dayAfterToday)
            .GroupBy(movement => movement.MovementDateUtc.Date)
            .Select(group => new
            {
                Date = group.Key,
                InboundCount = group.Count(movement =>
                    movement.Type == StockMovementType.PurchaseReceipt ||
                    movement.Type == StockMovementType.SupplierStockIn ||
                    movement.Type == StockMovementType.CustomerReturn),
                OutboundCount = group.Count(movement =>
                    movement.Type == StockMovementType.SalesDelivery ||
                    movement.Type == StockMovementType.SupplierStockReturn),
                AdjustmentCount = group.Count(movement => movement.Type == StockMovementType.InventoryAdjustment),
                TotalCount = group.Count()
            })
            .ToListAsync(cancellationToken);

        var movementsByDate = movementRows.ToDictionary(row => row.Date);
        var dailyMovements = Enumerable.Range(0, 30)
            .Select(offset =>
            {
                var date = firstDay.AddDays(offset);
                return movementsByDate.TryGetValue(date, out var row)
                    ? new DailyMovementStatisticsDto(
                        DateOnly.FromDateTime(date),
                        row.InboundCount,
                        row.OutboundCount,
                        row.AdjustmentCount,
                        row.TotalCount)
                    : new DailyMovementStatisticsDto(DateOnly.FromDateTime(date), 0, 0, 0, 0);
            })
            .ToArray();

        var movementStatistics = new MovementStatisticsDto(
            movementRows.Sum(row => row.TotalCount),
            movementRows.Sum(row => row.InboundCount),
            movementRows.Sum(row => row.OutboundCount),
            movementRows.Sum(row => row.AdjustmentCount),
            dailyMovements);

        return TypedResults.Ok(new DashboardStatisticsDto(
            new SalesStatisticsDto(
                dailySales.Sum(day => day.OrderCount),
                dailySales.Sum(day => day.GrossRevenue),
                dailySales),
            contactStatistics,
            customerLocations,
            inventoryStatistics,
            movementStatistics));
    }
}
