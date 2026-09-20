using System.ComponentModel.DataAnnotations;
using InkdropLite.Api.Models;

namespace Inkdrop_lite.Features.Tags.Contracts;

public sealed record UpdateTagRequest
{
    public UpdateTagRequest(string name, TagColor color = TagColor.Default)
    {
        Name = name;
        Color = color;
    }

    [Required, MaxLength(64)]
    public string Name { get; init; }

    [EnumDataType(typeof(TagColor))]
    public TagColor Color { get; init; }
}
