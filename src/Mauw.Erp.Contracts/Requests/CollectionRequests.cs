namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating a new collection.
/// </summary>
public record CreateCollectionRequest(string Name, string? Description);

/// <summary>
/// Request for updating a collection.
/// </summary>
public record UpdateCollectionRequest(string Name, string? Description);
