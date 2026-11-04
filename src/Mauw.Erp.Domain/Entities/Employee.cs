using System.ComponentModel.DataAnnotations;

namespace Mauw.Erp.Domain.Entities;

public sealed class Employee : Entity<Guid>
{
    private const decimal MaximumAnnualAmount = 9_999_999_999_999_999.99m;
    public string EmployeeNumber { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? Qualification { get; private set; }
    public string? ContractLevel { get; private set; }
    public string? ContractType { get; private set; }
    public decimal? AnnualGrossSalary { get; private set; }
    public decimal? AnnualCompanyCost { get; private set; }
    public DateOnly? HireDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string? StreetAddress { get; private set; }
    public string? PostalCode { get; private set; }
    public string? City { get; private set; }
    public string? Province { get; private set; }
    public string? CountryCode { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? MobilePhone { get; private set; }
    public bool IsActive { get; private set; }
    public string? Notes { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    public ICollection<EmployeeDocument> Documents { get; private set; } = [];
    public ICollection<EmployeeTimeEntry> TimeEntries { get; private set; } = [];

    public Employee() : base(Guid.NewGuid()) { }

    public EmployeeOperationResult Update(
        string? employeeNumber,
        string? firstName,
        string? lastName,
        string? qualification,
        string? contractLevel,
        string? contractType,
        decimal? annualGrossSalary,
        decimal? annualCompanyCost,
        DateOnly? hireDate,
        DateOnly? endDate,
        bool isActive,
        string? notes,
        string? streetAddress = null,
        string? postalCode = null,
        string? city = null,
        string? province = null,
        string? countryCode = null,
        string? email = null,
        string? phone = null,
        string? mobilePhone = null)
    {
        if (string.IsNullOrWhiteSpace(employeeNumber) || employeeNumber.Length > 50)
            return EmployeeOperationResult.Fail("La matricola è obbligatoria e non può superare 50 caratteri.");
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Length > 100)
            return EmployeeOperationResult.Fail("Il nome è obbligatorio e non può superare 100 caratteri.");
        if (string.IsNullOrWhiteSpace(lastName) || lastName.Length > 100)
            return EmployeeOperationResult.Fail("Il cognome è obbligatorio e non può superare 100 caratteri.");
        if (qualification?.Length > 150 || contractLevel?.Length > 100
            || contractType?.Length > 100 || notes?.Length > 1000)
            return EmployeeOperationResult.Fail("Uno dei dati anagrafici o note supera la lunghezza massima.");
        if (streetAddress?.Length > 250 || postalCode?.Length > 20 || city?.Length > 100
            || province?.Length > 100 || email?.Length > 254 || phone?.Length > 50
            || mobilePhone?.Length > 50)
            return EmployeeOperationResult.Fail("Indirizzo o recapiti superano la lunghezza massima.");
        if (!ValidAnnualAmount(annualGrossSalary) || !ValidAnnualAmount(annualCompanyCost))
            return EmployeeOperationResult.Fail("RAL e costo annuo aziendale devono essere importi non negativi a due decimali.");
        if (!annualGrossSalary.HasValue && !annualCompanyCost.HasValue)
            return EmployeeOperationResult.Fail("Indicare almeno RAL o costo annuo aziendale.");
        if (hireDate.HasValue && endDate.HasValue && endDate.Value < hireDate.Value)
            return EmployeeOperationResult.Fail("La data di cessazione non può precedere la data di assunzione.");
        if (isActive && endDate.HasValue)
            return EmployeeOperationResult.Fail("Un dipendente cessato non può essere attivo.");
        var hasAddress = !string.IsNullOrWhiteSpace(streetAddress)
            || !string.IsNullOrWhiteSpace(postalCode)
            || !string.IsNullOrWhiteSpace(city);
        if (hasAddress && (string.IsNullOrWhiteSpace(streetAddress)
            || string.IsNullOrWhiteSpace(postalCode) || string.IsNullOrWhiteSpace(city)))
            return EmployeeOperationResult.Fail("Per salvare l'indirizzo indicare via, CAP e città.");
        var normalizedCountryCode = Normalize(countryCode) ?? (hasAddress ? "IT" : null);
        if (normalizedCountryCode is not null
            && (normalizedCountryCode.Length != 2 || !normalizedCountryCode.All(char.IsAsciiLetter)))
            return EmployeeOperationResult.Fail("Il codice paese deve contenere due lettere.");
        if (!string.IsNullOrWhiteSpace(email)
            && !new EmailAddressAttribute().IsValid(email))
            return EmployeeOperationResult.Fail("L'indirizzo email non è valido.");

        EmployeeNumber = employeeNumber.Trim();
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Qualification = Normalize(qualification);
        ContractLevel = Normalize(contractLevel);
        ContractType = Normalize(contractType);
        AnnualGrossSalary = annualGrossSalary;
        AnnualCompanyCost = annualCompanyCost;
        HireDate = hireDate;
        EndDate = endDate;
        StreetAddress = Normalize(streetAddress);
        PostalCode = Normalize(postalCode);
        City = Normalize(city);
        Province = Normalize(province);
        CountryCode = normalizedCountryCode?.ToUpperInvariant();
        Email = Normalize(email);
        Phone = Normalize(phone);
        MobilePhone = Normalize(mobilePhone);
        IsActive = isActive;
        Notes = Normalize(notes);
        Version = Guid.NewGuid();
        UpdateTimestamp();
        return EmployeeOperationResult.Ok();
    }

    public EmployeeOperationResult Deactivate(DateOnly endDate)
    {
        if (endDate == default)
            return EmployeeOperationResult.Fail("Indicare la data di cessazione.");
        if (HireDate.HasValue && endDate < HireDate.Value)
            return EmployeeOperationResult.Fail("La data di cessazione non può precedere la data di assunzione.");

        IsActive = false;
        EndDate = endDate;
        Version = Guid.NewGuid();
        UpdateTimestamp();
        return EmployeeOperationResult.Ok();
    }

    private static bool ValidAnnualAmount(decimal? amount) =>
        !amount.HasValue || amount is >= 0m and <= MaximumAnnualAmount
        && decimal.Round(amount.Value, 2, MidpointRounding.AwayFromZero) == amount.Value;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record EmployeeOperationResult(bool Success, string? Error = null)
{
    public static EmployeeOperationResult Ok() => new(true);
    public static EmployeeOperationResult Fail(string error) => new(false, error);
}
