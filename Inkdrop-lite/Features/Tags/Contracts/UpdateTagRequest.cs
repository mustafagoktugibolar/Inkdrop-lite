using System.ComponentModel.DataAnnotations;

namespace Inkdrop_lite.Features.Tags.Contracts;

public sealed record UpdateTagRequest
{
    public UpdateTagRequest(string name)
    {
        Name = name;
    }

    [Required, MaxLength(100)]
    public string Name { get; init; }
}
