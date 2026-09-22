using Inkdrop_lite.Features.Mcp.OAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inkdrop_lite.Tests.Infrastructure;

public sealed class InkdropWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"inkdrop-tests-{Guid.NewGuid():N}.db");

    public const string McpScope = "mcp_access";

    public const int AttachmentMaxBytes = 1024;

    public string AttachmentRoot { get; } = Path.Combine(
        Path.GetTempPath(),
        $"inkdrop-tests-attachments-{Guid.NewGuid():N}");

    public StubEntraHandler EntraStub { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Program.cs reads the connection string while building the host, before
        // ConfigureAppConfiguration callbacks run, so it must be a host setting.
        builder.UseSetting("ConnectionStrings:Inkdrop-lite", $"Data Source={_databasePath}");
        builder.UseSetting("AzureAd:McpScope", McpScope);
        builder.UseSetting("Attachments:Root", AttachmentRoot);
        builder.UseSetting("Attachments:MaxBytes", AttachmentMaxBytes.ToString());

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureAd:Scopes"] = "access_as_user",
                ["Cors:AllowedOrigins:0"] = "http://localhost:52364"
            });
        });

        builder.ConfigureServices(services =>
        {
            services
                .AddHttpClient(McpOAuthEndpoints.EntraHttpClient)
                .ConfigurePrimaryHttpMessageHandler(() => EntraStub);

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }

    public HttpClient CreateAuthenticatedClient(
        string? scope = "access_as_user",
        string userId = "test-user")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.UserHeader, userId);

        if (scope is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ScopeHeader, scope);
        }

        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        DeleteIfExists(_databasePath);
        DeleteIfExists($"{_databasePath}-shm");
        DeleteIfExists($"{_databasePath}-wal");

        if (Directory.Exists(AttachmentRoot))
        {
            Directory.Delete(AttachmentRoot, recursive: true);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
