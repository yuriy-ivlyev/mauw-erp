namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating a new size.
/// </summary>
public record CreateSizeRequest(string Name);

/// <summary>
/// Request for updating a size.
/// </summary>
public record UpdateSizeRequest(string Name, bool IsVisible);
