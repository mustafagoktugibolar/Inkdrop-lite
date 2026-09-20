using Inkdrop_lite.Features.Notes;
using Inkdrop_lite.Features.Notes.Contracts;
using Inkdrop_lite.Tests.Infrastructure;
using InkdropLite.Api.Models;

namespace Inkdrop_lite.Tests.Features.Notes;

public sealed class NoteServiceTests
{
    [Fact]
    public async Task Crud_flow_uses_server_generated_values_and_persists_changes()
    {
        await using var database = await SqliteTestDb.CreateAsync();
        var service = new NoteService(database.Context);
        var notebook = await database.AddNotebookAsync();
        var startedAt = DateTime.UtcNow;

        var created = await service.CreateAsync(
            new CreateNoteRequest("Title", "# Markdown", NoteStatus.Active, notebook.Id),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.InRange(created.CreatedAt, startedAt, DateTime.UtcNow);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);

        var found = await service.GetByIdAsync(
            created.Id,
            CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal(created.Title, found.Title);
        Assert.Equal(created.Content, found.Content);
        Assert.Equal(created.Status, found.Status);
        Assert.Equal(created.NotebookId, found.NotebookId);
        Assert.Equal(created.TagIds, found.TagIds);
        Assert.Equal(created.Pinned, found.Pinned);
        Assert.Equal(created.CreatedAt, found.CreatedAt);
        Assert.Equal(created.UpdatedAt, found.UpdatedAt);

        var updated = await service.UpdateAsync(
            created.Id,
            new UpdateNoteRequest("Updated", "New content", NoteStatus.Completed, notebook.Id),
            CancellationToken.None);

        Assert.True(updated);

        found = await service.GetByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(found);
        Assert.Equal("Updated", found.Title);
        Assert.Equal(NoteStatus.Completed, found.Status);
        Assert.True(found.UpdatedAt >= created.UpdatedAt);

        Assert.True(await service.DeleteAsync(created.Id, CancellationToken.None));
        Assert.Null(await service.GetByIdAsync(created.Id, CancellationToken.None));
        Assert.False(await service.DeleteAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetAll_returns_notes_by_most_recent_update()
    {
        await using var database = await SqliteTestDb.CreateAsync();
        var notebook = await database.AddNotebookAsync();
        var older = new Note
        {
            Title = "Older",
            NotebookId = notebook.Id,
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var newer = new Note
        {
            Title = "Newer",
            NotebookId = notebook.Id,
            UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };

        database.Context.Notes.AddRange(older, newer);
        await database.Context.SaveChangesAsync(CancellationToken.None);

        var notes = await new NoteService(database.Context)
            .GetAllAsync(CancellationToken.None);

        Assert.Equal([newer.Id, older.Id], notes.Select(note => note.Id));
    }
}
