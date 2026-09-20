using InkdropLite.Api.Models;

namespace Inkdrop_lite.Features.Notebooks.Contracts;

public sealed record NotebookResponse(
    Guid Id,
    string Name,
    Guid? ParentNotebookId,
    int? Order,
    NotebookIconType IconType,
    string? IconSvg,
    Guid? IconAttachmentId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
