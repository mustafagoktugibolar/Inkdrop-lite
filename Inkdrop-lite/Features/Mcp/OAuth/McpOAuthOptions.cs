namespace Inkdrop_lite.Features.Mcp.OAuth;

/// <summary>
/// Settings for the OAuth facade that lets MCP clients sign in with Entra without being told a client id.
/// </summary>
public sealed class McpOAuthOptions
{
    /// <summary>The Entra public-client app registration every MCP client signs in as.</summary>
    public required string ClientId { get; init; }

    public required string AuthorizeEndpoint { get; init; }

    public required string TokenEndpoint { get; init; }

    /// <summary>API scopes (fully qualified) plus offline_access. Always used; client-requested scopes are ignored.</summary>
    public required IReadOnlyList<string> Scopes { get; init; }

    /// <summary>Externally visible base URL (e.g. behind a proxy). When null it is derived from the request.</summary>
    public string? PublicUrl { get; init; }

    public string ScopeString => string.Join(' ', Scopes);

    public string BaseUrl(HttpRequest request) =>
        (PublicUrl ?? $"{request.Scheme}://{request.Host}").TrimEnd('/');
}
