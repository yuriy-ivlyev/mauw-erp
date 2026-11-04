namespace Mauw.Erp.Contracts.Dtos;

public sealed record EmployeeDto(
    Guid Id,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string? Qualification,
    string? ContractLevel,
    string? ContractType,
    decimal? AnnualGrossSalary,
    decimal? AnnualCompanyCost,
    DateOnly? HireDate,
    DateOnly? EndDate,
    bool IsActive,
    string? Notes,
    Guid Version);
