namespace Inkdrop_lite.Features.Notebooks.Contracts;

public sealed record NotebookResponse(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAt);
