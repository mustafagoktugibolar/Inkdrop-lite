using System.ComponentModel.DataAnnotations;
using InkdropLite.Api.Models;

namespace Inkdrop_lite.Features.Notes.Contracts;

public sealed record CreateNoteRequest
{
    public CreateNoteRequest(
        string title,
        string content,
        NoteStatus status,
        Guid? notebookId,
        IReadOnlyCollection<Guid>? tagIds = null)
    {
        Title = title;
        Content = content;
        Status = status;
        NotebookId = notebookId;
        TagIds = tagIds ?? [];
    }

    [Required, MaxLength(200)]
    public string Title { get; init; }

    [MaxLength(10_000_000)]
    public string Content { get; init; }

    [EnumDataType(typeof(NoteStatus))]
    public NoteStatus Status { get; init; }

    public Guid? NotebookId { get; init; }

    public IReadOnlyCollection<Guid> TagIds { get; init; }
}
