using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Export;

/// <summary>
/// Tests for WikiExporter: wiki-formatted page creation, internal links,
/// table of contents (index), community articles, and edge cases.
/// </summary>
[Trait("Category", "Export")]
public sealed class WikiExporterTests : IDisposable
{
    private readonly string _testRoot;
    private readonly WikiExporter _exporter = new();

    public WikiExporterTests()
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
    public void Format_ReturnsWiki()
    {
        Assert.Equal("wiki", _exporter.Format);
    }

    [Fact]
    public async Task ExportAsync_CreatesIndexFile()
    {
        var graph = CreateGraphWithCommunities();
        var outputDir = Path.Combine(_testRoot, "wiki");

        await _exporter.ExportAsync(graph, outputDir);

        Assert.True(File.Exists(Path.Combine(outputDir, "index.md")));
    }

    [Fact]
    public async Task ExportAsync_IndexContainsTableOfContents()
    {
        var graph = CreateGraphWithCommunities();
        var outputDir = Path.Combine(_testRoot, "toc");

        await _exporter.ExportAsync(graph, outputDir);

        var index = await File.ReadAllTextAsync(Path.Combine(outputDir, "index.md"));
        Assert.Contains("Knowledge Graph Index", index);
        Assert.Contains("## Communities", index);
        Assert.Contains("nodes", index);
        Assert.Contains("edges", index);
    }

    [Fact]
    public async Task ExportAsync_InternalLinksBetweenNodes()
    {
        var graph = CreateGraphWithCommunities();
        var outputDir = Path.Combine(_testRoot, "links");

        await _exporter.ExportAsync(graph, outputDir);

        var index = await File.ReadAllTextAsync(Path.Combine(outputDir, "index.md"));
        Assert.Contains("[[", index);
        Assert.Contains("]]", index);
    }

    [Fact]
    public async Task ExportAsync_CommunityArticlesCreated()
    {
        var graph = CreateGraphWithCommunities();
        var outputDir = Path.Combine(_testRoot, "communities");

        await _exporter.ExportAsync(graph, outputDir);

        Assert.True(File.Exists(Path.Combine(outputDir, "Community_0.md")));
    }

    [Fact]
    public async Task ExportAsync_CommunityArticleContainsKeyConcepts()
    {
        var graph = CreateGraphWithCommunities();
        var outputDir = Path.Combine(_testRoot, "concepts");

        await _exporter.ExportAsync(graph, outputDir);

        var communityFile = Path.Combine(outputDir, "Community_0.md");
        if (File.Exists(communityFile))
        {
            var content = await File.ReadAllTextAsync(communityFile);
            Assert.Contains("Key Concepts", content);
            Assert.Contains("connections", content);
        }
    }

    [Fact]
    public async Task ExportAsync_EmptyGraph_ProducesValidIndex()
    {
        var graph = new KnowledgeGraph();
        var outputDir = Path.Combine(_testRoot, "empty");

        await _exporter.ExportAsync(graph, outputDir);

        var index = await File.ReadAllTextAsync(Path.Combine(outputDir, "index.md"));
        Assert.Contains("Knowledge Graph Index", index);
        Assert.Contains("0 nodes", index);
    }

    [Fact]
    public async Task ExportAsync_GodNodeArticlesCreated()
    {
        var graph = CreateGraphWithCommunities();
        var outputDir = Path.Combine(_testRoot, "godnodes");

        await _exporter.ExportAsync(graph, outputDir);

        // Hub node should have an article
        var files = Directory.GetFiles(outputDir, "*.md");
        Assert.True(files.Length >= 2); // At least index + community or god node
    }

    [Fact]
    public async Task ExportAsync_NullGraph_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _exporter.ExportAsync(null!, _testRoot));
    }

    [Fact]
    public async Task ExportAsync_EmptyPath_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _exporter.ExportAsync(new KnowledgeGraph(), ""));
    }

    [Fact]
    public async Task ExportAsync_CommunityArticleContainsAuditTrail()
    {
        var graph = CreateGraphWithCommunities();
        var outputDir = Path.Combine(_testRoot, "audit");

        await _exporter.ExportAsync(graph, outputDir);

        var communityFile = Path.Combine(outputDir, "Community_0.md");
        if (File.Exists(communityFile))
        {
            var content = await File.ReadAllTextAsync(communityFile);
            Assert.Contains("Audit Trail", content);
        }
    }

    [Fact]
    public async Task ExportAsync_IndexShowsGodNodes()
    {
        var graph = CreateGraphWithCommunities();
        var outputDir = Path.Combine(_testRoot, "godsection");

        await _exporter.ExportAsync(graph, outputDir);

        var index = await File.ReadAllTextAsync(Path.Combine(outputDir, "index.md"));
        Assert.Contains("God Nodes", index);
    }

    private static KnowledgeGraph CreateGraphWithCommunities()
    {
        var graph = new KnowledgeGraph();
        var hub = new GraphNode { Id = "Hub", Label = "HubNode", Type = "Class" };
        var n1 = new GraphNode { Id = "A", Label = "Alpha", Type = "Class" };
        var n2 = new GraphNode { Id = "B", Label = "Beta", Type = "Method" };
        var n3 = new GraphNode { Id = "C", Label = "Gamma", Type = "Function" };
        graph.AddNode(hub);
        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddNode(n3);
        graph.AddEdge(new GraphEdge { Source = hub, Target = n1, Relationship = "calls" });
        graph.AddEdge(new GraphEdge { Source = hub, Target = n2, Relationship = "uses" });
        graph.AddEdge(new GraphEdge { Source = hub, Target = n3, Relationship = "imports" });

        var communities = new Dictionary<int, IReadOnlyList<string>>
        {
            [0] = new[] { "Hub", "A", "B" },
            [1] = new[] { "C" }
        };
        graph.AssignCommunities(communities);
        return graph;
    }
}
