namespace Mauw.Erp.Contracts.Dtos;

public sealed record DashboardStatisticsDto(
    SalesStatisticsDto Sales,
    ContactStatisticsDto Contacts,
    IReadOnlyList<CustomerLocationStatisticsDto> CustomerLocations,
    InventoryStatisticsDto Inventory,
    MovementStatisticsDto Movements);

public sealed record SalesStatisticsDto(
    int OrderCount,
    decimal GrossRevenue,
    IReadOnlyList<DailySalesStatisticsDto> Daily);

public sealed record DailySalesStatisticsDto(
    DateOnly Date,
    int OrderCount,
    decimal GrossRevenue);

public sealed record ContactStatisticsDto(
    int TotalContacts,
    int TotalCustomers,
    int TotalSuppliers,
    int TotalAgents,
    int NewThisWeek,
    int NewThisMonth);

public sealed record CustomerLocationStatisticsDto(
    string City,
    string? Province,
    string CountryCode,
    int CustomerCount);

public sealed record InventoryStatisticsDto(
    int ActiveWarehouses,
    decimal QuantityOnHand,
    decimal QuantityReserved,
    decimal QuantityAvailable);

public sealed record MovementStatisticsDto(
    int TotalCount,
    int InboundCount,
    int OutboundCount,
    int AdjustmentCount,
    IReadOnlyList<DailyMovementStatisticsDto> Daily);

public sealed record DailyMovementStatisticsDto(
    DateOnly Date,
    int InboundCount,
    int OutboundCount,
    int AdjustmentCount,
    int TotalCount);
