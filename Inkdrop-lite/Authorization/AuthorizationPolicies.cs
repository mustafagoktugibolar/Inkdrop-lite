namespace Inkdrop_lite.Authorization;

public static class AuthorizationPolicies
{
    public const string ApiScope = "ApiScope";

    /// <summary>Guards /mcp. Requires the MCP scope when one is configured, otherwise the API scope.</summary>
    public const string McpScope = "McpScope";
}
