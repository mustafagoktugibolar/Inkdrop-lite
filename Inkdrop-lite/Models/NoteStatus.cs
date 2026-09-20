using System.Text.Json.Serialization;

namespace InkdropLite.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NoteStatus
{
    None,
    Active,
    OnHold,
    Completed,
    Dropped
}
