using Inkdrop_lite.Data;
using Inkdrop_lite.Features.Common;
using Inkdrop_lite.Features.Notes.Contracts;
using InkdropLite.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Inkdrop_lite.Features.Notes;

public sealed class NoteService(AppDbContext context) : INoteService
{
    public async Task<IReadOnlyList<NoteResponse>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await context.Notes
            .AsNoTracking()
            .OrderByDescending(note => note.UpdatedAt)
            .Select(note => new NoteResponse(
                note.Id,
                note.Title,
                note.Content,
                note.Status,
                note.Pinned,
                note.NotebookId,
                note.SourceTemplateId,
                note.Tags.Select(tag => tag.Id).ToList(),
                note.CreatedAt,
                note.UpdatedAt,
                note.CreatedSource,
                note.UpdatedSource))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NoteSummaryResponse>> SearchAsync(
        string? query,
        Guid? notebookId,
        Guid? tagId,
        NoteStatus? status,
        int limit,
        CancellationToken cancellationToken)
    {
        var notes = context.Notes.AsNoTracking();

        if (notebookId is not null)
        {
            notes = notes.Where(note => note.NotebookId == notebookId);
        }

        if (tagId is not null)
        {
            notes = notes.Where(note => note.Tags.Any(tag => tag.Id == tagId));
        }

        if (status is not null)
        {
            notes = notes.Where(note => note.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{EscapeLike(query.Trim())}%";

            notes = notes.Where(note =>
                EF.Functions.Like(note.Title, pattern, "\\") ||
                EF.Functions.Like(note.Content, pattern, "\\") ||
                EF.Functions.Like(note.Notebook.Name, pattern, "\\") ||
                note.Tags.Any(tag => EF.Functions.Like(tag.Name, pattern, "\\")));
        }

        return await notes
            .OrderByDescending(note => note.UpdatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(note => new NoteSummaryResponse(
                note.Id,
                note.Title,
                note.Status,
                note.Pinned,
                note.NotebookId,
                note.Content.Length,
                note.Tags.Select(tag => tag.Id).ToList(),
                note.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<NoteResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await context.Notes
            .AsNoTracking()
            .Where(note => note.Id == id)
            .Select(note => new NoteResponse(
                note.Id,
                note.Title,
                note.Content,
                note.Status,
                note.Pinned,
                note.NotebookId,
                note.SourceTemplateId,
                note.Tags.Select(tag => tag.Id).ToList(),
                note.CreatedAt,
                note.UpdatedAt,
                note.CreatedSource,
                note.UpdatedSource))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<NoteResponse> CreateAsync(
        CreateNoteRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureNotebookExistsAsync(request.NotebookId, cancellationToken);

        if (request.SourceTemplateId is { } templateId &&
            !await context.Notes.AnyAsync(note => note.Id == templateId, cancellationToken))
        {
            throw new RuleViolationException("The source template note does not exist.");
        }

        var note = new Note
        {
            Title = request.Title,
            Content = request.Content,
            Status = request.Status,
            Pinned = request.Pinned,
            NotebookId = request.NotebookId,
            SourceTemplateId = request.SourceTemplateId,
            Tags = await LoadTagsAsync(request.TagIds, cancellationToken)
        };

        context.Notes.Add(note);
        await context.SaveChangesAsync(cancellationToken);

        return ToResponse(note);
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        UpdateNoteRequest request,
        CancellationToken cancellationToken)
    {
        var note = await context.Notes
            .Include(note => note.Tags)
            .FirstOrDefaultAsync(note => note.Id == id, cancellationToken);

        if (note is null)
        {
            return false;
        }

        await EnsureNotebookExistsAsync(request.NotebookId, cancellationToken);
        var requestedTags = await LoadTagsAsync(request.TagIds, cancellationToken);

        note.Title = request.Title;
        note.Content = request.Content;
        note.Status = request.Status;
        note.Pinned = request.Pinned;
        note.NotebookId = request.NotebookId;

        var requestedTagIds = requestedTags.Select(tag => tag.Id).ToHashSet();
        note.Tags.RemoveAll(tag => !requestedTagIds.Contains(tag.Id));

        var currentTagIds = note.Tags.Select(tag => tag.Id).ToHashSet();
        note.Tags.AddRange(requestedTags.Where(tag => !currentTagIds.Contains(tag.Id)));

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var note = await context.Notes
            .FirstOrDefaultAsync(note => note.Id == id, cancellationToken);

        if (note is null)
        {
            return false;
        }

        // Join rows are removed by ON DELETE CASCADE; derived notes get SourceTemplateId = NULL.
        context.Notes.Remove(note);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static NoteResponse ToResponse(Note note) =>
        new(
            note.Id,
            note.Title,
            note.Content,
            note.Status,
            note.Pinned,
            note.NotebookId,
            note.SourceTemplateId,
            note.Tags.Select(tag => tag.Id).ToList(),
            note.CreatedAt,
            note.UpdatedAt,
            note.CreatedSource,
            note.UpdatedSource);

    private async Task EnsureNotebookExistsAsync(
        Guid notebookId,
        CancellationToken cancellationToken)
    {
        if (!await context.Notebooks.AnyAsync(
                notebook => notebook.Id == notebookId,
                cancellationToken))
        {
            throw new RuleViolationException(
                "The selected notebook does not exist or belongs to another user.");
        }
    }

    private async Task<List<Tag>> LoadTagsAsync(
        IReadOnlyCollection<Guid> requestedTagIds,
        CancellationToken cancellationToken)
    {
        var distinctTagIds = requestedTagIds.Distinct().ToList();
        var tags = await context.Tags
            .Where(tag => distinctTagIds.Contains(tag.Id))
            .ToListAsync(cancellationToken);

        if (tags.Count != distinctTagIds.Count)
        {
            throw new RuleViolationException(
                "One or more selected tags do not exist or belong to another user.");
        }

        return tags;
    }
}
