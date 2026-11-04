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
    Guid? Version = null,
    string? StreetAddress = null,
    string? PostalCode = null,
    string? City = null,
    string? Province = null,
    string? CountryCode = null,
    string? Email = null,
    string? Phone = null,
    string? MobilePhone = null);

public sealed record DeactivateEmployeeRequest(Guid Version, DateOnly EndDate);
