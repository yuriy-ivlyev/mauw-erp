namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Agent-role profile attached to a Contact via shared primary key (Party-Role pattern).
/// </summary>
public class AgentProfile
{
    public Guid ContactId { get; set; }
    public decimal DefaultCommissionRate { get; set; }
    public string? OperatingArea { get; set; }

    // Navigation
    public Contact Contact { get; set; } = null!;
}
