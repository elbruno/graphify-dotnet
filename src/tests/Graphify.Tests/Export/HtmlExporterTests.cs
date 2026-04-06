using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Export;

[Trait("Category", "Export")]
public sealed class HtmlExporterTests : IDisposable
{
    private readonly string _testRoot;
    private readonly HtmlExporter _exporter = new();

    public HtmlExporterTests()
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
    public async Task ExportAsync_ValidGraph_ProducesHtmlFile()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var outputPath = Path.Combine(_testRoot, "graph.html");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        var html = await File.ReadAllTextAsync(outputPath);
        Assert.NotEmpty(html);
    }

    [Fact]
    public async Task ExportAsync_Html_ContainsVisJsCdn()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var outputPath = Path.Combine(_testRoot, "graph.html");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var html = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("vis-network", html);
        Assert.Contains("cdn", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_Html_ContainsNodeData()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var outputPath = Path.Combine(_testRoot, "graph.html");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var html = await File.ReadAllTextAsync(outputPath);
        
        // Should contain node IDs
        Assert.Contains("node1", html);
        Assert.Contains("node2", html);
        Assert.Contains("node3", html);
        
        // Should contain node labels
        Assert.Contains("Node1", html);
        Assert.Contains("Node2", html);
        Assert.Contains("Node3", html);
    }

    [Fact]
    public async Task ExportAsync_Html_ContainsEdgeData()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var outputPath = Path.Combine(_testRoot, "graph.html");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var html = await File.ReadAllTextAsync(outputPath);
        
        // Should contain relationship types
        Assert.Contains("calls", html);
        Assert.Contains("imports", html);
    }

    [Fact]
    public async Task ExportAsync_EmptyGraph_ProducesValidHtml()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var outputPath = Path.Combine(_testRoot, "empty.html");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
        var html = await File.ReadAllTextAsync(outputPath);
        Assert.NotEmpty(html);
        Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_CommunityColors_Applied()
    {
        // Arrange
        var graph = CreateSampleGraph();
        graph.AssignCommunities(new Dictionary<int, IReadOnlyList<string>>
        {
            [0] = new[] { "node1", "node2" },
            [1] = new[] { "node3" }
        });
        var outputPath = Path.Combine(_testRoot, "communities.html");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var html = await File.ReadAllTextAsync(outputPath);
        
        // Should contain color information
        Assert.Contains("color", html);
        
        // Should contain community information
        Assert.Contains("community", html);
    }

    [Fact]
    public async Task ExportAsync_Format_ReturnsHtml()
    {
        // Assert
        Assert.Equal("html", _exporter.Format);
    }

    [Fact]
    public async Task ExportAsync_CreatesDirectory_IfNotExists()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var subDir = Path.Combine(_testRoot, "nested", "dirs");
        var outputPath = Path.Combine(subDir, "graph.html");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public async Task ExportAsync_LargeGraph_ThrowsException()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        
        // Create more than max allowed nodes (check HtmlTemplate.MaxNodesForVisualization)
        // Default is usually 1000-2000, let's create 10000
        for (int i = 0; i < 10000; i++)
        {
            graph.AddNode(CreateNode($"node{i}", $"Node{i}"));
        }

        var outputPath = Path.Combine(_testRoot, "large.html");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _exporter.ExportAsync(graph, outputPath)
        );
    }

    [Fact]
    public async Task ExportAsync_WithCommunityLabels_IncludesLabels()
    {
        // Arrange
        var graph = CreateSampleGraph();
        graph.AssignCommunities(new Dictionary<int, IReadOnlyList<string>>
        {
            [0] = new[] { "node1", "node2" },
            [1] = new[] { "node3" }
        });

        var communityLabels = new Dictionary<int, string>
        {
            [0] = "Core Components",
            [1] = "Utilities"
        };

        var outputPath = Path.Combine(_testRoot, "labeled_communities.html");

        // Act
        await _exporter.ExportAsync(graph, outputPath, communityLabels);

        // Assert
        var html = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("Core Components", html);
        Assert.Contains("Utilities", html);
    }

    [Fact]
    public async Task ExportAsync_StatisticsEmbedded_InHtml()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var outputPath = Path.Combine(_testRoot, "stats.html");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var html = await File.ReadAllTextAsync(outputPath);
        
        // Should contain statistics like "3 nodes" and "2 edges"
        Assert.Contains("nodes", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("edges", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_ConfidenceLevels_RenderedDifferently()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node1 = CreateNode("a", "A");
        var node2 = CreateNode("b", "B");
        var node3 = CreateNode("c", "C");

        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);

        // Extracted edge (solid)
        var extractedEdge = new GraphEdge
        {
            Source = node1,
            Target = node2,
            Relationship = "calls",
            Weight = 1.0,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        // Inferred edge (should be dashed)
        var inferredEdge = new GraphEdge
        {
            Source = node2,
            Target = node3,
            Relationship = "uses",
            Weight = 1.0,
            Confidence = Confidence.Inferred,
            Metadata = new Dictionary<string, string>()
        };

        graph.AddEdge(extractedEdge);
        graph.AddEdge(inferredEdge);

        var outputPath = Path.Combine(_testRoot, "confidence.html");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var html = await File.ReadAllTextAsync(outputPath);
        
        // Inferred edges should use dashes
        Assert.Contains("dashes", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("EXTRACTED", html);
        Assert.Contains("INFERRED", html);
    }

    [Fact]
    public async Task ExportAsync_NodeSizes_ProportionalToDegree()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        
        // Hub node with high degree
        var hub = CreateNode("hub", "Hub");
        var leaf1 = CreateNode("leaf1", "Leaf1");
        var leaf2 = CreateNode("leaf2", "Leaf2");
        var leaf3 = CreateNode("leaf3", "Leaf3");

        graph.AddNode(hub);
        graph.AddNode(leaf1);
        graph.AddNode(leaf2);
        graph.AddNode(leaf3);

        // Connect all leaves to hub
        graph.AddEdge(CreateEdge(hub, leaf1));
        graph.AddEdge(CreateEdge(hub, leaf2));
        graph.AddEdge(CreateEdge(hub, leaf3));

        var outputPath = Path.Combine(_testRoot, "sizes.html");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var html = await File.ReadAllTextAsync(outputPath);
        
        // Should contain size information
        Assert.Contains("size", html);
    }

    [Fact]
    public async Task ExportAsync_ValidHtmlStructure_Present()
    {
        // Arrange
        var graph = CreateSampleGraph();
        var outputPath = Path.Combine(_testRoot, "structure.html");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var html = await File.ReadAllTextAsync(outputPath);
        
        // Basic HTML structure checks
        Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<head>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<body>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("</html>", html, StringComparison.OrdinalIgnoreCase);
        
        // Should have a container div for vis.js
        Assert.Contains("<div", html, StringComparison.OrdinalIgnoreCase);
        
        // Should have script tags
        Assert.Contains("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_LegendData_Included()
    {
        // Arrange
        var graph = CreateSampleGraph();
        graph.AssignCommunities(new Dictionary<int, IReadOnlyList<string>>
        {
            [0] = new[] { "node1", "node2" },
            [1] = new[] { "node3" }
        });
        var outputPath = Path.Combine(_testRoot, "legend.html");

        // Act
        await _exporter.ExportAsync(graph, outputPath);

        // Assert
        var html = await File.ReadAllTextAsync(outputPath);
        
        // Should contain legend or community information
        Assert.Contains("community", html, StringComparison.OrdinalIgnoreCase);
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

        graph.AddEdge(CreateEdge(node1, node2, "calls"));
        graph.AddEdge(CreateEdge(node2, node3, "imports"));

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

    private static GraphEdge CreateEdge(GraphNode source, GraphNode target, string relationship)
    {
        return new GraphEdge
        {
            Source = source,
            Target = target,
            Relationship = relationship,
            Weight = 1.0,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };
    }
}
