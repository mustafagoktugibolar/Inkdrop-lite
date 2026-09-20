using System.ComponentModel.DataAnnotations;
using InkdropLite.Api.Models;

namespace Inkdrop_lite.Features.Notes.Contracts;

public sealed record CreateNoteRequest
{
    public CreateNoteRequest(
        string title,
        string content,
        NoteStatus status,
        Guid notebookId,
        IReadOnlyCollection<Guid>? tagIds = null,
        bool pinned = false,
        Guid? sourceTemplateId = null)
    {
        Title = title;
        Content = content;
        Status = status;
        NotebookId = notebookId;
        TagIds = tagIds ?? [];
        Pinned = pinned;
        SourceTemplateId = sourceTemplateId;
    }

    [Required, MaxLength(256)]
    public string Title { get; init; }

    [Required(AllowEmptyStrings = true), MaxLength(1_048_576)]
    public string Content { get; init; }

    [EnumDataType(typeof(NoteStatus))]
    public NoteStatus Status { get; init; }

    public bool Pinned { get; init; }

    public Guid NotebookId { get; init; }

    /// <summary>The note this one was created from, when it was started from a template.</summary>
    public Guid? SourceTemplateId { get; init; }

    public IReadOnlyCollection<Guid> TagIds { get; init; }
}
