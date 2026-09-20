using System.Net;
using Inkdrop_lite.Tests.Infrastructure;

namespace Inkdrop_lite.Tests.Api;

// Own fixture on purpose: this test exhausts the OAuth budget of its (in-process) client address.
public sealed class OAuthRateLimitTests : IClassFixture<InkdropWebApplicationFactory>
{
    private readonly InkdropWebApplicationFactory _factory;

    public OAuthRateLimitTests(InkdropWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Anonymous_oauth_flood_is_limited_without_starving_signed_in_users()
    {
        using var anonymous = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 70; i++)
        {
            var response = await anonymous.GetAsync(
                "/.well-known/oauth-authorization-server",
                CancellationToken.None);
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        Assert.Equal(HttpStatusCode.OK, statuses[0]);

        using var signedIn = _factory.CreateAuthenticatedClient();
        var api = await signedIn.GetAsync("/api/notes", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, api.StatusCode);
    }
}
