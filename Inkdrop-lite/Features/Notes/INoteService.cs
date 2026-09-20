using Inkdrop_lite.Features.Notes.Contracts;
using InkdropLite.Api.Models;

namespace Inkdrop_lite.Features.Notes;

public interface INoteService
{
    Task<IReadOnlyList<NoteResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<NoteSummaryResponse>> SearchAsync(
        string? query,
        Guid? notebookId,
        Guid? tagId,
        NoteStatus? status,
        int limit,
        CancellationToken cancellationToken);
    Task<NoteResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<NoteResponse> CreateAsync(CreateNoteRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
