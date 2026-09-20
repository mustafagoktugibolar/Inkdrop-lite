using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Inkdrop_lite.Features.Mcp.OAuth;
using Inkdrop_lite.Features.Notebooks.Contracts;
using Inkdrop_lite.Features.Notes.Contracts;
using Inkdrop_lite.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Inkdrop_lite.Tests.Api;

public sealed class McpEndpointTests : IClassFixture<InkdropWebApplicationFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly InkdropWebApplicationFactory _factory;

    public McpEndpointTests(InkdropWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Mcp_endpoint_rejects_unauthenticated_requests()
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/mcp", content, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Mcp_endpoint_rejects_tokens_without_required_scope()
    {
        using var client = _factory.CreateAuthenticatedClient(scope: null);
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/mcp", content, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Mcp_endpoint_rejects_tokens_that_only_carry_the_api_scope()
    {
        using var client = _factory.CreateAuthenticatedClient();
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/mcp", content, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Api_rejects_tokens_that_only_carry_the_mcp_scope()
    {
        using var client = _factory.CreateAuthenticatedClient(InkdropWebApplicationFactory.McpScope);

        foreach (var request in new[]
                 {
                     new HttpRequestMessage(HttpMethod.Get, "/api/notes"),
                     new HttpRequestMessage(HttpMethod.Delete, $"/api/notes/{Guid.NewGuid()}")
                 })
        {
            var response = await client.SendAsync(request, CancellationToken.None);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task Tools_are_listed_and_none_can_delete()
    {
        await using var mcp = await ConnectAsync("alice");

        var tools = (await mcp.ListToolsAsync(cancellationToken: CancellationToken.None))
            .Select(tool => tool.Name)
            .ToList();

        Assert.Equal(
            ["create_note", "get_note", "list_notebooks", "list_tags", "search_notes", "update_note"],
            tools.Order());
        Assert.DoesNotContain(tools, name => name.Contains("delete", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Agent_can_create_search_read_and_update_notes()
    {
        await using var mcp = await ConnectAsync("agent-user");
        var notebook = await CreateNotebookAsync("agent-user");
        var marker = $"marker{Guid.NewGuid():N}";

        var created = await CallAsync<NoteResponse>(mcp, "create_note", new()
        {
            ["title"] = "Agent note",
            ["notebookId"] = notebook.Id,
            ["content"] = $"# Hello {marker}",
            ["status"] = "Active"
        });
        Assert.Equal("Active", created.Status.ToString());

        var found = await CallAsync<List<NoteSummaryResponse>>(mcp, "search_notes", new()
        {
            ["query"] = marker
        });
        Assert.Equal([created.Id], found.Select(note => note.Id));

        var updated = await CallAsync<NoteResponse>(mcp, "update_note", new()
        {
            ["id"] = created.Id,
            ["pinned"] = true
        });
        Assert.True(updated.Pinned);
        Assert.Equal(created.Title, updated.Title);
        Assert.Equal(created.Content, updated.Content);

        var read = await CallAsync<NoteResponse>(mcp, "get_note", new() { ["id"] = created.Id });
        Assert.Equal(updated.UpdatedAt, read.UpdatedAt);
    }

    [Fact]
    public async Task Protected_resource_metadata_points_clients_at_our_own_authorization_server()
    {
        using var client = _factory.CreateClient();

        using var metadata = await GetJsonAsync(client, "/.well-known/oauth-protected-resource/mcp");

        var root = metadata.RootElement;
        var issuer = root.GetProperty("authorization_servers")[0].GetString();
        Assert.EndsWith("/mcp", root.GetProperty("resource").GetString());
        Assert.Equal(client.BaseAddress!.ToString().TrimEnd('/'), issuer);
        Assert.EndsWith("/mcp_access", root.GetProperty("scopes_supported")[0].GetString());
    }

    [Fact]
    public async Task Authorization_server_metadata_advertises_dcr_and_pkce()
    {
        using var client = _factory.CreateClient();

        foreach (var path in new[]
                 {
                     "/.well-known/oauth-authorization-server",
                     "/.well-known/openid-configuration"
                 })
        {
            using var metadata = await GetJsonAsync(client, path);
            var root = metadata.RootElement;
            var baseUrl = client.BaseAddress!.ToString().TrimEnd('/');

            Assert.Equal(baseUrl, root.GetProperty("issuer").GetString());
            Assert.Equal($"{baseUrl}/oauth/authorize", root.GetProperty("authorization_endpoint").GetString());
            Assert.Equal($"{baseUrl}/oauth/token", root.GetProperty("token_endpoint").GetString());
            Assert.Equal($"{baseUrl}/oauth/register", root.GetProperty("registration_endpoint").GetString());
            Assert.Equal("S256", root.GetProperty("code_challenge_methods_supported")[0].GetString());
            Assert.Equal("none", root.GetProperty("token_endpoint_auth_methods_supported")[0].GetString());
        }
    }

    [Fact]
    public async Task Registration_hands_every_client_the_same_public_entra_client_id()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/oauth/register",
            new { redirect_uris = new[] { "http://localhost:8080/callback" }, client_name = "Claude Code" },
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(EntraClientId(), body.RootElement.GetProperty("client_id").GetString());
        Assert.Equal("none", body.RootElement.GetProperty("token_endpoint_auth_method").GetString());
        Assert.Equal(
            "http://localhost:8080/callback",
            body.RootElement.GetProperty("redirect_uris")[0].GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"redirect_uris":[]}""")]
    [InlineData("""{"redirect_uris":["not a uri"]}""")]
    [InlineData("""{"redirect_uris":["http://localhost/cb#frag"]}""")]
    [InlineData("""{"redirect_uris":[1]}""")]
    [InlineData("[1]")]
    public async Task Registration_rejects_invalid_redirect_uris(string json)
    {
        using var client = _factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/oauth/register", content, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Authorize_redirects_to_entra_with_fixed_scopes_and_without_resource()
    {
        using var client = NoRedirectClient();
        var query = AuthorizeQuery(
            ("scope", "https://graph.microsoft.com/.default"),
            ("resource", "http://localhost/mcp"),
            ("state", "xyz"));

        var response = await client.GetAsync($"/oauth/authorize?{query}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("login.microsoftonline.com", location.Host);
        Assert.EndsWith("/oauth2/v2.0/authorize", location.AbsolutePath);
        var forwarded = System.Web.HttpUtility.ParseQueryString(location.Query);
        Assert.Equal(EntraClientId(), forwarded["client_id"]);
        Assert.Equal("code", forwarded["response_type"]);
        Assert.Equal("http://localhost:8080/callback", forwarded["redirect_uri"]);
        Assert.Equal("xyz", forwarded["state"]);
        Assert.Equal("challenge", forwarded["code_challenge"]);
        Assert.Equal("S256", forwarded["code_challenge_method"]);
        Assert.Null(forwarded["resource"]);
        Assert.Contains("/mcp_access", forwarded["scope"]);
        Assert.DoesNotContain("access_as_user", forwarded["scope"]);
        Assert.Contains("offline_access", forwarded["scope"]);
        Assert.DoesNotContain("graph.microsoft.com", forwarded["scope"]);
    }

    [Theory]
    [InlineData("client_id", "someone-else", "invalid_client")]
    [InlineData("response_type", "token", "unsupported_response_type")]
    [InlineData("code_challenge", "", "invalid_request")]
    [InlineData("code_challenge_method", "plain", "invalid_request")]
    [InlineData("redirect_uri", "", "invalid_request")]
    public async Task Authorize_rejects_unsafe_requests(string field, string value, string error)
    {
        using var client = NoRedirectClient();

        var response = await client.GetAsync(
            $"/oauth/authorize?{AuthorizeQuery((field, value))}",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(error, body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Token_endpoint_forwards_only_allowed_fields_to_entra()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = "the-code",
                ["code_verifier"] = "the-verifier",
                ["redirect_uri"] = "http://localhost:8080/callback",
                ["client_id"] = EntraClientId(),
                ["resource"] = "http://localhost/mcp",
                ["scope"] = "https://graph.microsoft.com/.default",
                ["client_secret"] = "nope"
            }),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("access_token", await response.Content.ReadAsStringAsync());
        var stub = _factory.EntraStub;
        Assert.EndsWith("/oauth2/v2.0/token", stub.LastUri!.AbsolutePath);
        Assert.Equal(
            ["client_id", "code", "code_verifier", "grant_type", "redirect_uri", "scope"],
            stub.LastForm.Keys.Order());
        Assert.Equal("the-code", stub.LastForm["code"]);
        Assert.Contains("/mcp_access", stub.LastForm["scope"]);
        Assert.DoesNotContain("access_as_user", stub.LastForm["scope"]);
        Assert.DoesNotContain("graph.microsoft.com", stub.LastForm["scope"]);
    }

    [Fact]
    public async Task Token_endpoint_supports_refresh_and_passes_entra_errors_through()
    {
        using var client = _factory.CreateClient();
        _factory.EntraStub.Status = HttpStatusCode.BadRequest;
        _factory.EntraStub.Body = """{"error":"invalid_grant"}""";

        try
        {
            var response = await client.PostAsync(
                "/oauth/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = "old"
                }),
                CancellationToken.None);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("invalid_grant", await response.Content.ReadAsStringAsync());
            Assert.Equal("old", _factory.EntraStub.LastForm["refresh_token"]);
        }
        finally
        {
            _factory.EntraStub.Status = HttpStatusCode.OK;
            _factory.EntraStub.Body = """{"access_token":"a","refresh_token":"r","expires_in":3600}""";
        }
    }

    [Theory]
    [InlineData("client_credentials", "unsupported_grant_type")]
    [InlineData("password", "unsupported_grant_type")]
    [InlineData("authorization_code", "invalid_request")]
    public async Task Token_endpoint_rejects_other_grants_and_incomplete_requests(
        string grantType,
        string error)
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync(
            "/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = grantType }),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(error, body.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Changes_are_attributed_to_the_client_that_made_them()
    {
        var notebook = await CreateNotebookAsync("attribution");
        using var http = _factory.CreateAuthenticatedClient(userId: "attribution");
        var human = await http.PostAsJsonAsync(
            "/api/notes",
            new CreateNoteRequest("By hand", "text", InkdropLite.Api.Models.NoteStatus.None, notebook.Id),
            CancellationToken.None);
        var humanNote = (await human.Content.ReadFromJsonAsync<NoteResponse>(CancellationToken.None))!;
        Assert.Equal("app", humanNote.CreatedSource);
        Assert.Equal("app", humanNote.UpdatedSource);

        await using var mcp = await ConnectAsync("attribution", "claude-code");
        var agentNote = await CallAsync<NoteResponse>(mcp, "create_note", new()
        {
            ["title"] = "By agent",
            ["notebookId"] = notebook.Id
        });
        Assert.Equal("mcp:claude-code/1.2", agentNote.CreatedSource);

        var edited = await CallAsync<NoteResponse>(mcp, "update_note", new()
        {
            ["id"] = humanNote.Id,
            ["content"] = "edited"
        });
        Assert.Equal("app", edited.CreatedSource);
        Assert.Equal("mcp:claude-code/1.2", edited.UpdatedSource);

        var backByHand = await http.PutAsJsonAsync(
            $"/api/notes/{humanNote.Id}",
            new UpdateNoteRequest("By hand", "final", InkdropLite.Api.Models.NoteStatus.None, notebook.Id),
            CancellationToken.None);
        Assert.Equal(HttpStatusCode.NoContent, backByHand.StatusCode);
        var final = (await http.GetFromJsonAsync<NoteResponse>(
            $"/api/notes/{humanNote.Id}",
            CancellationToken.None))!;
        Assert.Equal("app", final.UpdatedSource);
    }

    [Fact]
    public async Task Large_notes_are_paged_and_sections_can_be_read_on_their_own()
    {
        await using var mcp = await ConnectAsync("big-notes");
        var notebook = await CreateNotebookAsync("big-notes");
        var content = "# Guide\n" +
            string.Concat(Enumerable.Range(0, 3_000).Select(i => $"filler line {i}\n")) +
            "## Deploy\nrun deploy.sh\n## Rollback\nrun rollback.sh\n";
        var note = await CallAsync<NoteResponse>(mcp, "create_note", new()
        {
            ["title"] = "Runbook",
            ["notebookId"] = notebook.Id,
            ["content"] = content
        });

        var summary = await CallAsync<List<NoteSummaryResponse>>(mcp, "search_notes", new()
        {
            ["query"] = "Runbook"
        });
        Assert.Equal(content.Length, summary.Single(item => item.Id == note.Id).ContentLength);

        using var page = JsonDocument.Parse(Text(await mcp.CallToolAsync(
            "get_note",
            new Dictionary<string, object?> { ["id"] = note.Id },
            cancellationToken: CancellationToken.None)));
        Assert.True(page.RootElement.GetProperty("hasMore").GetBoolean());
        Assert.True(page.RootElement.GetProperty("content").GetString()!.Length <= 12_000);
        Assert.Equal(3, page.RootElement.GetProperty("outline").GetArrayLength());

        using var section = JsonDocument.Parse(Text(await mcp.CallToolAsync(
            "get_note",
            new Dictionary<string, object?> { ["id"] = note.Id, ["section"] = "deploy" },
            cancellationToken: CancellationToken.None)));
        Assert.False(section.RootElement.GetProperty("hasMore").GetBoolean());
        Assert.Equal("## Deploy\nrun deploy.sh\n", section.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public async Task Search_treats_like_wildcards_literally()
    {
        await using var mcp = await ConnectAsync("wildcards");
        var notebook = await CreateNotebookAsync("wildcards");
        await CallAsync<NoteResponse>(mcp, "create_note", new()
        {
            ["title"] = "Plain",
            ["notebookId"] = notebook.Id,
            ["content"] = "nothing special"
        });

        var found = await CallAsync<List<NoteSummaryResponse>>(mcp, "search_notes", new()
        {
            ["query"] = "%"
        });

        Assert.Empty(found);
    }

    [Fact]
    public async Task Tool_errors_are_reported_to_the_agent()
    {
        await using var mcp = await ConnectAsync("errors");

        var unknownNotebook = await mcp.CallToolAsync(
            "create_note",
            new Dictionary<string, object?>
            {
                ["title"] = "Orphan",
                ["notebookId"] = Guid.NewGuid()
            },
            cancellationToken: CancellationToken.None);
        var tooLong = await mcp.CallToolAsync(
            "create_note",
            new Dictionary<string, object?>
            {
                ["title"] = new string('x', 257),
                ["notebookId"] = Guid.NewGuid()
            },
            cancellationToken: CancellationToken.None);
        var missing = await mcp.CallToolAsync(
            "get_note",
            new Dictionary<string, object?> { ["id"] = Guid.NewGuid() },
            cancellationToken: CancellationToken.None);

        Assert.True(unknownNotebook.IsError);
        Assert.Contains("notebook", Text(unknownNotebook), StringComparison.OrdinalIgnoreCase);
        Assert.True(tooLong.IsError);
        Assert.True(missing.IsError);
    }

    [Fact]
    public async Task Agents_only_see_the_data_of_the_user_they_act_for()
    {
        var aliceNotebook = await CreateNotebookAsync("alice-iso");
        await using var alice = await ConnectAsync("alice-iso");
        await using var bob = await ConnectAsync("bob-iso");
        var marker = $"secret{Guid.NewGuid():N}";

        var note = await CallAsync<NoteResponse>(alice, "create_note", new()
        {
            ["title"] = "Alice only",
            ["notebookId"] = aliceNotebook.Id,
            ["content"] = marker
        });

        Assert.Empty(await CallAsync<List<NoteSummaryResponse>>(bob, "search_notes", new()
        {
            ["query"] = marker
        }));
        Assert.True((await bob.CallToolAsync(
            "get_note",
            new Dictionary<string, object?> { ["id"] = note.Id },
            cancellationToken: CancellationToken.None)).IsError);
        Assert.True((await bob.CallToolAsync(
            "update_note",
            new Dictionary<string, object?> { ["id"] = note.Id, ["title"] = "hijacked" },
            cancellationToken: CancellationToken.None)).IsError);
        Assert.DoesNotContain(
            await CallAsync<List<NotebookResponse>>(bob, "list_notebooks", []),
            notebook => notebook.Id == aliceNotebook.Id);
    }

    private string EntraClientId() =>
        _factory.Services.GetRequiredService<McpOAuthOptions>().ClientId;

    private HttpClient NoRedirectClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private string AuthorizeQuery(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string>
        {
            ["client_id"] = EntraClientId(),
            ["response_type"] = "code",
            ["redirect_uri"] = "http://localhost:8080/callback",
            ["code_challenge"] = "challenge",
            ["code_challenge_method"] = "S256"
        };

        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }

        return string.Join("&", values.Select(pair =>
            $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(CancellationToken.None),
            cancellationToken: CancellationToken.None);
    }

    private async Task<McpClient> ConnectAsync(string userId, string clientName = "test-agent")
    {
        var http = _factory.CreateAuthenticatedClient(InkdropWebApplicationFactory.McpScope, userId);
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri("http://localhost/mcp") },
            http,
            ownsHttpClient: true);

        return await McpClient.CreateAsync(
            transport,
            new McpClientOptions
            {
                ClientInfo = new Implementation { Name = clientName, Version = "1.2" }
            },
            cancellationToken: CancellationToken.None);
    }

    private async Task<NotebookResponse> CreateNotebookAsync(string userId)
    {
        using var client = _factory.CreateAuthenticatedClient(userId: userId);
        var response = await client.PostAsJsonAsync(
            "/api/notebooks",
            new CreateNotebookRequest($"Book-{Guid.NewGuid():N}"[..20]),
            CancellationToken.None);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NotebookResponse>(CancellationToken.None))!;
    }

    private static async Task<T> CallAsync<T>(
        McpClient mcp,
        string tool,
        Dictionary<string, object?> arguments)
    {
        var result = await mcp.CallToolAsync(
            tool,
            arguments,
            cancellationToken: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        return JsonSerializer.Deserialize<T>(Text(result), Json)!;
    }

    private static string Text(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
