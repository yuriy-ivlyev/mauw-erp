namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Material/fabric type.
/// </summary>
public class Material : Entity<Guid>
{
    public string Name { get; set; }
    public bool IsVisible { get; set; } = true;

    // Navigation
    public ICollection<MaterialComposition> Composition { get; set; } = [];

    public Material(string name) : base()
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
