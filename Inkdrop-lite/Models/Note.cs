namespace InkdropLite.Api.Models;

public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Title { get; set; }

    public string Content { get; set; } = string.Empty;

    public NoteStatus Status { get; set; } = NoteStatus.Active;

    public Guid? NotebookId { get; set; }

    public Notebook? Notebook { get; set; }

    public List<NoteTag> NoteTags { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
