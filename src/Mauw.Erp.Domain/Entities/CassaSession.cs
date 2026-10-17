using Mauw.Erp.Domain.Enums;

namespace Mauw.Erp.Domain.Entities;

public class CassaSession : Entity<Guid>
{
    public string SessionCode { get; private set; } = string.Empty;
    public CassaSessionStatus Status { get; private set; } = CassaSessionStatus.Active;
    public string CartJson { get; private set; } = "[]";
    public string? ContactId { get; private set; }
    public string? ContactName { get; private set; }
    public string? DocumentNumber { get; private set; }
    public string? TerminalId { get; private set; }

    protected CassaSession() { }

    public CassaSession(
        string sessionCode,
        string cartJson,
        string? contactId,
        string? contactName,
        string? documentNumber,
        string? terminalId) : base(Guid.NewGuid())
    {
        SessionCode = sessionCode;
        CartJson = cartJson;
        ContactId = contactId;
        ContactName = contactName;
        DocumentNumber = documentNumber;
        TerminalId = terminalId;
        Status = CassaSessionStatus.Active;
    }

    public void Pause(string cartJson, string? contactId, string? contactName, string? documentNumber, string? terminalId)
    {
        CartJson = cartJson;
        ContactId = contactId;
        ContactName = contactName;
        DocumentNumber = documentNumber;
        TerminalId = terminalId;
        Status = CassaSessionStatus.Paused;
        UpdateTimestamp();
    }

    public void Resume(string? terminalId)
    {
        TerminalId = terminalId;
        Status = CassaSessionStatus.Active;
        UpdateTimestamp();
    }

    public void Terminate()
    {
        Status = CassaSessionStatus.Terminated;
        UpdateTimestamp();
    }
}
