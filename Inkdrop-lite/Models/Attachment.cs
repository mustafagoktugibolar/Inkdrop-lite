using Inkdrop_lite.Domain.Common;

namespace InkdropLite.Api.Models;

/// <summary>File metadata only; the bytes live outside SQLite at <see cref="StoragePath"/>.</summary>
public class Attachment : UserOwnedEntity
{
    public required string Name { get; set; }

    public required string ContentType { get; set; }

    public long ContentLength { get; set; }

    /// <summary>Path relative to the storage root, e.g. <c>attachments/{guid}-image.png</c>.</summary>
    public required string StoragePath { get; set; }

    public string? Hash { get; set; }

    public List<Note> Notes { get; set; } = [];

    public List<Notebook> NotebooksUsingAsIcon { get; set; } = [];
}
