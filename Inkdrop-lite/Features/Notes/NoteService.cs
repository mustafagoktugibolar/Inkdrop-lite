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
                note.CreatedAt,
                note.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<NoteResponse> CreateAsync(
        CreateNoteRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var note = new Note
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Content = request.Content,
            Status = request.Status,
            NotebookId = request.NotebookId,
            CreatedAt = now,
            UpdatedAt = now
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

        note.Title = request.Title;
        note.Content = request.Content;
        note.Status = request.Status;
        note.NotebookId = request.NotebookId;
        note.UpdatedAt = DateTime.UtcNow;

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
            note.CreatedAt,
            note.UpdatedAt);
}
