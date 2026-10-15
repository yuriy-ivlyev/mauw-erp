namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Product color variant (ID: 3 char string, e.g., "BLK", "WHT", "MRN").
/// </summary>
public class Color : Entity<string>
{
    public string Name { get; set; }
    public string HtmlColor { get; set; }
    public bool IsVisible { get; set; } = true;

    public Color(string id, string name, string htmlColor) : base(id)
    {
        Name = name;
        HtmlColor = htmlColor;
        IsVisible = true;
    }

    public void Update(string name, string htmlColor)
    {
        Name = name;
        HtmlColor = htmlColor;
        UpdateTimestamp();
    }
}
