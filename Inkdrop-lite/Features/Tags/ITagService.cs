using Inkdrop_lite.Features.Tags.Contracts;

namespace Inkdrop_lite.Features.Tags;

public interface ITagService
{
    Task<IReadOnlyList<TagResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<TagResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TagResponse> CreateAsync(CreateTagRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
