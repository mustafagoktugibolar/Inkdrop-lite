using System.Text.Json.Serialization;

namespace InkdropLite.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TagColor
{
    Default,
    Red,
    Orange,
    Yellow,
    Olive,
    Green,
    Teal,
    Blue,
    Violet,
    Purple,
    Pink,
    Brown,
    Grey,
    Black
}
