using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Entities;

public sealed class EmployeeTimeEntry : Entity<Guid>
{
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;
    public DateOnly WorkDate { get; private set; }
    public EmployeeTimeEntryType Type { get; private set; }
    public TimeOnly? StartTime { get; private set; }
    public TimeOnly? EndTime { get; private set; }
    public int BreakMinutes { get; private set; }
    public string? Notes { get; private set; }
    public string? CreatedByUserId { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    public int WorkedMinutes => Type == EmployeeTimeEntryType.Work
        && StartTime.HasValue && EndTime.HasValue
            ? (int)((EndTime.Value.ToTimeSpan() - StartTime.Value.ToTimeSpan()).Ticks
                / TimeSpan.TicksPerMinute) - BreakMinutes
            : 0;

    public EmployeeTimeEntry() : base(Guid.NewGuid()) { }

    public EmployeeTimeEntry(Guid employeeId, string? createdByUserId) : base(Guid.NewGuid())
    {
        EmployeeId = employeeId;
        CreatedByUserId = Normalize(createdByUserId);
    }

    public EmployeeOperationResult Update(
        DateOnly workDate,
        EmployeeTimeEntryType type,
        TimeOnly? startTime,
        TimeOnly? endTime,
        int breakMinutes,
        string? notes)
    {
        if (EmployeeId == Guid.Empty)
            return EmployeeOperationResult.Fail("Il dipendente è obbligatorio.");
        if (workDate == default)
            return EmployeeOperationResult.Fail("La data del cartellino è obbligatoria.");
        if (!Enum.IsDefined(type))
            return EmployeeOperationResult.Fail("Tipo di cartellino non valido.");
        if (notes?.Length > 1000)
            return EmployeeOperationResult.Fail("Le note non possono superare 1000 caratteri.");

        if (type == EmployeeTimeEntryType.Work)
        {
            if (!startTime.HasValue || !endTime.HasValue
                || endTime.Value <= startTime.Value)
                return EmployeeOperationResult.Fail(
                    "Per una giornata lavorata indicare un'uscita successiva all'entrata.");
            var durationMinutes = (int)((endTime.Value.ToTimeSpan() - startTime.Value.ToTimeSpan()).Ticks
                / TimeSpan.TicksPerMinute);
            if (breakMinutes < 0 || breakMinutes >= durationMinutes || breakMinutes > 360)
                return EmployeeOperationResult.Fail("La pausa deve essere compresa tra zero e la durata del turno.");
        }
        else if (startTime.HasValue || endTime.HasValue || breakMinutes != 0)
        {
            return EmployeeOperationResult.Fail("Le assenze non devono riportare orari o pause.");
        }

        WorkDate = workDate;
        Type = type;
        StartTime = startTime;
        EndTime = endTime;
        BreakMinutes = breakMinutes;
        Notes = Normalize(notes);
        Version = Guid.NewGuid();
        UpdateTimestamp();
        return EmployeeOperationResult.Ok();
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
