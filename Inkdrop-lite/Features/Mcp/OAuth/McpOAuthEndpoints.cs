using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Inkdrop_lite.Features.Mcp.OAuth;

/// <summary>
/// A deliberately thin OAuth authorization-server facade in front of Entra ID.
///
/// Why it exists: MCP clients expect Dynamic Client Registration and send an RFC 8707 <c>resource</c>
/// parameter; Entra supports neither in a way that works for a plain API registration. This facade
/// advertises a standard authorization server, hands every client the same public Entra client id,
/// and forwards authorize/token requests to Entra with only the parameters it allows.
///
/// What it is not: it issues no tokens, stores no state and holds no secrets. Entra authenticates the
/// user, validates the redirect URI against the app registration, and mints the token; the API
/// validates that token exactly like any other. Do not add behavior here that widens what a
/// client may ask Entra for (scopes, client ids, grant types).
/// </summary>
public static class McpOAuthEndpoints
{
    public const string EntraHttpClient = "entra-oauth";
    public const string RateLimitPolicy = "oauth";

    private const int MaxBodyBytes = 16 * 1024;
    private static readonly string[] GrantTypes = ["authorization_code", "refresh_token"];

    public static IEndpointRouteBuilder MapMcpOAuth(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(string.Empty)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy);

        group.MapGet("/.well-known/oauth-authorization-server", Metadata);
        group.MapGet("/.well-known/openid-configuration", Metadata);
        group.MapPost("/oauth/register", Register);
        group.MapGet("/oauth/authorize", Authorize);
        group.MapPost("/oauth/token", Token);

