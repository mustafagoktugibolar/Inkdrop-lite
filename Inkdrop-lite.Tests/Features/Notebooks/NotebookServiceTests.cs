using Inkdrop_lite.Features.Notebooks;
using Inkdrop_lite.Features.Notebooks.Contracts;
using Inkdrop_lite.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Inkdrop_lite.Tests.Features.Notebooks;

public sealed class NotebookServiceTests
{
    [Fact]
    public async Task Crud_flow_persists_changes()
    {
        await using var database = await SqliteTestDb.CreateAsync();
        var service = new NotebookService(database.Context);

        var created = await service.CreateAsync(
            new CreateNotebookRequest("Work", "Work notes"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Work", created.Name);

        Assert.True(await service.UpdateAsync(
            created.Id,
            new UpdateNotebookRequest("Personal", null),
            CancellationToken.None));

        var updated = await service.GetByIdAsync(
            created.Id,
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Personal", updated.Name);
        Assert.Null(updated.Description);

        Assert.True(await service.DeleteAsync(created.Id, CancellationToken.None));
        Assert.Null(await service.GetByIdAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Create_rejects_duplicate_names_at_database_boundary()
    {
        await using var database = await SqliteTestDb.CreateAsync();
        var service = new NotebookService(database.Context);

        await service.CreateAsync(
            new CreateNotebookRequest("Unique", null),
            CancellationToken.None);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(
            new CreateNotebookRequest("Unique", "Duplicate"),
            CancellationToken.None));
    }
}
