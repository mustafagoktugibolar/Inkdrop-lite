using System.Text.Json.Serialization;

namespace InkdropLite.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotebookIconType
{
    None,
    Svg,
    Attachment
}
