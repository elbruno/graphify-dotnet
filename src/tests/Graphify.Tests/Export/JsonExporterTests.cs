using System.Text.Json;
using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Export;

[Trait("Category", "Export")]
public sealed class JsonExporterTests : IDisposable
{
    private readonly string _testRoot;
    private readonly JsonExporter _exporter = new();

    public JsonExporterTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task ExportAsync_ValidGraph_ProducesValidJson()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var outputPath = Path.Combine(_testRoot, "graph.json");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        var json = await File.ReadAllTextAsync(outputPath);
        Assert.NotEmpty(json);

        // Verify it's valid JSON by parsing
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public async Task ExportAsync_NodeAndEdgeCounts_Match()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var outputPath = Path.Combine(_testRoot, "graph.json");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);

        var nodes = doc.RootElement.GetProperty("nodes");
        var edges = doc.RootElement.GetProperty("edges");

        Assert.Equal(3, nodes.GetArrayLength());
        Assert.Equal(2, edges.GetArrayLength());
    }

    [Fact]
    public async Task ExportAsync_RoundTrip_PreservesData()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var outputPath = Path.Combine(_testRoot, "graph.json");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert - Read back and verify
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);

        var nodes = doc.RootElement.GetProperty("nodes");
        var edges = doc.RootElement.GetProperty("edges");
        var metadata = doc.RootElement.GetProperty("metadata");

        // Check node properties
        var firstNode = nodes[0];
        Assert.True(firstNode.TryGetProperty("id", out var id));
        Assert.True(firstNode.TryGetProperty("label", out var label));
        Assert.True(firstNode.TryGetProperty("type", out var type));

        // Check edge properties
        var firstEdge = edges[0];
        Assert.True(firstEdge.TryGetProperty("source", out var source));
        Assert.True(firstEdge.TryGetProperty("target", out var target));
        Assert.True(firstEdge.TryGetProperty("relationship", out var relationship));
        Assert.True(firstEdge.TryGetProperty("weight", out var weight));

        // Check metadata
        Assert.True(metadata.TryGetProperty("node_count", out var nodeCount));
        Assert.True(metadata.TryGetProperty("edge_count", out var edgeCount));
        Assert.True(metadata.TryGetProperty("community_count", out var communityCount));
        Assert.True(metadata.TryGetProperty("generated_at", out var generatedAt));

        Assert.Equal(3, nodeCount.GetInt32());
        Assert.Equal(2, edgeCount.GetInt32());
    }

    [Fact]
    public async Task ExportAsync_EmptyGraph_ProducesValidJson()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var outputPath = Path.Combine(_testRoot, "empty.json");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);

        var nodes = doc.RootElement.GetProperty("nodes");
        var edges = doc.RootElement.GetProperty("edges");
        var metadata = doc.RootElement.GetProperty("metadata");

        Assert.Equal(0, nodes.GetArrayLength());
        Assert.Equal(0, edges.GetArrayLength());
        Assert.Equal(0, metadata.GetProperty("node_count").GetInt32());
        Assert.Equal(0, metadata.GetProperty("edge_count").GetInt32());
    }

    [Fact]
    public async Task ExportAsync_CommunityAssignments_Exported()
    {
        // Arrange
        var graph = CreateSampleGraph();
        graph.AssignCommunities(new Dictionary<int, IReadOnlyList<string>>
        {
            [0] = new[] { "node1", "node2" },
            [1] = new[] { "node3" }
        });
        var outputPath = Path.Combine(_testRoot, "graph_with_communities.json");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);

        var nodes = doc.RootElement.GetProperty("nodes");
        var metadata = doc.RootElement.GetProperty("metadata");

        // Check community assignments
        foreach (var node in nodes.EnumerateArray())
        {
            Assert.True(node.TryGetProperty("community", out var community));
            Assert.True(community.ValueKind == JsonValueKind.Number);
        }

        // Check community count
        var communityCount = metadata.GetProperty("community_count").GetInt32();
        Assert.Equal(2, communityCount);
    }

    [Fact]
    public async Task ExportAsync_CreatesDirectory_IfNotExists()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var subDir = Path.Combine(_testRoot, "nested", "dirs");
        var outputPath = Path.Combine(subDir, "graph.json");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task ExportAsync_Format_ReturnsJson()
    {
        // Assert
        Assert.Equal("json", _exporter.Format);
    }

    [Fact]
    public async Task ExportAsync_NodeMetadata_Preserved()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node = new GraphNode
        {
            Id = "test_node",
            Label = "TestNode",
            Type = "Class",
            FilePath = "Test.cs",
            Language = "CSharp",
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>
            {
                ["custom_key"] = "custom_value",
                ["line_count"] = "42"
            }
        };
        graph.AddNode(node);

        var outputPath = Path.Combine(_testRoot, "metadata.json");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var nodes = doc.RootElement.GetProperty("nodes");
        var firstNode = nodes[0];

        Assert.True(firstNode.TryGetProperty("metadata", out var metadata));
        Assert.True(metadata.TryGetProperty("custom_key", out var customValue));
        Assert.Equal("custom_value", customValue.GetString());
    }

    [Fact]
    public async Task ExportAsync_EdgeConfidence_Exported()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node1 = CreateNode("a", "A");
        var node2 = CreateNode("b", "B");
        graph.AddNode(node1);
        graph.AddNode(node2);

        var edge = new GraphEdge
        {
            Source = node1,
            Target = node2,
            Relationship = "calls",
            Weight = 2.5,
            Confidence = Confidence.Inferred,
            Metadata = new Dictionary<string, string>()
        };
        graph.AddEdge(edge);

        var outputPath = Path.Combine(_testRoot, "confidence.json");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var edges = doc.RootElement.GetProperty("edges");
        var firstEdge = edges[0];

        Assert.True(firstEdge.TryGetProperty("confidence", out var confidence));
        Assert.Equal("INFERRED", confidence.GetString());
        Assert.True(firstEdge.TryGetProperty("weight", out var weight));
        Assert.Equal(2.5, weight.GetDouble());
    }

    [Fact]
    public async Task ExportAsync_LargeGraph_Completes()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        
        // Create 100 nodes
        for (int i = 0; i < 100; i++)
        {
            graph.AddNode(CreateNode($"node{i}", $"Node{i}"));
        }

        // Create some edges
        var nodes = graph.GetNodes().ToList();
        for (int i = 0; i < 50; i++)
        {
            var edge = new GraphEdge
            {
                Source = nodes[i],
                Target = nodes[i + 50],
                Relationship = "connects",
                Weight = 1.0,
                Confidence = Confidence.Extracted,
                Metadata = new Dictionary<string, string>()
            };
            graph.AddEdge(edge);
        }

        var outputPath = Path.Combine(_testRoot, "large.json");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);

        var nodes_array = doc.RootElement.GetProperty("nodes");
        var edges_array = doc.RootElement.GetProperty("edges");

        Assert.Equal(100, nodes_array.GetArrayLength());
        Assert.Equal(50, edges_array.GetArrayLength());
    }

    private static KnowledgeGraph CreateSampleGraph()
    {
        var graph = new KnowledgeGraph();

        var node1 = CreateNode("node1", "Node1");
        var node2 = CreateNode("node2", "Node2");
        var node3 = CreateNode("node3", "Node3");

        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);

        var edge1 = new GraphEdge
        {
            Source = node1,
            Target = node2,
            Relationship = "calls",
            Weight = 1.0,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        var edge2 = new GraphEdge
        {
            Source = node2,
            Target = node3,
            Relationship = "imports",
            Weight = 1.0,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        graph.AddEdge(edge1);
        graph.AddEdge(edge2);

        return graph;
    }

    private static GraphNode CreateNode(string id, string label)
    {
        return new GraphNode
        {
            Id = id,
            Label = label,
            Type = "Entity",
            FilePath = "test.cs",
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };
    }
}
