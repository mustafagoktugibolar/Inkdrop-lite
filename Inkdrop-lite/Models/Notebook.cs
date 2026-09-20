using Inkdrop_lite.Domain.Common;

namespace InkdropLite.Api.Models;

public class Notebook : UpdatableUserOwnedEntity
{
    public required string Name { get; set; }

    public Guid? ParentNotebookId { get; set; }

    public Notebook? ParentNotebook { get; set; }

    public List<Notebook> ChildNotebooks { get; set; } = [];

    public int? Order { get; set; }

    public NotebookIconType IconType { get; set; } = NotebookIconType.None;

    public string? IconSvg { get; set; }

    public Guid? IconAttachmentId { get; set; }

    public Attachment? IconAttachment { get; set; }

    public List<Note> Notes { get; set; } = [];
}
