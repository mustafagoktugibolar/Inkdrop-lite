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
        var startedAt = DateTime.UtcNow;

        var created = await service.CreateAsync(
            new CreateNoteRequest("Title", "# Markdown", NoteStatus.Active, null),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.InRange(created.CreatedAt, startedAt, DateTime.UtcNow);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);

        var found = await service.GetByIdAsync(
            created.Id,
            CancellationToken.None);

        Assert.Equal(created, found);

        var updated = await service.UpdateAsync(
            created.Id,
            new UpdateNoteRequest("Updated", "New content", NoteStatus.Completed, null),
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
        var older = new Note
        {
            Title = "Older",
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var newer = new Note
        {
            Title = "Newer",
            UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };

        database.Context.Notes.AddRange(older, newer);
        await database.Context.SaveChangesAsync(CancellationToken.None);

        var notes = await new NoteService(database.Context)
            .GetAllAsync(CancellationToken.None);

        Assert.Equal([newer.Id, older.Id], notes.Select(note => note.Id));
    }
}
