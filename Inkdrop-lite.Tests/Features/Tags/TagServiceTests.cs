using Inkdrop_lite.Features.Tags;
using Inkdrop_lite.Features.Tags.Contracts;
using Inkdrop_lite.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Inkdrop_lite.Tests.Features.Tags;

public sealed class TagServiceTests
{
    [Fact]
    public async Task Crud_flow_persists_changes()
    {
        await using var database = await SqliteTestDb.CreateAsync();
        var service = new TagService(database.Context);

        var created = await service.CreateAsync(
            new CreateTagRequest("backend"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.True(await service.UpdateAsync(
            created.Id,
            new UpdateTagRequest("frontend"),
            CancellationToken.None));

        var updated = await service.GetByIdAsync(
            created.Id,
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("frontend", updated.Name);
        Assert.True(await service.DeleteAsync(created.Id, CancellationToken.None));
        Assert.False(await service.DeleteAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Create_rejects_case_insensitive_duplicate_names_at_database_boundary()
    {
        await using var database = await SqliteTestDb.CreateAsync();
        var service = new TagService(database.Context);

        await service.CreateAsync(
            new CreateTagRequest("unique"),
            CancellationToken.None);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(
            new CreateTagRequest("UNIQUE"),
            CancellationToken.None));
    }
}
