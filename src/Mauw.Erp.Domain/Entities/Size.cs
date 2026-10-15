namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Product size variant.
/// </summary>
public class Size : Entity
{
    public string Name { get; set; }

    public Size(string name) : base()
    {
        Name = name;
    }

    public void Update(string name)
    {
        Name = name;
        UpdateTimestamp();
    }
}
