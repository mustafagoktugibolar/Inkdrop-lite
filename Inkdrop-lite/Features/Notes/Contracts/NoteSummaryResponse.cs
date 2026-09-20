using InkdropLite.Api.Models;

namespace Inkdrop_lite.Features.Notes.Contracts;

/// <summary>A note without its Markdown content; ContentLength (characters) tells how costly reading it is.</summary>
public sealed record NoteSummaryResponse(
    Guid Id,
    string Title,
    NoteStatus Status,
    bool Pinned,
    Guid NotebookId,
    int ContentLength,
    IReadOnlyList<Guid> TagIds,
    DateTime UpdatedAt);
