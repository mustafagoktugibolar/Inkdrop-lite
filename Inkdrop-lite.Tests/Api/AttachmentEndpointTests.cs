using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Inkdrop_lite.Features.Attachments.Contracts;
using Inkdrop_lite.Features.Notebooks.Contracts;
using Inkdrop_lite.Features.Notes.Contracts;
using Inkdrop_lite.Tests.Infrastructure;
using InkdropLite.Api.Models;

namespace Inkdrop_lite.Tests.Api;

public sealed class AttachmentEndpointTests : IClassFixture<InkdropWebApplicationFactory>
{
    private readonly InkdropWebApplicationFactory _factory;

    public AttachmentEndpointTests(InkdropWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        byte[] bytes,
        string fileName,
        string contentType = "text/plain")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/attachments", form);
    }

    private static async Task<AttachmentResponse> UploadOkAsync(
        HttpClient client,
        byte[] bytes,
        string fileName,
        string contentType = "text/plain")
    {
        var response = await UploadAsync(client, bytes, fileName, contentType);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AttachmentResponse>())!;
    }

    private static async Task<NotebookResponse> CreateNotebookAsync(
        HttpClient client,
        NotebookIconType iconType = NotebookIconType.None,
        Guid? iconAttachmentId = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/notebooks",
            new CreateNotebookRequest(
                $"Nb-{Guid.NewGuid():N}"[..20],
                iconType: iconType,
                iconAttachmentId: iconAttachmentId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<NotebookResponse>())!;
    }

    private string DiskPath(AttachmentResponse attachment) =>
        Path.Combine(_factory.AttachmentRoot, attachment.StoragePath);

    [Fact]
    public async Task Upload_stores_metadata_and_bytes_and_download_returns_them_safely()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var bytes = Encoding.UTF8.GetBytes("hello attachment");

        var attachment = await UploadOkAsync(client, bytes, "notes.txt");

        Assert.Equal("notes.txt", attachment.Name);
        Assert.Equal("text/plain", attachment.ContentType);
        Assert.Equal(bytes.Length, attachment.ContentLength);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), attachment.Hash);
        Assert.StartsWith("attachments/", attachment.StoragePath);
        Assert.True(File.Exists(DiskPath(attachment)));

        var download = await client.GetAsync($"/api/attachments/{attachment.Id}/content");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("sandbox", download.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
    }

    [Fact]
    public async Task Upload_ignores_directories_in_the_client_file_name()
    {
        using var client = _factory.CreateAuthenticatedClient();

        var attachment = await UploadOkAsync(client, [1, 2, 3], "../../etc/evil.txt");

        Assert.Equal("evil.txt", attachment.Name);
        Assert.DoesNotContain("..", attachment.StoragePath);
        Assert.DoesNotContain("evil", attachment.StoragePath);
        var full = Path.GetFullPath(DiskPath(attachment));
        Assert.StartsWith(Path.GetFullPath(_factory.AttachmentRoot), full);
        Assert.True(File.Exists(full));
    }

    [Fact]
    public async Task Upload_rejects_empty_files_and_files_over_the_limit()
    {
        using var client = _factory.CreateAuthenticatedClient();

        var empty = await UploadAsync(client, [], "empty.txt");
        var tooBig = await UploadAsync(
            client,
            new byte[InkdropWebApplicationFactory.AttachmentMaxBytes + 1],
            "big.bin",
            "application/octet-stream");

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooBig.StatusCode);
        var attachments = await client.GetFromJsonAsync<AttachmentResponse[]>("/api/attachments");
        Assert.DoesNotContain(attachments!, item => item.Name is "empty.txt" or "big.bin");
    }

    [Fact]
    public async Task Untrusted_content_type_falls_back_to_octet_stream()
    {
        using var client = _factory.CreateAuthenticatedClient();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent([1]);
        file.Headers.TryAddWithoutValidation("Content-Type", "not a mime; <script>");
        form.Add(file, "file", "x.bin");

        var response = await client.PostAsync("/api/attachments", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var attachment = await response.Content.ReadFromJsonAsync<AttachmentResponse>();
        Assert.Equal("application/octet-stream", attachment!.ContentType);
    }

    [Fact]
    public async Task Attachments_are_private_to_their_owner()
    {
        using var alice = _factory.CreateAuthenticatedClient(userId: "alice");
        using var bob = _factory.CreateAuthenticatedClient(userId: "bob");
        var attachment = await UploadOkAsync(alice, [1, 2, 3], "private.txt");

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/attachments/{attachment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/attachments/{attachment.Id}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/attachments/{attachment.Id}")).StatusCode);
        Assert.DoesNotContain(
            await bob.GetFromJsonAsync<AttachmentResponse[]>("/api/attachments") ?? [],
            item => item.Id == attachment.Id);
        Assert.True(File.Exists(DiskPath(attachment)));
    }

    [Fact]
    public async Task Delete_removes_the_file_and_clears_notebook_icons()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var attachment = await UploadOkAsync(client, [1, 2, 3], "icon.png", "image/png");
        var notebook = await CreateNotebookAsync(client, NotebookIconType.Attachment, attachment.Id);

        var delete = await client.DeleteAsync($"/api/attachments/{attachment.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.False(File.Exists(DiskPath(attachment)));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/attachments/{attachment.Id}")).StatusCode);
        var reloaded = await client.GetFromJsonAsync<NotebookResponse>($"/api/notebooks/{notebook.Id}");
        Assert.Null(reloaded!.IconAttachmentId);
    }

    [Fact]
    public async Task Notebook_icon_attachment_must_be_an_image()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var text = await UploadOkAsync(client, [1], "not-image.txt");

        var response = await client.PostAsJsonAsync(
            "/api/notebooks",
            new CreateNotebookRequest("Icons", iconType: NotebookIconType.Attachment, iconAttachmentId: text.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Attachments_can_be_linked_to_and_unlinked_from_a_note()
    {
        using var client = _factory.CreateAuthenticatedClient();
        var notebook = await CreateNotebookAsync(client);
        var note = (await (await client.PostAsJsonAsync(
            "/api/notes",
            new CreateNoteRequest("With files", "", NoteStatus.Active, notebook.Id)))
            .Content.ReadFromJsonAsync<NoteResponse>())!;
        var attachment = await UploadOkAsync(client, [1, 2], "a.txt");

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/notes/{note.Id}/attachments/{attachment.Id}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/notes/{note.Id}/attachments/{attachment.Id}", null)).StatusCode);
        var linked = await client.GetFromJsonAsync<AttachmentResponse[]>($"/api/notes/{note.Id}/attachments");
        Assert.Equal([attachment.Id], linked!.Select(item => item.Id));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/notes/{note.Id}/attachments/{attachment.Id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<AttachmentResponse[]>($"/api/notes/{note.Id}/attachments"))!);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/attachments/{attachment.Id}")).StatusCode);
    }

    [Fact]
    public async Task Another_users_attachment_cannot_be_linked_to_my_note()
    {
        using var alice = _factory.CreateAuthenticatedClient(userId: "alice");
        using var bob = _factory.CreateAuthenticatedClient(userId: "bob");
        var notebook = await CreateNotebookAsync(bob);
        var note = (await (await bob.PostAsJsonAsync(
            "/api/notes",
            new CreateNoteRequest("Bob note", "", NoteStatus.Active, notebook.Id)))
            .Content.ReadFromJsonAsync<NoteResponse>())!;
        var alicesFile = await UploadOkAsync(alice, [1], "alice.txt");

        var response = await bob.PutAsync($"/api/notes/{note.Id}/attachments/{alicesFile.Id}", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Attachment_endpoints_require_authentication()
    {
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/attachments")).StatusCode);
    }
}
