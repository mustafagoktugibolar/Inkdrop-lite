using Inkdrop_lite.Features.Notebooks.Contracts;

namespace Inkdrop_lite.Features.Notebooks;

public interface INotebookService
{
    Task<IReadOnlyList<NotebookResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<NotebookResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<NotebookResponse> CreateAsync(CreateNotebookRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Guid id, UpdateNotebookRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
