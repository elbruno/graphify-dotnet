namespace Graphify.Mcp;

/// <summary>
/// Configuration options for the MCP server.
/// </summary>
public sealed record McpServerOptions
{
    /// <summary>
    /// Path to the pre-built knowledge graph JSON file.
    /// </summary>
    public string GraphPath { get; init; } = "graph.json";

    /// <summary>
    /// Enable verbose logging to stderr.
    /// </summary>
    public bool Verbose { get; init; }
}
