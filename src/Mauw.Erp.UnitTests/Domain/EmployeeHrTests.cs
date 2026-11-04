using Mauw.Erp.Domain.Entities;
using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.UnitTests.Domain;

public sealed class EmployeeHrTests
{
    [Fact]
    public void IdentityDocumentRequiresItsNumberAndValidDates()
    {
        var document = new EmployeeDocument(Guid.NewGuid(), "admin");
        var missingNumber = document.SetDetails(
            EmployeeDocumentKind.IdentityDocument,
            "Carta d'identità",
            null,
            "Comune",
            new DateOnly(2024, 1, 1),
            null,
            new DateOnly(2034, 1, 1),
            null);
        Assert.False(missingNumber.Success);

        var invalidDates = document.SetDetails(
            EmployeeDocumentKind.IdentityDocument,
            "Carta d'identità",
            "AA123456",
            "Comune",
            new DateOnly(2024, 1, 1),
            null,
            new DateOnly(2023, 12, 31),
            null);
        Assert.False(invalidDates.Success);
    }

    [Fact]
    public void SafetyTrainingRequiresCompletionDateAndStoresExpiry()
    {
        var document = new EmployeeDocument(Guid.NewGuid(), "admin");
        var missingCompletion = document.SetDetails(
            EmployeeDocumentKind.SafetyTraining,
            "Formazione sicurezza",
            null,
            "Ente formatore",
            null,
            null,
            new DateOnly(2028, 1, 1),
            null);
        Assert.False(missingCompletion.Success);

        var saved = document.SetDetails(
            EmployeeDocumentKind.SafetyTraining,
            "Formazione sicurezza",
            null,
            "Ente formatore",
            null,
            new DateOnly(2026, 11, 4),
            new DateOnly(2028, 1, 1),
            null);
        Assert.True(saved.Success, saved.Error);
        Assert.Equal(EmployeeDocumentKind.SafetyTraining, document.Kind);
        Assert.Equal(new DateOnly(2026, 11, 4), document.CompletedOn);
    }

    [Fact]
    public void WorkEntryCalculatesNetMinutesAndAbsenceHasNoHours()
    {
        var work = new EmployeeTimeEntry(Guid.NewGuid(), "admin");
        var result = work.Update(
            new DateOnly(2026, 11, 4),
            EmployeeTimeEntryType.Work,
            new TimeOnly(8, 30),
            new TimeOnly(17, 0),
            30,
            null);
        Assert.True(result.Success, result.Error);
        Assert.Equal(480, work.WorkedMinutes);

        var absence = new EmployeeTimeEntry(Guid.NewGuid(), "admin");
        var absenceResult = absence.Update(
            new DateOnly(2026, 11, 5),
            EmployeeTimeEntryType.Vacation,
            null,
            null,
            0,
            "Ferie");
        Assert.True(absenceResult.Success, absenceResult.Error);
        Assert.Equal(0, absence.WorkedMinutes);
    }

    [Fact]
    public void WorkEntryRejectsInvalidBreaksAndAbsenceTimes()
    {
        var work = new EmployeeTimeEntry(Guid.NewGuid(), "admin");
        Assert.False(work.Update(
            new DateOnly(2026, 11, 4),
            EmployeeTimeEntryType.Work,
            new TimeOnly(8, 30),
            new TimeOnly(17, 0),
            600,
            null).Success);

        var absence = new EmployeeTimeEntry(Guid.NewGuid(), "admin");
        Assert.False(absence.Update(
            new DateOnly(2026, 11, 5),
            EmployeeTimeEntryType.SickLeave,
            new TimeOnly(8, 30),
            new TimeOnly(17, 0),
            0,
            null).Success);
    }
}
