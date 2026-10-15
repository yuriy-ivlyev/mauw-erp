namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating a new category.
/// </summary>
public record CreateCategoryRequest(string Name, Guid? MacroCategoryId);

/// <summary>
/// Request for updating a category.
/// </summary>
public record UpdateCategoryRequest(string Name, Guid? MacroCategoryId);
