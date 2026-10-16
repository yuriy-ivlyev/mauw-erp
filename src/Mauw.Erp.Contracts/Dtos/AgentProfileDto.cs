namespace Mauw.Erp.Contracts.Dtos;

public record AgentProfileDto(
    decimal DefaultCommissionRate,
    string? OperatingArea
);
