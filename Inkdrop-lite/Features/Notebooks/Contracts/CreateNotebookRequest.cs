using System.ComponentModel.DataAnnotations;

namespace Inkdrop_lite.Features.Notebooks.Contracts;

public sealed record CreateNotebookRequest
{
    public CreateNotebookRequest(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    [Required, MaxLength(200)]
    public string Name { get; init; }

    [MaxLength(2_000)]
    public string? Description { get; init; }
}
