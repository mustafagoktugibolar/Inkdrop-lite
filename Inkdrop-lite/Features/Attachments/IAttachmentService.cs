using Inkdrop_lite.Features.Attachments.Contracts;

namespace Inkdrop_lite.Features.Attachments;

public sealed record AttachmentContent(AttachmentResponse Attachment, Stream Content);

public interface IAttachmentService
{
    Task<IReadOnlyList<AttachmentResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<AttachmentResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<AttachmentContent?> OpenContentAsync(Guid id, CancellationToken cancellationToken);
    Task<AttachmentResponse> UploadAsync(
        Stream content,
        string? fileName,
        string? contentType,
        CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Attachments linked to the note, or null when the note does not exist.</summary>
    Task<IReadOnlyList<AttachmentResponse>?> GetForNoteAsync(Guid noteId, CancellationToken cancellationToken);
    Task<bool> AttachToNoteAsync(Guid noteId, Guid attachmentId, CancellationToken cancellationToken);
    Task<bool> DetachFromNoteAsync(Guid noteId, Guid attachmentId, CancellationToken cancellationToken);
}
