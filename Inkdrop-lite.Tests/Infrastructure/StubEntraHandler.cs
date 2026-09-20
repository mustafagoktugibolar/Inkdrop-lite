using System.Net;
using System.Text;

namespace Inkdrop_lite.Tests.Infrastructure;

/// <summary>Stands in for Entra's token endpoint and records what the OAuth facade forwarded.</summary>
public sealed class StubEntraHandler : HttpMessageHandler
{
    public Uri? LastUri { get; private set; }

    public Dictionary<string, string> LastForm { get; private set; } = [];

    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    public string Body { get; set; } = """{"access_token":"a","refresh_token":"r","expires_in":3600}""";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastUri = request.RequestUri;
        LastForm = (await request.Content!.ReadAsStringAsync(cancellationToken))
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')));

        return new HttpResponseMessage(Status)
        {
            Content = new StringContent(Body, Encoding.UTF8, "application/json")
        };
    }
}
