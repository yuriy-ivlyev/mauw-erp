using System.Text.Json.Serialization;

namespace Mauw.Erp.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImageCategory
{
    Main,
    Left,
    Right,
    Zoom1,
    Zoom2,
    Zoom3,
    OnPerson,
    OnManikin
}
