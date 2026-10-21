using System.Text.Json.Serialization;

namespace Mauw.Erp.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ImageTargetType
{
    Product,
    Variant
}
