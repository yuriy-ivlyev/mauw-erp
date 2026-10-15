namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Product size variant (ID: 5 char string, e.g., "XXS__", "M____").
/// </summary>
public class Size : Entity<string>
{
    public string Name { get; set; }
    public bool IsVisible { get; set; } = true;

    public Size(string id, string name) : base(id)
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

