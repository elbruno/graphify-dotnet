using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Export;

/// <summary>
/// Tests for Neo4jExporter: Cypher CREATE statement generation, label/property escaping,
/// relationship types, empty graph handling, and file output.
/// </summary>
[Trait("Category", "Export")]
public sealed class Neo4jExporterTests : IDisposable
{
    private readonly string _testRoot;
    private readonly Neo4jExporter _exporter = new();

    public Neo4jExporterTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, recursive: true);
        }
        catch { }
    }

    [Fact]
    public void Format_ReturnsNeo4j()
    {
        Assert.Equal("neo4j", _exporter.Format);
    }

    [Fact]
    public async Task ExportAsync_ValidGraph_ProducesValidCypher()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "graph.cypher");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("CREATE (", content);
        Assert.Contains("// Knowledge Graph Export to Neo4j", content);
    }

    [Fact]
    public async Task ExportAsync_NodesExportedWithCorrectLabelsAndProperties()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode
        {
            Id = "MyClass",
            Label = "MyClass",
            Type = "Class",
            Metadata = new Dictionary<string, string> { ["source_file"] = "file.cs" }
        });

        var path = Path.Combine(_testRoot, "nodes.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains(":Class", content);
        Assert.Contains("id: \"MyClass\"", content);
        Assert.Contains("label: \"MyClass\"", content);
        Assert.Contains("source_file: \"file.cs\"", content);
    }

    [Fact]
    public async Task ExportAsync_EdgesExportedWithCorrectRelationshipTypes()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "edges.cypher");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains(":CALLS", content);
        Assert.Contains("weight:", content);
        Assert.Contains("confidence:", content);
    }

    [Fact]
    public async Task ExportAsync_EmptyGraph_ProducesValidButEmptyOutput()
    {
        var graph = new KnowledgeGraph();
        var path = Path.Combine(_testRoot, "empty.cypher");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("// Knowledge Graph Export to Neo4j", content);
        Assert.Contains("// Nodes: 0, Edges: 0", content);
        Assert.DoesNotContain("CREATE (n", content);
    }

    [Fact]
    public async Task ExportAsync_SpecialCharactersInLabels_AreEscaped()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode
        {
            Id = "node\"with\"quotes",
            Label = "Label with \"quotes\" and \\backslashes",
            Type = "Entity"
        });

        var path = Path.Combine(_testRoot, "special.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("\\\"", content);
        Assert.Contains("\\\\", content);
    }

    [Fact]
    public async Task ExportAsync_FileOutputWritesToDisk()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "output.cypher");

        Assert.False(File.Exists(path));

        await _exporter.ExportAsync(graph, path);

        Assert.True(File.Exists(path));
        var info = new FileInfo(path);
        Assert.True(info.Length > 0);
    }

    [Fact]
    public async Task ExportAsync_NodeWithCommunity_IncludesCommunityProperty()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode { Id = "A", Label = "A", Type = "Class", Community = 3 });

        var path = Path.Combine(_testRoot, "community.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("community: 3", content);
    }

    [Fact]
    public async Task ExportAsync_CreatesIndexStatements()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "indexes.cypher");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("CREATE INDEX IF NOT EXISTS", content);
    }

    [Fact]
    public async Task ExportAsync_NullGraph_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _exporter.ExportAsync(null!, "test.cypher"));
    }

    [Fact]
    public async Task ExportAsync_EmptyOutputPath_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _exporter.ExportAsync(new KnowledgeGraph(), ""));
    }

    [Fact]
    public async Task ExportAsync_EmptyNodeType_SanitizedToNode()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode { Id = "x", Label = "x", Type = "" });

        var path = Path.Combine(_testRoot, "notype.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains(":Node", content);
    }

    private static KnowledgeGraph CreateSampleGraph()
    {
        var graph = new KnowledgeGraph();
        var n1 = new GraphNode { Id = "ClassA", Label = "ClassA", Type = "Class" };
        var n2 = new GraphNode { Id = "MethodB", Label = "MethodB", Type = "Method" };
        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddEdge(new GraphEdge { Source = n1, Target = n2, Relationship = "calls" });
        return graph;
    }
}
