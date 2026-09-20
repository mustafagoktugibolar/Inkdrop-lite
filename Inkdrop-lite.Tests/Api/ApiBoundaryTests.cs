using System.Net;
using Inkdrop_lite.Tests.Infrastructure;

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
}
