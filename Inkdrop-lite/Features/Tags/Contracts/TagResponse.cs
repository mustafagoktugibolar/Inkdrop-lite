using InkdropLite.Api.Models;

namespace Inkdrop_lite.Features.Tags.Contracts;

public sealed record TagResponse(
    Guid Id,
    string Name,
    TagColor Color,
    DateTime CreatedAt,
    DateTime UpdatedAt);
