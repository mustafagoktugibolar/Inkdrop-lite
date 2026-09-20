using System.ComponentModel.DataAnnotations;

namespace Inkdrop_lite.Features.Notebooks.Contracts;

public sealed record UpdateNotebookRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: MaxLength(2_000)] string? Description);
