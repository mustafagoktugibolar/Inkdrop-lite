using Inkdrop_lite.Features.Common;
using Inkdrop_lite.Features.Notebooks;
using Inkdrop_lite.Features.Notebooks.Contracts;
using Inkdrop_lite.Tests.Infrastructure;
using InkdropLite.Api.Models;

namespace Inkdrop_lite.Tests.Features.Notebooks;

public sealed class NotebookServiceTests
{
    [Fact]
    public async Task Crud_flow_persists_changes()
    {
        await using var database = await SqliteTestDb.CreateAsync();
        var service = new NotebookService(database.Context);

        var created = await service.CreateAsync(
            new CreateNotebookRequest("Work"),
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Work", created.Name);

        Assert.True(await service.UpdateAsync(
            created.Id,
            new UpdateNotebookRequest("Personal"),
            CancellationToken.None));

        var updated = await service.GetByIdAsync(
            created.Id,
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal("Personal", updated.Name);
        Assert.True(updated.UpdatedAt >= created.UpdatedAt);

        Assert.True(await service.DeleteAsync(created.Id, CancellationToken.None));
        Assert.Null(await service.GetByIdAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Same_name_is_allowed_under_different_parents()
    {
        await using var database = await SqliteTestDb.CreateAsync();
        var service = new NotebookService(database.Context);
        var a = await service.CreateAsync(new CreateNotebookRequest("A"), CancellationToken.None);
        var b = await service.CreateAsync(new CreateNotebookRequest("B"), CancellationToken.None);

        await service.CreateAsync(new CreateNotebookRequest("Docs", a.Id), CancellationToken.None);
        var second = await service.CreateAsync(
            new CreateNotebookRequest("Docs", b.Id),
            CancellationToken.None);

        Assert.Equal(b.Id, second.ParentNotebookId);
    }

    [Fact]
    public async Task Update_rejects_self_and_descendant_parents()
    {
        await using var database = await SqliteTestDb.CreateAsync();
        var service = new NotebookService(database.Context);
        var a = await service.CreateAsync(new CreateNotebookRequest("A"), CancellationToken.None);
        var b = await service.CreateAsync(new CreateNotebookRequest("B", a.Id), CancellationToken.None);
        var c = await service.CreateAsync(new CreateNotebookRequest("C", b.Id), CancellationToken.None);

        await Assert.ThrowsAsync<RuleViolationException>(() => service.UpdateAsync(
            a.Id,
            new UpdateNotebookRequest("A", a.Id),
            CancellationToken.None));
        await Assert.ThrowsAsync<RuleViolationException>(() => service.UpdateAsync(
            a.Id,
            new UpdateNotebookRequest("A", c.Id),
            CancellationToken.None));
    }

    [Fact]
    public async Task Delete_is_rejected_while_notebook_has_children_or_notes()
    {
        await using var database = await SqliteTestDb.CreateAsync();
        var service = new NotebookService(database.Context);
        var parent = await service.CreateAsync(new CreateNotebookRequest("Parent"), CancellationToken.None);
        var child = await service.CreateAsync(
            new CreateNotebookRequest("Child", parent.Id),
            CancellationToken.None);

        await Assert.ThrowsAsync<RuleViolationException>(() =>
            service.DeleteAsync(parent.Id, CancellationToken.None));

        database.Context.Notes.Add(new Note { Title = "N", NotebookId = child.Id });
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<RuleViolationException>(() =>
            service.DeleteAsync(child.Id, CancellationToken.None));
        Assert.NotNull(await service.GetByIdAsync(child.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Deleting_an_icon_attachment_clears_the_notebook_icon_reference()
    {
        await using var database = await SqliteTestDb.CreateAsync();
        var attachment = new Attachment
        {
            Name = "icon.png",
            ContentType = "image/png",
            StoragePath = "attachments/icon.png"
        };
        database.Context.Attachments.Add(attachment);
        await database.Context.SaveChangesAsync();

        var service = new NotebookService(database.Context);
        var created = await service.CreateAsync(
            new CreateNotebookRequest(
                "Iconic",
                iconType: NotebookIconType.Attachment,
                iconAttachmentId: attachment.Id),
            CancellationToken.None);

        database.Context.Attachments.Remove(attachment);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var reloaded = await service.GetByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.IconAttachmentId);
    }
}
