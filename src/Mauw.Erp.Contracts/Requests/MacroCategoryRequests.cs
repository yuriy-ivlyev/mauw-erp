namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating a new macro category.
/// </summary>
public record CreateMacroCategoryRequest(string Name);

/// <summary>
/// Request for updating a macro category.
/// </summary>
public record UpdateMacroCategoryRequest(string Name, bool IsVisible);
