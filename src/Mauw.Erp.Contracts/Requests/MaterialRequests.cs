namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Material composition data for creation/update.
/// </summary>
public record MaterialCompositionData(string ComponentName, decimal PercentageValue);

/// <summary>
/// Request for creating a new material.
/// </summary>
public record CreateMaterialRequest(string Name, List<MaterialCompositionData>? Composition);

/// <summary>
/// Request for updating a material.
/// </summary>
public record UpdateMaterialRequest(string Name, List<MaterialCompositionData>? Composition, bool IsVisible);
