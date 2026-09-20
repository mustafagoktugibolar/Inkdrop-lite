using Inkdrop_lite.Domain.Common;

namespace InkdropLite.Api.Models;

public class Tag : UpdatableUserOwnedEntity
{
    public required string Name { get; set; }

    public TagColor Color { get; set; } = TagColor.Default;

    public List<Note> Notes { get; set; } = [];
}
