namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Macro category for grouping product categories.
/// </summary>
public class MacroCategory : Entity
{
    public string Name { get; set; }

    public MacroCategory(string name) : base()
    {
        Name = name;
    }

    public void Update(string name)
    {
        Name = name;
        UpdateTimestamp();
    }
}
