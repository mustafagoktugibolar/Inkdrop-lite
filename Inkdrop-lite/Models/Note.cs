using Inkdrop_lite.Domain.Common;

namespace InkdropLite.Api.Models;

public class Note : UpdatableUserOwnedEntity, ISourceTracked
{
    public required string Title { get; set; }

    public string Content { get; set; } = string.Empty;

    public string CreatedSource { get; set; } = ChangeSources.App;

    public string UpdatedSource { get; set; } = ChangeSources.App;

    public NoteStatus Status { get; set; } = NoteStatus.None;

    public bool Pinned { get; set; }

    public Guid NotebookId { get; set; }

    public Notebook Notebook { get; set; } = null!;

    public Guid? SourceTemplateId { get; set; }

    public Note? SourceTemplate { get; set; }

    public List<Note> DerivedNotes { get; set; } = [];

    public List<Tag> Tags { get; set; } = [];

    public List<Attachment> Attachments { get; set; } = [];
}
