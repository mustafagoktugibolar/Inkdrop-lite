using Inkdrop_lite.Domain.Common;

namespace InkdropLite.Api.Models;

public class Tag : UserOwnedEntity
{
    public required string Name { get; set; }

    public List<NoteTag> NoteTags { get; set; } = [];
}
