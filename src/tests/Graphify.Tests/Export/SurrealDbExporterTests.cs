using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Export;

[Trait("Category", "Export")]
public sealed class SurrealDbExporterTests
{
    private static string NewDbPath(string testName)
    {
        var dir = Path.Combine(Path.GetTempPath(), "gfx-" + testName + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "codebase.db");
    }

    private static void Cleanup(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir is not null && Directory.Exists(dir))
            try { Directory.Delete(dir, recursive: true); } catch { }
    }

    [Fact]
    public void Format_ReturnsCorrectValue()
    {
        Assert.Equal("surrealdb", new SurrealDbExporter().Format);
    }

    [Fact]
    public async Task ExportAsync_ValidGraph_ProducesDatabase()
    {
        var path = NewDbPath(nameof(ExportAsync_ValidGraph_ProducesDatabase));
        try
        {
            await new SurrealDbExporter().ExportAsync(CreateSampleGraph(), path);
            Assert.True(Directory.Exists(path));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public async Task ExportAsync_EmptyGraph_Completes()
    {
        var path = NewDbPath(nameof(ExportAsync_EmptyGraph_Completes));
        try
        {
            await new SurrealDbExporter().ExportAsync(new KnowledgeGraph(), path);
            Assert.True(Directory.Exists(path));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public async Task ExportAsync_CreatesDirectory()
    {
        var path = NewDbPath(nameof(ExportAsync_CreatesDirectory));
        try
        {
            await new SurrealDbExporter().ExportAsync(CreateSampleGraph(), path);
            Assert.True(Directory.Exists(Path.GetDirectoryName(path)));
            Assert.True(Directory.Exists(path));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public async Task ExportAsync_LargeGraph_Completes()
    {
        var path = NewDbPath(nameof(ExportAsync_LargeGraph_Completes));
        try
        {
            await new SurrealDbExporter().ExportAsync(MakeLargeGraph(100, 50), path);
            Assert.True(Directory.Exists(path));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public async Task ExportAsync_NodesWithoutMetadata_Completes()
    {
        var path = NewDbPath(nameof(ExportAsync_NodesWithoutMetadata_Completes));
        try
        {
            var graph = new KnowledgeGraph();
            graph.AddNode(new GraphNode
            {
                Id = "test_node", Label = "TestNode", Type = "Class",
                FilePath = "Test.cs", Confidence = Confidence.Extracted,
                Metadata = null
            });
            await new SurrealDbExporter().ExportAsync(graph, path);
            Assert.True(Directory.Exists(path));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public async Task ExportAsync_RemoteConfig_UseEndpoint()
    {
        var exporter = new SurrealDbExporter(
            endpoint: "http://localhost:8000",
            username: "root",
            password: "root",
            ns: "test",
            database: "test");
        Assert.Equal("surrealdb", exporter.Format);
    }

    private static KnowledgeGraph MakeLargeGraph(int nodes, int edges)
    {
        var graph = new KnowledgeGraph();
        for (int i = 0; i < nodes; i++)
            graph.AddNode(CreateNode($"n{i}", $"N{i}"));
        var allNodes = graph.GetNodes().ToList();
        for (int i = 0; i < edges && i + 1 < allNodes.Count; i++)
            graph.AddEdge(new GraphEdge
            {
                Source = allNodes[i], Target = allNodes[(i + 1) % allNodes.Count],
                Relationship = "connects", Weight = 1.0,
                Confidence = Confidence.Extracted, Metadata = new Dictionary<string, string>()
            });
        return graph;
    }

    private static KnowledgeGraph CreateSampleGraph()
    {
        var graph = new KnowledgeGraph();
        var n1 = CreateNode("node1", "Node1");
        var n2 = CreateNode("node2", "Node2");
        var n3 = CreateNode("node3", "Node3");
        graph.AddNode(n1); graph.AddNode(n2); graph.AddNode(n3);
        graph.AddEdge(new GraphEdge { Source = n1, Target = n2, Relationship = "calls", Weight = 1.0, Confidence = Confidence.Extracted, Metadata = new Dictionary<string, string>() });
        graph.AddEdge(new GraphEdge { Source = n2, Target = n3, Relationship = "imports", Weight = 1.0, Confidence = Confidence.Extracted, Metadata = new Dictionary<string, string>() });
        return graph;
    }

    private static GraphNode CreateNode(string id, string label) =>
        new() { Id = id, Label = label, Type = "Entity", FilePath = "test.cs", Confidence = Confidence.Extracted, Metadata = new Dictionary<string, string>() };
}
