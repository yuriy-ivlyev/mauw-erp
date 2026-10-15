namespace Mauw.Erp.Domain.Entities;

/// <summary>
/// Product color variant with HTML color code.
/// </summary>
public class Color : Entity
{
    public string Name { get; set; }
    public string HtmlColor { get; set; }

    public Color(string name, string htmlColor) : base()
    {
        Name = name;
        HtmlColor = htmlColor;
    }

    public void Update(string name, string htmlColor)
    {
        Name = name;
        HtmlColor = htmlColor;
        UpdateTimestamp();
    }
}
