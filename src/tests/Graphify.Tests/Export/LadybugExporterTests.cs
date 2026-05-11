using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Export;

/// <summary>
/// Tests for LadybugExporter: Ladybug Cypher DDL generation, MAP property encoding,
/// relationship table syntax, MATCH-based edge creation, empty graph handling, and file output.
/// </summary>
[Trait("Category", "Export")]
public sealed class LadybugExporterTests : IDisposable
{
    private readonly string _testRoot;
    private readonly LadybugExporter _exporter = new();

    public LadybugExporterTests()
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
    public void Format_ReturnsLadybug()
    {
        Assert.Equal("ladybug", _exporter.Format);
    }

    [Fact]
    public async Task ExportAsync_ValidGraph_ProducesValidLadybugCypher()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "graph.ladybug.cypher");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("CREATE NODE TABLE GraphNode", content);
        Assert.Contains("CREATE REL TABLE GraphEdge", content);
        Assert.Contains("// Ladybug Knowledge Graph Export", content);
    }

    [Fact]
    public async Task ExportAsync_NodesExportedWithCorrectSchema()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode
        {
            Id = "MyClass",
            Label = "MyClass",
            Type = "Class",
            FilePath = "/src/MyClass.cs",
            RelativePath = "src/MyClass.cs",
            Language = "CSharp",
            Community = 2,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string> { ["source_file"] = "MyClass.cs" }
        });

        var path = Path.Combine(_testRoot, "nodes.ladybug.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("CREATE NODE TABLE GraphNode", content);
        Assert.Contains("metadata MAP(STRING, STRING)", content);
        Assert.Contains("id: \"MyClass\"", content);
        Assert.Contains("nodeType: \"Class\"", content);
        Assert.Contains("community: 2", content);
        Assert.Contains("filePath: \"/src/MyClass.cs\"", content);
        Assert.Contains("relativePath: \"src/MyClass.cs\"", content);
        Assert.Contains("language: \"CSharp\"", content);
        Assert.Contains("confidence: \"Extracted\"", content);
    }

    [Fact]
    public async Task ExportAsync_MetadataExportedAsMap()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode
        {
            Id = "NodeA",
            Label = "NodeA",
            Type = "Function",
            Metadata = new Dictionary<string, string>
            {
                ["source_file"] = "file.cs",
                ["line"] = "42"
            }
        });

        var path = Path.Combine(_testRoot, "metadata.ladybug.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("metadata: map([\"line\", \"source_file\"], [\"42\", \"file.cs\"])", content);
    }

    [Fact]
    public async Task ExportAsync_EmptyMetadata_OmitsMap()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode
        {
            Id = "NodeA",
            Label = "NodeA",
            Type = "Class"
        });

        var path = Path.Combine(_testRoot, "empty-meta.ladybug.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain(", metadata:", content);
    }

    [Fact]
    public async Task ExportAsync_EdgesExportedWithMatch()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "edges.ladybug.cypher");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("CREATE REL TABLE GraphEdge", content);
        Assert.Contains("FROM GraphNode TO GraphNode", content);
        Assert.Contains("MANY_MANY", content);
        
        // Edges should use MATCH to link existing nodes
        Assert.Contains("MATCH (s:GraphNode {id: \"ClassA\"}), (t:GraphNode {id: \"MethodB\"})", content);
        Assert.Contains("CREATE (s)-[:GraphEdge", content);
        Assert.Contains("relationship: \"calls\"", content);
        Assert.Contains("weight:", content);
        Assert.Contains("confidence: \"Extracted\"", content);
    }

    [Fact]
    public async Task ExportAsync_EmptyGraph_ProducesValidButEmptyOutput()
    {
        var graph = new KnowledgeGraph();
        var path = Path.Combine(_testRoot, "empty.ladybug.cypher");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("// Ladybug Knowledge Graph Export", content);
        Assert.Contains("// Nodes: 0, Edges: 0", content);
        Assert.Contains("CREATE NODE TABLE GraphNode", content);
        Assert.Contains("CREATE REL TABLE GraphEdge", content);
        Assert.DoesNotContain("CREATE (", content);
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

        var path = Path.Combine(_testRoot, "special.ladybug.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("\\\"", content);
        Assert.Contains("\\\\", content);
    }

    [Fact]
    public async Task ExportAsync_NewlineCharactersInLabels_AreEscaped()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode
        {
            Id = "multi",
            Label = "Line1\nLine2\tTabbed",
            Type = "Concept"
        });

        var path = Path.Combine(_testRoot, "newline.ladybug.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("\\n", content);
        Assert.Contains("\\t", content);
    }

    [Fact]
    public async Task ExportAsync_FileOutputWritesToDisk()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "output.ladybug.cypher");

        Assert.False(File.Exists(path));

        await _exporter.ExportAsync(graph, path);

        Assert.True(File.Exists(path));
        var info = new FileInfo(path);
        Assert.True(info.Length > 0);
    }

    [Fact]
    public async Task ExportAsync_NodeWithoutCommunity_OmitsCommunityProperty()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode { Id = "A", Label = "A", Type = "Class" });

        var path = Path.Combine(_testRoot, "no-community.ladybug.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("id: \"A\"", content);
        Assert.DoesNotContain(", community:", content);
    }

    [Fact]
    public async Task ExportAsync_NullGraph_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _exporter.ExportAsync(null!, "test.ladybug.cypher"));
    }

    [Fact]
    public async Task ExportAsync_EmptyOutputPath_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _exporter.ExportAsync(new KnowledgeGraph(), ""));
    }

    [Fact]
    public async Task ExportAsync_RelationshipTableUsesManyMany()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "many-many.ladybug.cypher");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("MANY_MANY", content);
    }

    [Fact]
    public async Task ExportAsync_EmptyNodeType_HandledGracefully()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode { Id = "x", Label = "x", Type = "" });

        var path = Path.Combine(_testRoot, "empty-type.ladybug.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("nodeType: \"\"", content);
        Assert.Contains("CREATE NODE TABLE GraphNode", content);
    }

    [Fact]
    public async Task ExportAsync_EdgeWithOrphanedNode_IsSkipped()
    {
        var graph = new KnowledgeGraph();
        var n1 = new GraphNode { Id = "ClassA", Label = "ClassA", Type = "Class" };
        graph.AddNode(n1);

        // Edge references a node not in the graph
        var orphanNode = new GraphNode { Id = "Orphan", Label = "Orphan", Type = "Method" };
        graph.AddEdge(new GraphEdge { Source = n1, Target = orphanNode, Relationship = "calls" });

        var path = Path.Combine(_testRoot, "orphan.ladybug.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        // Should contain node
        Assert.Contains("id: \"ClassA\"", content);
        // Orphan edge must be skipped - no MATCH statement in the output
        Assert.DoesNotContain("MATCH", content);
    }

    [Fact]
    public async Task ExportAsync_WeightFormatUsesTwoDecimals()
    {
        var graph = new KnowledgeGraph();
        var n1 = new GraphNode { Id = "A", Label = "A", Type = "Class" };
        var n2 = new GraphNode { Id = "B", Label = "B", Type = "Method" };
        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddEdge(new GraphEdge { Source = n1, Target = n2, Relationship = "calls", Weight = 3.5 });

        var path = Path.Combine(_testRoot, "weight.ladybug.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("weight: 3.50", content);
    }

    [Fact]
    public async Task ExportAsync_SingleQuoteInLabel_IsEscaped()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode
        {
            Id = "apos",
            Label = "It's a test",
            Type = "Concept"
        });

        var path = Path.Combine(_testRoot, "quote.ladybug.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("It\\'s a test", content);
    }

    [Fact]
    public async Task ExportAsync_WhitespaceOnlyOutputPath_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _exporter.ExportAsync(new KnowledgeGraph(), "   "));
    }

    [Fact]
    public async Task ExportAsync_NullRelationship_UsesRelatedToDefault()
    {
        var graph = new KnowledgeGraph();
        var n1 = new GraphNode { Id = "A", Label = "A", Type = "Class" };
        var n2 = new GraphNode { Id = "B", Label = "B", Type = "Class" };
        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddEdge(new GraphEdge { Source = n1, Target = n2, Relationship = null! });

        var path = Path.Combine(_testRoot, "null-rel.ladybug.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("relationship: \"RELATED_TO\"", content);
    }

    [Fact]
    public async Task ExportAsync_EdgeWithMetadata_EmitsMetadataInEdgeCreate()
    {
        var graph = new KnowledgeGraph();
        var n1 = new GraphNode { Id = "A", Label = "A", Type = "Class" };
        var n2 = new GraphNode { Id = "B", Label = "B", Type = "Class" };
        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddEdge(new GraphEdge
        {
            Source = n1,
            Target = n2,
            Relationship = "calls",
            Metadata = new Dictionary<string, string> { ["source_file"] = "A.cs" }
        });

        var path = Path.Combine(_testRoot, "edge-meta.ladybug.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("metadata: map([\"source_file\"], [\"A.cs\"])", content);
    }

    [Fact]
    public async Task ExportAsync_MultipleEdgesSameSourceTarget_AllEmitted()
    {
        var graph = new KnowledgeGraph();
        var n1 = new GraphNode { Id = "A", Label = "A", Type = "Class" };
        var n2 = new GraphNode { Id = "B", Label = "B", Type = "Class" };
        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddEdge(new GraphEdge { Source = n1, Target = n2, Relationship = "calls" });
        graph.AddEdge(new GraphEdge { Source = n1, Target = n2, Relationship = "imports" });

        var path = Path.Combine(_testRoot, "multi-edge.ladybug.cypher");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("relationship: \"calls\"", content);
        Assert.Contains("relationship: \"imports\"", content);
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
