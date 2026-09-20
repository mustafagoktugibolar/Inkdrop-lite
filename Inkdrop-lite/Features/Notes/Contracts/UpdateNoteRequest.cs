using System.ComponentModel.DataAnnotations;
using InkdropLite.Api.Models;

namespace Inkdrop_lite.Features.Notes.Contracts;

public sealed record UpdateNoteRequest
{
    public UpdateNoteRequest(
        string title,
        string content,
        NoteStatus status,
        Guid notebookId,
        IReadOnlyCollection<Guid>? tagIds = null,
        bool pinned = false)
    {
        Title = title;
        Content = content;
        Status = status;
        NotebookId = notebookId;
        TagIds = tagIds ?? [];
        Pinned = pinned;
    }

    [Required, MaxLength(256)]
    public string Title { get; init; }

    [Required(AllowEmptyStrings = true), MaxLength(1_048_576)]
    public string Content { get; init; }

    [EnumDataType(typeof(NoteStatus))]
    public NoteStatus Status { get; init; }

    public bool Pinned { get; init; }

    public Guid NotebookId { get; init; }

    public IReadOnlyCollection<Guid> TagIds { get; init; }
}
