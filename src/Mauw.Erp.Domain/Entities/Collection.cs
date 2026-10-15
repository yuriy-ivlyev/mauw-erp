namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Product collection/line.
/// </summary>
public class Collection : Entity
{
    public string Name { get; set; }
    public string? Description { get; set; }

    public Collection(string name, string? description = null) : base()
    {
        Name = name;
        Description = description;
    }

    public void Update(string name, string? description = null)
    {
        Name = name;
        Description = description;
        UpdateTimestamp();
    }
}
