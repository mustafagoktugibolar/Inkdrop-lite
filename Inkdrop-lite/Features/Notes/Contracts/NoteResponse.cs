using InkdropLite.Api.Models;

namespace Inkdrop_lite.Features.Notes.Contracts;

public sealed record NoteResponse(
    Guid Id,
    string Title,
    string Content,
    NoteStatus Status,
    Guid? NotebookId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
