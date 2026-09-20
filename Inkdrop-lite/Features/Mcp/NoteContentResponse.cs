using Inkdrop_lite.Features.Notes.Contracts;
using InkdropLite.Api.Models;

namespace Inkdrop_lite.Features.Mcp;

/// <summary>What get_note returns: note metadata plus one page (or section) of its Markdown.</summary>
public sealed record NoteContentResponse(
    Guid Id,
    string Title,
    NoteStatus Status,
    bool Pinned,
    Guid NotebookId,
    Guid? SourceTemplateId,
    IReadOnlyList<Guid> TagIds,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string CreatedSource,
    string UpdatedSource,
    int TotalChars,
    int Offset,
    bool HasMore,
    int? NextOffset,
    string? Section,
    IReadOnlyList<OutlineEntry>? Outline,
    string? Hint,
    string Content)
{
    public static NoteContentResponse From(NoteResponse note, ContentPage page) =>
        new(
            note.Id,
            note.Title,
            note.Status,
            note.Pinned,
            note.NotebookId,
            note.SourceTemplateId,
            note.TagIds,
            note.CreatedAt,
            note.UpdatedAt,
            note.CreatedSource,
            note.UpdatedSource,
            page.TotalChars,
            page.Offset,
            page.HasMore,
            page.NextOffset,
            page.Section,
            page.Outline,
            page.Hint,
            page.Content);
}
