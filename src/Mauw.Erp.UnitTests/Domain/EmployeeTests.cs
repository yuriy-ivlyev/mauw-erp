using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.UnitTests.Domain;

public sealed class EmployeeTests
{
    [Fact]
    public void UpdateStoresEmployeeRegistryAndAnnualAmounts()
    {
        var employee = new Employee();

        var result = employee.Update(
            "MAT-001",
            "Mario",
            "Rossi",
            "Impiegato",
            "4",
            "Commercio",
            32_000m,
            45_000m,
            new DateOnly(2024, 1, 1),
            null,
            true,
            "Nota interna",
            "Via Roma 1",
            "20100",
            "Milano",
            "MI",
            "it",
            "mario.rossi@example.it",
            "+39 02 1234567",
            "+39 333 1234567");

        Assert.True(result.Success, result.Error);
        Assert.Equal("MAT-001", employee.EmployeeNumber);
        Assert.Equal("Mario", employee.FirstName);
        Assert.Equal("Rossi", employee.LastName);
        Assert.Equal(32_000m, employee.AnnualGrossSalary);
        Assert.Equal(45_000m, employee.AnnualCompanyCost);
        Assert.Equal("Via Roma 1", employee.StreetAddress);
        Assert.Equal("20100", employee.PostalCode);
        Assert.Equal("Milano", employee.City);
        Assert.Equal("MI", employee.Province);
        Assert.Equal("IT", employee.CountryCode);
        Assert.Equal("mario.rossi@example.it", employee.Email);
        Assert.Equal("+39 02 1234567", employee.Phone);
        Assert.Equal("+39 333 1234567", employee.MobilePhone);
        Assert.True(employee.IsActive);
        Assert.NotEqual(Guid.Empty, employee.Version);
    }

    [Fact]
    public void UpdateRejectsNegativeAnnualCost()
    {
        var employee = new Employee();

        var result = employee.Update(
            "MAT-001",
            "Mario",
            "Rossi",
            null,
            null,
            null,
            null,
            -1m,
            null,
            null,
            true,
            null);

        Assert.False(result.Success);
    }

    [Fact]
    public void UpdateRejectsAnnualCostWithMoreThanTwoDecimals()
    {
        var employee = new Employee();

        var result = employee.Update(
            "MAT-001",
            "Mario",
            "Rossi",
            null,
            null,
            null,
            null,
            1.234m,
            null,
            null,
            true,
            null);

        Assert.False(result.Success);
    }

    [Fact]
    public void UpdateRejectsAnnualCostBeyondDatabasePrecision()
    {
        var employee = new Employee();

        var result = employee.Update(
            "MAT-001",
            "Mario",
            "Rossi",
            null,
            null,
            null,
            null,
            10_000_000_000_000_000m,
            null,
            null,
            true,
            null);

        Assert.False(result.Success);
    }

    [Fact]
    public void UpdateRejectsPartialAddressAndInvalidEmail()
    {
        var employee = new Employee();
        var partialAddress = employee.Update(
            "MAT-001", "Mario", "Rossi", null, null, null, null, 45_000m,
            null, null, true, null,
            streetAddress: "Via Roma 1", postalCode: null, city: "Milano");
        Assert.False(partialAddress.Success);
        Assert.Empty(employee.EmployeeNumber);

        var invalidEmail = employee.Update(
            "MAT-001", "Mario", "Rossi", null, null, null, null, 45_000m,
            null, null, true, null, email: "non-valida");
        Assert.False(invalidEmail.Success);
        Assert.Empty(employee.EmployeeNumber);
    }

    [Fact]
    public void DeactivationRequiresEndDateAfterHireDate()
    {
        var employee = new Employee();
        Assert.True(employee.Update(
            "MAT-001", "Mario", "Rossi", null, null, null, null, 45_000m,
            new DateOnly(2024, 1, 1), null, true, null).Success);

        var invalid = employee.Deactivate(new DateOnly(2023, 12, 31));
        Assert.False(invalid.Success);
        Assert.True(employee.IsActive);

        var result = employee.Deactivate(new DateOnly(2026, 11, 4));
        Assert.True(result.Success, result.Error);
        Assert.False(employee.IsActive);
        Assert.Equal(new DateOnly(2026, 11, 4), employee.EndDate);
    }
}
