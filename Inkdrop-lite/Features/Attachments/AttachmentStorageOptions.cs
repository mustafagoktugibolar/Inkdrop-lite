namespace Inkdrop_lite.Features.Attachments;

/// <summary>Where attachment bytes live and how large one upload may be.</summary>
public sealed class AttachmentStorageOptions
{
    public const long DefaultMaxBytes = 10 * 1024 * 1024;

    /// <summary>Storage root. <see cref="InkdropLite.Api.Models.Attachment.StoragePath"/> is relative to it.</summary>
    public required string Root { get; init; }

    public long MaxBytes { get; init; } = DefaultMaxBytes;
}
