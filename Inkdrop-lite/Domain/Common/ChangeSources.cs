namespace Inkdrop_lite.Domain.Common;

/// <summary>Who or what performed a change: the web app, or an MCP agent (<c>mcp:{client}</c>).</summary>
public static class ChangeSources
{
    public const string App = "app";
    public const string Mcp = "mcp";
    public const int MaxLength = 128;
}
