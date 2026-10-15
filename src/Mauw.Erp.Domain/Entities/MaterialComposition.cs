namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Material composition breakdown (e.g., Cotton 20%, Viscose 79%).
/// </summary>
public class MaterialComposition : Entity<Guid>
{
    public Guid MaterialId { get; set; }
    public string ComponentName { get; set; }
    public decimal PercentageValue { get; set; }

    // Navigation
    public Material Material { get; set; } = null!;

    public MaterialComposition(Guid materialId, string componentName, decimal percentageValue) : base()
    {
        MaterialId = materialId;
        ComponentName = componentName;
        PercentageValue = percentageValue;
    }

    public void Update(string componentName, decimal percentageValue)
    {
        ComponentName = componentName;
        PercentageValue = percentageValue;
        UpdateTimestamp();
    }
}
