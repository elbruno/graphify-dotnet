using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using System.Text.Json;
using Graphify.Graph;
using Graphify.Models;
using Graphify.Mcp;

// Parse command line arguments
var graphPath = args.Length > 0 ? args[0] : "graph.json";
var verbose = args.Contains("--verbose") || args.Contains("-v");

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr (MCP requirement: stdout is reserved for JSON-RPC)
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = verbose ? LogLevel.Trace : LogLevel.Warning;
});

// Load the knowledge graph from JSON
KnowledgeGraph graph;
try
{
    if (!File.Exists(graphPath))
    {
        Console.Error.WriteLine($"Error: Graph file not found at '{graphPath}'");
        Console.Error.WriteLine("Usage: Graphify.Mcp <path-to-graph.json> [--verbose]");
        return 1;
    }

    var json = await File.ReadAllTextAsync(graphPath);
    var graphData = JsonSerializer.Deserialize<GraphJsonData>(json);
    
    if (graphData == null)
    {
        Console.Error.WriteLine($"Error: Failed to parse graph JSON from '{graphPath}'");
        return 1;
    }

    graph = new KnowledgeGraph();
    
    // Add all nodes
    foreach (var node in graphData.Nodes)
    {
        graph.AddNode(node);
    }
    
    // Add all edges
    foreach (var edge in graphData.Edges)
    {
        graph.AddEdge(edge);
    }

    if (verbose)
    {
        Console.Error.WriteLine($"Loaded graph: {graph.NodeCount} nodes, {graph.EdgeCount} edges");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error loading graph: {ex.Message}");
    return 1;
}

// Register the graph as a singleton
builder.Services.AddSingleton(graph);

// Register GraphTools
builder.Services.AddSingleton<GraphTools>();

// Configure MCP server with stdio transport
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

if (verbose)
{
    Console.Error.WriteLine("Graphify MCP Server starting...");
}

await app.RunAsync();

return 0;

/// <summary>
/// JSON deserialization model for graph data.
/// </summary>
file record GraphJsonData
{
    public List<GraphNode> Nodes { get; init; } = new();
    public List<GraphEdge> Edges { get; init; } = new();
}
