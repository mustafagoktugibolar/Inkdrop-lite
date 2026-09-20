using Inkdrop_lite.Domain.Common;

namespace InkdropLite.Api.Models;

public class Note : UserOwnedEntity
{
    public required string Title { get; set; }

    public string Content { get; set; } = string.Empty;

    public NoteStatus Status { get; set; } = NoteStatus.Active;

    public Guid? NotebookId { get; set; }

    public Notebook? Notebook { get; set; }

    public List<NoteTag> NoteTags { get; set; } = [];

}
