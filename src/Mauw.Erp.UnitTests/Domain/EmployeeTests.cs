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
            "Sede showroom");

        Assert.True(result.Success, result.Error);
        Assert.Equal("MAT-001", employee.EmployeeNumber);
        Assert.Equal("Mario", employee.FirstName);
        Assert.Equal("Rossi", employee.LastName);
        Assert.Equal(32_000m, employee.AnnualGrossSalary);
        Assert.Equal(45_000m, employee.AnnualCompanyCost);
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
