using Inkdrop_lite.Data;
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
                note.NotebookId,
                note.NoteTags.Select(noteTag => noteTag.TagId).ToList(),
                note.CreatedAt,
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
                note.NotebookId,
                note.NoteTags.Select(noteTag => noteTag.TagId).ToList(),
                note.CreatedAt,
                note.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<NoteResponse> CreateAsync(
        CreateNoteRequest request,
        CancellationToken cancellationToken)
    {
        var tagIds = await ValidateReferencesAsync(
            request.NotebookId,
            request.TagIds,
            cancellationToken);
        var note = new Note
        {
            Title = request.Title,
            Content = request.Content,
            Status = request.Status,
            NotebookId = request.NotebookId,
            NoteTags = tagIds
                .Select(tagId => new NoteTag { TagId = tagId })
                .ToList()
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
            .FirstOrDefaultAsync(note => note.Id == id, cancellationToken);

        if (note is null)
        {
            return false;
        }

        var tagIds = await ValidateReferencesAsync(
            request.NotebookId,
            request.TagIds,
            cancellationToken);

        note.Title = request.Title;
        note.Content = request.Content;
        note.Status = request.Status;
        note.NotebookId = request.NotebookId;

        var existingNoteTags = await context.NoteTags
            .IgnoreQueryFilters()
            .Where(noteTag =>
                noteTag.NoteId == note.Id &&
                noteTag.OwnerId == context.CurrentOwnerId)
            .ToListAsync(cancellationToken);
        var requestedTagIds = tagIds.ToHashSet();
        var removedNoteTags = existingNoteTags
            .Where(noteTag => !noteTag.IsDeleted)
            .Where(noteTag => !requestedTagIds.Contains(noteTag.TagId))
            .ToList();

        context.NoteTags.RemoveRange(removedNoteTags);

        var existingTagIds = existingNoteTags
            .Select(noteTag => noteTag.TagId)
            .ToHashSet();

        foreach (var restoredNoteTag in existingNoteTags.Where(noteTag =>
                     noteTag.IsDeleted && requestedTagIds.Contains(noteTag.TagId)))
        {
            restoredNoteTag.IsDeleted = false;
            restoredNoteTag.DeletedAt = null;
        }

        foreach (var tagId in requestedTagIds.Except(existingTagIds))
        {
            context.NoteTags.Add(new NoteTag
            {
                NoteId = note.Id,
                TagId = tagId
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var note = await context.Notes
            .Include(note => note.NoteTags)
            .FirstOrDefaultAsync(note => note.Id == id, cancellationToken);

        if (note is null)
        {
            return false;
        }

        context.NoteTags.RemoveRange(note.NoteTags);
        context.Notes.Remove(note);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static NoteResponse ToResponse(Note note) =>
        new(
            note.Id,
            note.Title,
            note.Content,
            note.Status,
            note.NotebookId,
            note.NoteTags.Select(noteTag => noteTag.TagId).ToList(),
            note.CreatedAt,
            note.UpdatedAt);

    private async Task<IReadOnlyList<Guid>> ValidateReferencesAsync(
        Guid? notebookId,
        IReadOnlyCollection<Guid> requestedTagIds,
        CancellationToken cancellationToken)
    {
        if (notebookId is not null &&
            !await context.Notebooks.AnyAsync(
                notebook => notebook.Id == notebookId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The selected notebook does not exist or belongs to another user.");
        }

        var distinctTagIds = requestedTagIds.Distinct().ToList();
        var accessibleTagIds = await context.Tags
            .Where(tag => distinctTagIds.Contains(tag.Id))
            .Select(tag => tag.Id)
            .ToListAsync(cancellationToken);

        if (accessibleTagIds.Count != distinctTagIds.Count)
        {
            throw new InvalidOperationException(
                "One or more selected tags do not exist or belong to another user.");
        }

        return accessibleTagIds;
    }
}
