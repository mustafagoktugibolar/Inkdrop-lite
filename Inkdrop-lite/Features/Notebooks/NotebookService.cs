using Inkdrop_lite.Data;
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
            .OrderBy(notebook => notebook.Name)
            .Select(notebook => new NotebookResponse(
                notebook.Id,
                notebook.Name,
                notebook.Description,
                notebook.CreatedAt))
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
                notebook.Description,
                notebook.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<NotebookResponse> CreateAsync(
        CreateNotebookRequest request,
        CancellationToken cancellationToken)
    {
        var notebook = new Notebook
        {
            Name = request.Name,
            Description = request.Description
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

        notebook.Name = request.Name;
        notebook.Description = request.Description;

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

        var notes = await context.Notes
            .Include(note => note.NoteTags)
            .Where(note => note.NotebookId == notebook.Id)
            .ToListAsync(cancellationToken);

        context.NoteTags.RemoveRange(notes.SelectMany(note => note.NoteTags));
        context.Notes.RemoveRange(notes);
        context.Notebooks.Remove(notebook);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static NotebookResponse ToResponse(Notebook notebook) =>
        new(notebook.Id, notebook.Name, notebook.Description, notebook.CreatedAt);
}
