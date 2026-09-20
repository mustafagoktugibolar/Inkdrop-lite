using System.ComponentModel.DataAnnotations;

namespace Inkdrop_lite.Features.Tags.Contracts;

public sealed record UpdateTagRequest(
    [property: Required, MaxLength(100)] string Name);
