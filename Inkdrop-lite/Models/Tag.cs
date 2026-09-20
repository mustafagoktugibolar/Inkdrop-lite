namespace InkdropLite.Api.Models;

public class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    public List<NoteTag> NoteTags { get; set; } = [];
}
