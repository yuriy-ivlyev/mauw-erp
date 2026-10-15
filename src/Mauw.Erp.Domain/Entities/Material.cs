namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Material/fabric type.
/// </summary>
public class Material : Entity
{
    public string Name { get; set; }

    // Navigation
    public ICollection<MaterialComposition> Composition { get; set; } = [];

    public Material(string name) : base()
    {
        Name = name;
    }

    public void Update(string name)
    {
        Name = name;
        UpdateTimestamp();
    }
}
