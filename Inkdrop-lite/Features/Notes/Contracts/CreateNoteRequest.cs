using System.ComponentModel.DataAnnotations;
using InkdropLite.Api.Models;

namespace Inkdrop_lite.Features.Notes.Contracts;

public sealed record CreateNoteRequest(
    [property: Required, MaxLength(200)] string Title,
    [property: MaxLength(10_000_000)] string Content,
    [property: EnumDataType(typeof(NoteStatus))] NoteStatus Status,
    Guid? NotebookId);
