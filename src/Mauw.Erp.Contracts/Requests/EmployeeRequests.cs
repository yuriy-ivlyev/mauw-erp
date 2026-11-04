namespace Mauw.Erp.Contracts.Requests;

public sealed record SaveEmployeeRequest(
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
    Guid? Version = null);

public sealed record DeactivateEmployeeRequest(Guid Version, DateOnly EndDate);
