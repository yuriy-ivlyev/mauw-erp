namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Macro category for grouping product categories.
/// </summary>
public class MacroCategory : Entity<Guid>
{
    public string Name { get; set; }
    public bool IsVisible { get; set; } = true;

    public MacroCategory(string name) : base()
    {
        Name = name;
        IsVisible = true;
    }

    public void Update(string name)
    {
        Name = name;
        UpdateTimestamp();
    }
}
