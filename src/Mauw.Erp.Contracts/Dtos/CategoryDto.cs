namespace Mauw.Erp.Contracts.Dtos;

/// <summary>
/// DTO for Category information (response).
/// </summary>
public record CategoryDto(Guid Id, string Name, Guid? MacroCategoryId, string? MacroCategoryName, bool IsVisible);
