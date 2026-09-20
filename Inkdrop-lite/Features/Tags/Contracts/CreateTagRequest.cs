using System.ComponentModel.DataAnnotations;

namespace Inkdrop_lite.Features.Tags.Contracts;

public sealed record CreateTagRequest(
    [property: Required, MaxLength(100)] string Name);
