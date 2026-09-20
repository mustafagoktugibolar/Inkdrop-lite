using System.ComponentModel.DataAnnotations;

namespace Inkdrop_lite.Features.Tags.Contracts;

public sealed record CreateTagRequest
{
    public CreateTagRequest(string name)
    {
        Name = name;
    }

    [Required, MaxLength(100)]
    public string Name { get; init; }
}