        return app;
    }

    private static IResult Metadata(HttpRequest request, McpOAuthOptions options)
    {
        var baseUrl = options.BaseUrl(request);

        return Results.Json(new Dictionary<string, object>
        {
            ["issuer"] = baseUrl,
            ["authorization_endpoint"] = $"{baseUrl}/oauth/authorize",
            ["token_endpoint"] = $"{baseUrl}/oauth/token",
            ["registration_endpoint"] = $"{baseUrl}/oauth/register",
            ["response_types_supported"] = new[] { "code" },
            ["grant_types_supported"] = GrantTypes,
            ["code_challenge_methods_supported"] = new[] { "S256" },
            ["token_endpoint_auth_methods_supported"] = new[] { "none" },
            ["scopes_supported"] = options.Scopes
        });
    }

    // RFC 7591. There is nothing to register: every client is the same public Entra client.
    // Redirect URIs are only echoed back; Entra enforces them against the app registration.
    private static async Task<IResult> Register(HttpRequest request, McpOAuthOptions options)
    {
        if (request.ContentLength is null or > MaxBodyBytes)
        {
            return OAuthError("invalid_client_metadata", "Request body is missing or too large.");
        }

        JsonElement body;

        try
        {
            body = await request.ReadFromJsonAsync<JsonElement>();
        }
        catch (JsonException)
        {
            return OAuthError("invalid_client_metadata", "Body must be a JSON object.");
        }

        if (body.ValueKind != JsonValueKind.Object ||
            !body.TryGetProperty("redirect_uris", out var uris) ||
            uris.ValueKind != JsonValueKind.Array ||
            uris.GetArrayLength() is 0 or > 10)
        {
            return OAuthError("invalid_redirect_uri", "redirect_uris must be an array of 1-10 URIs.");
        }

        var redirectUris = new List<string>();

        foreach (var element in uris.EnumerateArray())
        {
            var value = element.ValueKind == JsonValueKind.String ? element.GetString() : null;

            if (value is not { Length: <= 2048 } ||
                !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                return OAuthError("invalid_redirect_uri", "Each redirect URI must be an absolute URI without a fragment.");
            }

            redirectUris.Add(value);
        }

        var response = new Dictionary<string, object>
        {
            ["client_id"] = options.ClientId,
            ["client_id_issued_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["redirect_uris"] = redirectUris,
            ["token_endpoint_auth_method"] = "none",
            ["grant_types"] = GrantTypes,
            ["response_types"] = new[] { "code" }
        };

        if (body.TryGetProperty("client_name", out var name) &&
            name.ValueKind == JsonValueKind.String &&
            name.GetString() is { Length: > 0 and <= 200 } clientName)
        {
            response["client_name"] = clientName;
        }

        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }

    // Sends the browser to Entra with a sanitized query: fixed client id and scopes, PKCE required,
    // and no `resource` parameter. The destination host is fixed, so this is not an open redirect.
    private static IResult Authorize(HttpRequest request, McpOAuthOptions options)
    {
        var query = request.Query;
        var redirectUri = query["redirect_uri"].ToString();
        var challenge = query["code_challenge"].ToString();

        if (query["client_id"] != options.ClientId)
        {
            return OAuthError("invalid_client", "Unknown client_id.");
        }

        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out _))
        {
            return OAuthError("invalid_request", "redirect_uri is required.");
        }

        if (query["response_type"] != "code")
        {
            return OAuthError("unsupported_response_type", "Only response_type=code is supported.");
        }

        if (challenge.Length == 0 || query["code_challenge_method"] != "S256")
        {
            return OAuthError("invalid_request", "PKCE with code_challenge_method=S256 is required.");
        }

        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = options.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = options.ScopeString,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256"
        };

        if (query["state"].ToString() is { Length: > 0 } state)
        {
            parameters["state"] = state;
        }

        return Results.Redirect(QueryHelpers.AddQueryString(options.AuthorizeEndpoint, parameters));
    }

    // Forwards only the fields Entra needs. Server-side so `resource` (and anything else) can be dropped.
    private static async Task<IResult> Token(
        HttpRequest request,
        McpOAuthOptions options,
        IHttpClientFactory httpClientFactory,
        ILogger<McpOAuthOptions> logger,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType || request.ContentLength is null or > MaxBodyBytes)
        {
            return OAuthError("invalid_request", "Send a small application/x-www-form-urlencoded body.");
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var grantType = form["grant_type"].ToString();

        if (!GrantTypes.Contains(grantType, StringComparer.Ordinal))
        {
            return OAuthError("unsupported_grant_type", "Only authorization_code and refresh_token are supported.");
        }

        if (form.TryGetValue("client_id", out var clientId) && clientId != options.ClientId)
        {
            return OAuthError("invalid_client", "Unknown client_id.");
        }

        var forward = new Dictionary<string, string>
        {
            ["grant_type"] = grantType,
            ["client_id"] = options.ClientId,
            ["scope"] = options.ScopeString
        };

        var required = grantType == "authorization_code"
            ? new[] { "code", "code_verifier", "redirect_uri" }
            : ["refresh_token"];

        foreach (var field in required)
        {
            if (form[field].ToString() is not { Length: > 0 } value)
            {
                return OAuthError("invalid_request", $"{field} is required.");
            }

            forward[field] = value;
        }

        try
        {
            var client = httpClientFactory.CreateClient(EntraHttpClient);
            using var upstream = await client.PostAsync(
                options.TokenEndpoint,
                new FormUrlEncodedContent(forward),
                cancellationToken);
            var payload = await upstream.Content.ReadAsStringAsync(cancellationToken);

            // Never log the payload or the form: they carry codes, verifiers and tokens.
            logger.LogInformation(
                "Entra token endpoint answered {StatusCode} for grant {GrantType}",
                (int)upstream.StatusCode,
                grantType);

            return new NoStoreJsonResult((int)upstream.StatusCode, payload);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                              && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Entra token endpoint could not be reached: {Reason}", exception.GetType().Name);
            return OAuthError("server_error", "The identity provider could not be reached.", StatusCodes.Status502BadGateway);
        }
    }

    private static IResult OAuthError(
        string error,
        string description,
        int statusCode = StatusCodes.Status400BadRequest) =>
        Results.Json(
            new Dictionary<string, string> { ["error"] = error, ["error_description"] = description },
            statusCode: statusCode);

    private sealed class NoStoreJsonResult(int statusCode, string json) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = new MediaTypeHeaderValue("application/json").ToString();
            httpContext.Response.Headers.CacheControl = "no-store";
            httpContext.Response.Headers.Pragma = "no-cache";
            await httpContext.Response.WriteAsync(json);
        }
    }
}
