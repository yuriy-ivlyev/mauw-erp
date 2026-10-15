namespace Mauw.Erp.Contracts.Requests;

/// <summary>
/// Request for creating a new color.
/// </summary>
public record CreateColorRequest(string Name, string HtmlColor);

/// <summary>
/// Request for updating a color.
/// </summary>
public record UpdateColorRequest(string Name, string HtmlColor, bool IsVisible);
