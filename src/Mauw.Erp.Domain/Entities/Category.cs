namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Product category with optional macro category.
/// </summary>
public class Category : Entity<Guid>
{
    public string Name { get; set; }
    public Guid? MacroCategoryId { get; set; }
    public bool IsVisible { get; set; } = true;

    // Navigation
    public MacroCategory? MacroCategory { get; set; }

    public Category(string name, Guid? macroCategoryId = null) : base()
    {
        Name = name;
        MacroCategoryId = macroCategoryId;
        IsVisible = true;
    }

    public void Update(string name, Guid? macroCategoryId = null)
    {
        Name = name;
        MacroCategoryId = macroCategoryId;
        UpdateTimestamp();
    }
}
