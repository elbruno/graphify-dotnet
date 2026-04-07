using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Export;

/// <summary>
/// Tests for ObsidianExporter: markdown file creation per node, wiki-link syntax,
/// YAML frontmatter, empty graph handling, and index generation.
/// </summary>
[Trait("Category", "Export")]
public sealed class ObsidianExporterTests : IDisposable
{
    private readonly string _testRoot;
    private readonly ObsidianExporter _exporter = new();

    public ObsidianExporterTests()
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
    public void Format_ReturnsObsidian()
    {
        Assert.Equal("obsidian", _exporter.Format);
    }

    [Fact]
    public async Task ExportAsync_CreatesMarkdownFilePerNode()
    {
        var graph = CreateTwoNodeGraph();
        var outputDir = Path.Combine(_testRoot, "vault");

        await _exporter.ExportAsync(graph, outputDir);

        Assert.True(File.Exists(Path.Combine(outputDir, "Alpha.md")));
        Assert.True(File.Exists(Path.Combine(outputDir, "Beta.md")));
        Assert.True(File.Exists(Path.Combine(outputDir, "_Index.md")));
    }

    [Fact]
    public async Task ExportAsync_LinksBetweenNodesUseWikiLinkSyntax()
    {
        var graph = CreateTwoNodeGraph();
        var outputDir = Path.Combine(_testRoot, "wikilinks");

        await _exporter.ExportAsync(graph, outputDir);

        var alphaContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "Alpha.md"));
        Assert.Contains("[[Beta]]", alphaContent);
    }

    [Fact]
    public async Task ExportAsync_NodeMetadataIncludedInFrontmatter()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode
        {
            Id = "Svc",
            Label = "Service",
            Type = "Class",
            Metadata = new Dictionary<string, string> { ["source_file"] = "svc.cs" }
        });

        var outputDir = Path.Combine(_testRoot, "meta");
        await _exporter.ExportAsync(graph, outputDir);

        var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "Service.md"));
        Assert.Contains("---", content);
        Assert.Contains("id: Svc", content);
        Assert.Contains("type: Class", content);
        Assert.Contains("source_file:", content);
    }

    [Fact]
    public async Task ExportAsync_EmptyGraph_ProducesEmptyVault()
    {
        var graph = new KnowledgeGraph();
        var outputDir = Path.Combine(_testRoot, "empty");

        await _exporter.ExportAsync(graph, outputDir);

        // Only index file
        Assert.True(File.Exists(Path.Combine(outputDir, "_Index.md")));
        var mdFiles = Directory.GetFiles(outputDir, "*.md");
        Assert.Single(mdFiles);
    }

    [Fact]
    public async Task ExportAsync_CommunityAssignment_IncludedInFrontmatter()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode { Id = "A", Label = "NodeA", Type = "Class", Community = 1 });

        var outputDir = Path.Combine(_testRoot, "community");
        await _exporter.ExportAsync(graph, outputDir);

        var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "NodeA.md"));
        Assert.Contains("community:", content);
        Assert.Contains("Community 1", content);
    }

    [Fact]
    public async Task ExportAsync_IndexFileContainsStatistics()
    {
        var graph = CreateTwoNodeGraph();
        var outputDir = Path.Combine(_testRoot, "stats");

        await _exporter.ExportAsync(graph, outputDir);

        var index = await File.ReadAllTextAsync(Path.Combine(outputDir, "_Index.md"));
        Assert.Contains("Knowledge Graph Index", index);
        Assert.Contains("Nodes:", index);
        Assert.Contains("Edges:", index);
    }

    [Fact]
    public async Task ExportAsync_NullGraph_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _exporter.ExportAsync(null!, _testRoot));
    }

    [Fact]
    public async Task ExportAsync_NodeWithNoConnections_ShowsNoConnectionsMessage()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode { Id = "Lone", Label = "LoneNode", Type = "Entity" });

        var outputDir = Path.Combine(_testRoot, "lone");
        await _exporter.ExportAsync(graph, outputDir);

        var content = await File.ReadAllTextAsync(Path.Combine(outputDir, "LoneNode.md"));
        Assert.Contains("No connections found", content);
    }

    [Fact]
    public async Task ExportAsync_CreatesOutputDirectory()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode { Id = "X", Label = "X", Type = "T" });
        var outputDir = Path.Combine(_testRoot, "nested", "vault");

        await _exporter.ExportAsync(graph, outputDir);

        Assert.True(Directory.Exists(outputDir));
    }

    private static KnowledgeGraph CreateTwoNodeGraph()
    {
        var graph = new KnowledgeGraph();
        var n1 = new GraphNode { Id = "A", Label = "Alpha", Type = "Class" };
        var n2 = new GraphNode { Id = "B", Label = "Beta", Type = "Method" };
        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddEdge(new GraphEdge { Source = n1, Target = n2, Relationship = "calls" });
        return graph;
    }
}
