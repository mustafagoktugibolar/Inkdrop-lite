using Inkdrop_lite.Data;
using Inkdrop_lite.Features.Common;
using Inkdrop_lite.Features.Notebooks.Contracts;
using InkdropLite.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inkdrop_lite.Features.Notebooks;

public sealed class NotebookService(AppDbContext context) : INotebookService
{
    public async Task<IReadOnlyList<NotebookResponse>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await context.Notebooks
            .AsNoTracking()
            .OrderBy(notebook => notebook.Order == null)
            .ThenBy(notebook => notebook.Order)
            .ThenBy(notebook => notebook.Name)
            .Select(notebook => new NotebookResponse(
                notebook.Id,
                notebook.Name,
                notebook.ParentNotebookId,
                notebook.Order,
                notebook.IconType,
                notebook.IconSvg,
                notebook.IconAttachmentId,
                notebook.CreatedAt,
                notebook.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<NotebookResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await context.Notebooks
            .AsNoTracking()
            .Where(notebook => notebook.Id == id)
            .Select(notebook => new NotebookResponse(
                notebook.Id,
                notebook.Name,
                notebook.ParentNotebookId,
                notebook.Order,
                notebook.IconType,
                notebook.IconSvg,
                notebook.IconAttachmentId,
                notebook.CreatedAt,
                notebook.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<NotebookResponse> CreateAsync(
        CreateNotebookRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateReferencesAsync(
            request.ParentNotebookId,
            request.IconAttachmentId,
            cancellationToken);

        var notebook = new Notebook
        {
            Name = request.Name,
            ParentNotebookId = request.ParentNotebookId,
            Order = request.Order,
            IconType = request.IconType,
            IconSvg = request.IconSvg,
            IconAttachmentId = request.IconAttachmentId
        };

        context.Notebooks.Add(notebook);
        await context.SaveChangesAsync(cancellationToken);

        return ToResponse(notebook);
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        UpdateNotebookRequest request,
        CancellationToken cancellationToken)
    {
        var notebook = await context.Notebooks
            .FirstOrDefaultAsync(notebook => notebook.Id == id, cancellationToken);

        if (notebook is null)
        {
            return false;
        }

        await ValidateReferencesAsync(
            request.ParentNotebookId,
            request.IconAttachmentId,
            cancellationToken);
        await EnsureNoCycleAsync(id, request.ParentNotebookId, cancellationToken);

        notebook.Name = request.Name;
        notebook.ParentNotebookId = request.ParentNotebookId;
        notebook.Order = request.Order;
        notebook.IconType = request.IconType;
        notebook.IconSvg = request.IconSvg;
        notebook.IconAttachmentId = request.IconAttachmentId;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var notebook = await context.Notebooks
            .FirstOrDefaultAsync(notebook => notebook.Id == id, cancellationToken);

        if (notebook is null)
        {
            return false;
        }

        if (await context.Notebooks.AnyAsync(
                child => child.ParentNotebookId == id,
                cancellationToken))
        {
            throw RuleViolationException.Conflict(
                "The notebook has child notebooks. Move or delete them first.");
        }

        if (await context.Notes.AnyAsync(note => note.NotebookId == id, cancellationToken))
        {
            throw RuleViolationException.Conflict(
                "The notebook still contains notes. Move or delete them first.");
        }

        context.Notebooks.Remove(notebook);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static NotebookResponse ToResponse(Notebook notebook) =>
        new(
            notebook.Id,
            notebook.Name,
            notebook.ParentNotebookId,
            notebook.Order,
            notebook.IconType,
            notebook.IconSvg,
            notebook.IconAttachmentId,
            notebook.CreatedAt,
            notebook.UpdatedAt);

    private async Task ValidateReferencesAsync(
        Guid? parentNotebookId,
        Guid? iconAttachmentId,
        CancellationToken cancellationToken)
    {
        if (parentNotebookId is { } parentId &&
            !await context.Notebooks.AnyAsync(notebook => notebook.Id == parentId, cancellationToken))
        {
            throw new RuleViolationException(
                "The parent notebook does not exist or belongs to another user.");
        }

        if (iconAttachmentId is { } attachmentId)
        {
            var contentType = await context.Attachments
                .Where(attachment => attachment.Id == attachmentId)
                .Select(attachment => attachment.ContentType)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new RuleViolationException(
                    "The icon attachment does not exist or belongs to another user.");

            if (!contentType.StartsWith("image/", StringComparison.Ordinal))
            {
                throw new RuleViolationException("The icon attachment must be an image.");
            }
        }
    }

    /// <summary>Rejects a parent that is the notebook itself or one of its descendants.</summary>
    private async Task EnsureNoCycleAsync(
        Guid notebookId,
        Guid? newParentId,
        CancellationToken cancellationToken)
    {
        if (newParentId is null)
        {
            return;
        }

        var parentById = await context.Notebooks
            .AsNoTracking()
            .Select(notebook => new { notebook.Id, notebook.ParentNotebookId })
            .ToDictionaryAsync(
                notebook => notebook.Id,
                notebook => notebook.ParentNotebookId,
                cancellationToken);

        for (var current = newParentId; current is not null;
             current = parentById.GetValueOrDefault(current.Value))
        {
            if (current == notebookId)
            {
                throw new RuleViolationException(
                    "A notebook cannot be moved under itself or one of its descendants.");
            }
        }
    }
}
