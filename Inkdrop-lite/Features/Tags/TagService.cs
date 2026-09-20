using Inkdrop_lite.Data;
using Inkdrop_lite.Features.Tags.Contracts;
using InkdropLite.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inkdrop_lite.Features.Tags;

public sealed class TagService(AppDbContext context) : ITagService
{
    public async Task<IReadOnlyList<TagResponse>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await context.Tags
            .AsNoTracking()
            .OrderBy(tag => tag.Name)
            .Select(tag => new TagResponse(
                tag.Id,
                tag.Name,
                tag.Color,
                tag.CreatedAt,
                tag.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<TagResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await context.Tags
            .AsNoTracking()
            .Where(tag => tag.Id == id)
            .Select(tag => new TagResponse(
                tag.Id,
                tag.Name,
                tag.Color,
                tag.CreatedAt,
                tag.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TagResponse> CreateAsync(
        CreateTagRequest request,
        CancellationToken cancellationToken)
    {
        var tag = new Tag
        {
            Name = request.Name,
            Color = request.Color
        };

        context.Tags.Add(tag);
        await context.SaveChangesAsync(cancellationToken);

        return ToResponse(tag);
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        UpdateTagRequest request,
        CancellationToken cancellationToken)
    {
        var tag = await context.Tags
            .FirstOrDefaultAsync(tag => tag.Id == id, cancellationToken);

        if (tag is null)
        {
            return false;
        }

        tag.Name = request.Name;
        tag.Color = request.Color;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var tag = await context.Tags
            .FirstOrDefaultAsync(tag => tag.Id == id, cancellationToken);

        if (tag is null)
        {
            return false;
        }

        // NoteTags rows are removed by ON DELETE CASCADE; notes are untouched.
        context.Tags.Remove(tag);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static TagResponse ToResponse(Tag tag) =>
        new(tag.Id, tag.Name, tag.Color, tag.CreatedAt, tag.UpdatedAt);
}
