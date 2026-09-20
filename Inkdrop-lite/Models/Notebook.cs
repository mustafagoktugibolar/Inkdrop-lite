using Inkdrop_lite.Domain.Common;

namespace InkdropLite.Api.Models;

public class Notebook : UserOwnedEntity
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    public List<Note> Notes { get; set; } = [];

}
