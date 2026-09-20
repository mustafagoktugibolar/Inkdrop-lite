using System.Net;
using System.Net.Http.Json;
using Inkdrop_lite.Features.Notebooks.Contracts;
using Inkdrop_lite.Features.Notes.Contracts;
using Inkdrop_lite.Features.Tags.Contracts;
using Inkdrop_lite.Data;
using Inkdrop_lite.Tests.Infrastructure;
using InkdropLite.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Inkdrop_lite.Tests.Api;

public sealed class ApiBoundaryTests : IClassFixture<InkdropWebApplicationFactory>
{
    private readonly InkdropWebApplicationFactory _factory;

    public ApiBoundaryTests(InkdropWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Liveness_endpoint_allows_anonymous_requests()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/health/live",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Api_endpoint_rejects_unauthenticated_requests()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(
            "/api/notes",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Api_endpoint_rejects_tokens_without_required_scope()
    {
        using var client = _factory.CreateAuthenticatedClient(scope: null);

        var response = await client.GetAsync(
            "/api/notes",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Api_endpoint_accepts_configured_scope()
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync(
            "/api/notes",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Cors_preflight_allows_the_vite_development_origin()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/notes");
        request.Headers.Add("Origin", "http://localhost:52364");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "http://localhost:52364",
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Notes_endpoints_complete_the_full_crud_flow()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var title = $"Note-{Guid.NewGuid():N}";
        var tagCreateResponse = await client.PostAsJsonAsync(
            "/api/tags",
            new CreateTagRequest($"NoteTag-{Guid.NewGuid():N}"),
            CancellationToken.None);
        var tag = await tagCreateResponse.Content.ReadFromJsonAsync<TagResponse>(
            CancellationToken.None);
        Assert.NotNull(tag);

        var createResponse = await client.PostAsJsonAsync(
            "/api/notes",
            new CreateNoteRequest(title, "# Created", NoteStatus.Active, null, [tag.Id]),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<NoteResponse>(
            CancellationToken.None);
        Assert.NotNull(created);
        Assert.Contains(tag.Id, created.TagIds);

        var getResponse = await client.GetAsync($"/api/notes/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/notes/{created.Id}",
            new UpdateNoteRequest(title, "# Updated", NoteStatus.Completed, null, []),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var updated = await client.GetFromJsonAsync<NoteResponse>(
            $"/api/notes/{created.Id}",
            CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("# Updated", updated.Content);
        Assert.Equal(NoteStatus.Completed, updated.Status);
        Assert.Empty(updated.TagIds);

        var restoreTagResponse = await client.PutAsJsonAsync(
            $"/api/notes/{created.Id}",
            new UpdateNoteRequest(
                title,
                "# Updated",
                NoteStatus.Completed,
                null,
                [tag.Id]),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.NoContent, restoreTagResponse.StatusCode);
        updated = await client.GetFromJsonAsync<NoteResponse>(
            $"/api/notes/{created.Id}",
            CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Contains(tag.Id, updated.TagIds);

        var deleteResponse = await client.DeleteAsync(
            $"/api/notes/{created.Id}",
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/notes/{created.Id}")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deletedNote = await database.Notes
            .IgnoreQueryFilters()
            .SingleAsync(note => note.Id == created.Id);
        var deletedNoteTag = await database.NoteTags
            .IgnoreQueryFilters()
            .SingleAsync(noteTag =>
                noteTag.NoteId == created.Id && noteTag.TagId == tag.Id);
        Assert.True(deletedNote.IsDeleted);
        Assert.NotNull(deletedNote.DeletedAt);
        Assert.True(deletedNoteTag.IsDeleted);
    }

    [Fact]
    public async Task Notebooks_endpoints_complete_the_full_crud_flow()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var name = $"Notebook-{Guid.NewGuid():N}";

        var createResponse = await client.PostAsJsonAsync(
            "/api/notebooks",
            new CreateNotebookRequest(name, "Created"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<NotebookResponse>(
            CancellationToken.None);
        Assert.NotNull(created);

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync($"/api/notebooks/{created.Id}")).StatusCode);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/notebooks/{created.Id}",
            new UpdateNotebookRequest($"{name}-updated", "Updated"),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var updated = await client.GetFromJsonAsync<NotebookResponse>(
            $"/api/notebooks/{created.Id}",
            CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("Updated", updated.Description);

        var linkedNoteResponse = await client.PostAsJsonAsync(
            "/api/notes",
            new CreateNoteRequest(
                $"Linked-{Guid.NewGuid():N}",
                "Deleted with its notebook",
                NoteStatus.Active,
                created.Id),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, linkedNoteResponse.StatusCode);
        var linkedNote = await linkedNoteResponse.Content.ReadFromJsonAsync<NoteResponse>(
            CancellationToken.None);
        Assert.NotNull(linkedNote);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/notebooks/{created.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/notebooks/{created.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/notes/{linkedNote.Id}")).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deletedNotebook = await database.Notebooks
            .IgnoreQueryFilters()
            .SingleAsync(notebook => notebook.Id == created.Id);
        var deletedLinkedNote = await database.Notes
            .IgnoreQueryFilters()
            .SingleAsync(note => note.Id == linkedNote.Id);
        Assert.True(deletedNotebook.IsDeleted);
        Assert.True(deletedLinkedNote.IsDeleted);
    }

    [Fact]
    public async Task Tags_endpoints_complete_the_full_crud_flow()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var name = $"Tag-{Guid.NewGuid():N}";

        var createResponse = await client.PostAsJsonAsync(
            "/api/tags",
            new CreateTagRequest(name),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<TagResponse>(
            CancellationToken.None);
        Assert.NotNull(created);

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync($"/api/tags/{created.Id}")).StatusCode);

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/tags/{created.Id}",
            new UpdateTagRequest($"{name}-updated"),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.StatusCode);

        var updated = await client.GetFromJsonAsync<TagResponse>(
            $"/api/tags/{created.Id}",
            CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal($"{name}-updated", updated.Name);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/tags/{created.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/tags/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Users_only_see_and_change_their_own_data()
    {
        using var alice = _factory.CreateAuthenticatedClient(userId: "alice");
        using var bob = _factory.CreateAuthenticatedClient(userId: "bob");
        var sharedName = $"Private-{Guid.NewGuid():N}";

        var aliceCreateResponse = await alice.PostAsJsonAsync(
            "/api/tags",
            new CreateTagRequest(sharedName),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, aliceCreateResponse.StatusCode);
        var aliceTag = await aliceCreateResponse.Content.ReadFromJsonAsync<TagResponse>(
            CancellationToken.None);
        Assert.NotNull(aliceTag);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await bob.GetAsync($"/api/tags/{aliceTag.Id}")).StatusCode);
        Assert.DoesNotContain(
            await bob.GetFromJsonAsync<TagResponse[]>("/api/tags") ?? [],
            tag => tag.Id == aliceTag.Id);

        var bobCreateResponse = await bob.PostAsJsonAsync(
            "/api/tags",
            new CreateTagRequest(sharedName),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, bobCreateResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await bob.DeleteAsync($"/api/tags/{aliceTag.Id}")).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await alice.GetAsync($"/api/tags/{aliceTag.Id}")).StatusCode);
    }
}
