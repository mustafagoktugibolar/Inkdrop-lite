namespace Inkdrop_lite.Features.Attachments.Contracts;

public sealed record AttachmentResponse(
    Guid Id,
    string Name,
    string ContentType,
    long ContentLength,
    string StoragePath,
    string? Hash,
    DateTime CreatedAt);
