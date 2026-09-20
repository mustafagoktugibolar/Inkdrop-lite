using InkdropLite.Api.Models;

namespace Inkdrop_lite.Features.Notes.Contracts;

public sealed record NoteResponse(
    Guid Id,
    string Title,
    string Content,
    NoteStatus Status,
    bool Pinned,
    Guid NotebookId,
    Guid? SourceTemplateId,
    IReadOnlyList<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CreatedSource,
    string UpdatedSource);
