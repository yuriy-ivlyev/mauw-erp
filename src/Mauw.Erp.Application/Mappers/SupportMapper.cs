using Mauw.Erp.Contracts.Dtos;
using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.Application.Mappers;

/// <summary>
/// Manual mapper for support entities (Color, Size, Category, Collection, Material, MacroCategory) to DTOs.
/// </summary>
public static class SupportMapper
{
    public static ColorDto MapToDto(Color color) => new(color.Id, color.Name, color.HtmlColor, color.IsVisible);

    public static SizeDto MapToDto(Size size) => new(size.Id, size.Name, size.IsVisible);

    public static MaterialCompositionDto MapToDto(MaterialComposition mc) => 
        new(mc.Id, mc.ComponentName, mc.PercentageValue);

    public static MaterialDto MapToDto(Material material) => new(
        material.Id,
        material.Name,
        material.IsVisible,
        material.Composition.Select(MapToDto).ToList()
    );

    public static CategoryDto MapToDto(Category cat) => new(
        cat.Id,
        cat.Name,
        cat.MacroCategoryId,
        cat.MacroCategory?.Name,
        cat.IsVisible
    );

    public static CollectionDto MapToDto(Collection col) => 
        new(col.Id, col.Name, col.Description);

    public static MacroCategoryDto MapToDto(MacroCategory mc) => 
        new(mc.Id, mc.Name, mc.IsVisible);
}
