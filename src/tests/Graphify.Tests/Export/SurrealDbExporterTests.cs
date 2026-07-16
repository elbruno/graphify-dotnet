using System.Text.Json;
using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Export;

[Trait("Category", "Export")]
public sealed class SurrealDbExporterTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SurrealDbExporter _exporter = new();

    public SurrealDbExporterTests()
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
    public void Format_ReturnsCorrectValue()
    {
        Assert.Equal("surrealdb", _exporter.Format);
    }

    [Fact]
    public async Task ExportAsync_ValidGraph_ProducesDatabase()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var outputPath = Path.Combine(_testRoot, "graph.db");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        var json = await File.ReadAllTextAsync(outputPath);
        Assert.NotEmpty(json);

        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public async Task ExportAsync_NodeCounts_Match()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var outputPath = Path.Combine(_testRoot, "nodes.db");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);

        var entities = doc.RootElement.GetProperty("entities");
        Assert.Equal(3, entities.GetArrayLength());
    }

    [Fact]
    public async Task ExportAsync_EdgeCounts_Match()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var outputPath = Path.Combine(_testRoot, "edges.db");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);

        var relationships = doc.RootElement.GetProperty("relationships");
        Assert.Equal(2, relationships.GetArrayLength());
    }

    [Fact]
    public async Task ExportAsync_EmptyGraph_ProducesDatabase()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var outputPath = Path.Combine(_testRoot, "empty.db");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);

        var entities = doc.RootElement.GetProperty("entities");
        var relationships = doc.RootElement.GetProperty("relationships");

        Assert.Equal(0, entities.GetArrayLength());
        Assert.Equal(0, relationships.GetArrayLength());
    }

    [Fact]
    public async Task ExportAsync_CreatesDirectory_IfNotExists()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var subDir = Path.Combine(_testRoot, "nested", "dirs");
        var outputPath = Path.Combine(subDir, "graph.db");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task ExportAsync_SpecialCharacters_InNodeId()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node = new GraphNode
        {
            Id = "Namespace::Class.Method()",
            Label = "Method",
            Type = "Method",
            FilePath = "test.cs",
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };
        graph.AddNode(node);

        var outputPath = Path.Combine(_testRoot, "special_chars.db");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var entities = doc.RootElement.GetProperty("entities");
        Assert.Single(entities.EnumerateArray());
    }

    [Fact]
    public async Task ExportAsync_Metadata_Preserved()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node = new GraphNode
        {
            Id = "test_node",
            Label = "TestNode",
            Type = "Class",
            FilePath = "Test.cs",
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>
            {
                ["custom_key"] = "custom_value",
                ["line_count"] = "42"
            }
        };
        graph.AddNode(node);

        var outputPath = Path.Combine(_testRoot, "metadata.db");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var entities = doc.RootElement.GetProperty("entities");
        var firstEntity = entities[0];

        Assert.True(firstEntity.TryGetProperty("metadata", out var metadata));
        Assert.True(metadata.TryGetProperty("custom_key", out var customValue));
        Assert.Equal("custom_value", customValue.GetString());
    }

    [Fact]
    public async Task ExportAsync_Confidence_Exported()
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

        var outputPath = Path.Combine(_testRoot, "confidence.db");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var relationships = doc.RootElement.GetProperty("relationships");
        var firstEdge = relationships[0];

        Assert.True(firstEdge.TryGetProperty("confidence", out var confidence));
        Assert.Equal("INFERRED", confidence.GetString());
    }

    [Fact]
    public async Task ExportAsync_Community_Assignments_Exported()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node1 = CreateNode("node1", "Node1");
        var node2 = CreateNode("node2", "Node2");
        graph.AddNode(node1);
        graph.AddNode(node2);

        graph.AssignCommunities(new Dictionary<int, IReadOnlyList<string>>
        {
            [0] = new[] { "node1" },
            [1] = new[] { "node2" }
        });

        var outputPath = Path.Combine(_testRoot, "communities.db");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var entities = doc.RootElement.GetProperty("entities");

        var communities = new List<int?>();
        foreach (var entity in entities.EnumerateArray())
        {
            if (entity.TryGetProperty("community", out var communityVal) && communityVal.ValueKind != JsonValueKind.Null)
            {
                communities.Add(communityVal.GetInt32());
            }
        }

        Assert.NotEmpty(communities);
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

        var outputPath = Path.Combine(_testRoot, "large.db");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);

        var entities = doc.RootElement.GetProperty("entities");
        var relationships = doc.RootElement.GetProperty("relationships");

        Assert.Equal(100, entities.GetArrayLength());
        Assert.Equal(50, relationships.GetArrayLength());
    }

    [Fact]
    public async Task ExportAsync_EdgeReferences_MatchEntityIds()
    {
        // Arrange: Graph with special characters in node IDs to verify URI escaping consistency
        var graph = new KnowledgeGraph();
        var node1 = new GraphNode
        {
            Id = "Namespace::Class.Method()",
            Label = "Method",
            Type = "Method",
            FilePath = "test.cs",
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };
        var node2 = CreateNode("simple_node", "Simple");
        graph.AddNode(node1);
        graph.AddNode(node2);

        var edge = new GraphEdge
        {
            Source = node1,
            Target = node2,
            Relationship = "calls",
            Weight = 1.0,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };
        graph.AddEdge(edge);

        var outputPath = Path.Combine(_testRoot, "edge_reference_match.db");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert: Verify edges can be joined back to entities
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);

        var entities = doc.RootElement.GetProperty("entities");
        var relationships = doc.RootElement.GetProperty("relationships");

        // Collect all entity IDs
        var entityIds = new HashSet<string>();
        foreach (var entity in entities.EnumerateArray())
        {
            if (entity.TryGetProperty("id", out var idVal) && idVal.ValueKind == JsonValueKind.String)
            {
                var idStr = idVal.GetString();
                if (idStr != null)
                {
                    entityIds.Add(idStr);
                }
            }
        }

        // Verify each edge's source and target exist in entity IDs
        foreach (var relationship in relationships.EnumerateArray())
        {
            Assert.True(relationship.TryGetProperty("source", out var sourceVal), "Relationship should have source");
            Assert.True(relationship.TryGetProperty("target", out var targetVal), "Relationship should have target");

            var sourceId = sourceVal.GetString();
            var targetId = targetVal.GetString();

            Assert.NotNull(sourceId);
            Assert.NotNull(targetId);
            Assert.True(entityIds.Contains(sourceId), $"Edge source '{sourceId}' should match an entity ID");
            Assert.True(entityIds.Contains(targetId), $"Edge target '{targetId}' should match an entity ID");
        }
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
