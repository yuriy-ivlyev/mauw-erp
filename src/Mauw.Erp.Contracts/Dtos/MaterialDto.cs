namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for Material Composition information (response).
/// </summary>
public record MaterialCompositionDto(Guid Id, string ComponentName, decimal PercentageValue);

/// <summary>
/// DTO for Material information (response).
/// </summary>
public record MaterialDto(Guid Id, string Name, bool IsVisible, List<MaterialCompositionDto> Composition);
