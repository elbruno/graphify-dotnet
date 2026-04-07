using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Export;

/// <summary>
/// Tests for SvgExporter: valid SVG XML output, node/edge rendering,
/// viewBox dimensions, empty graph handling, and community colors.
/// </summary>
[Trait("Category", "Export")]
public sealed class SvgExporterTests : IDisposable
{
    private readonly string _testRoot;
    private readonly SvgExporter _exporter = new();

    public SvgExporterTests()
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
    public void Format_ReturnsSvg()
    {
        Assert.Equal("svg", _exporter.Format);
    }

    [Fact]
    public async Task ExportAsync_ProducesValidSvgXml()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "graph.svg");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.StartsWith("<?xml", content);
        Assert.Contains("<svg", content);
        Assert.Contains("</svg>", content);
    }

    [Fact]
    public async Task ExportAsync_SvgHasProperViewBoxAndDimensions()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "viewbox.svg");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("viewBox=\"0 0 1600 1200\"", content);
        Assert.Contains("width=\"1600\"", content);
        Assert.Contains("height=\"1200\"", content);
    }

    [Fact]
    public async Task ExportAsync_NodesRenderedAsCircles()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "circles.svg");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("<circle", content);
        Assert.Contains("class=\"node\"", content);
        Assert.Contains("cx=", content);
        Assert.Contains("cy=", content);
        Assert.Contains("r=", content);
    }

    [Fact]
    public async Task ExportAsync_EdgesRenderedAsLines()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "lines.svg");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("<line", content);
        Assert.Contains("class=\"edge\"", content);
        Assert.Contains("x1=", content);
        Assert.Contains("y1=", content);
    }

    [Fact]
    public async Task ExportAsync_EmptyGraph_ProducesValidEmptySvg()
    {
        var graph = new KnowledgeGraph();
        var path = Path.Combine(_testRoot, "empty.svg");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("<svg", content);
        Assert.Contains("</svg>", content);
        Assert.Contains("Empty Graph", content);
    }

    [Fact]
    public async Task ExportAsync_IncludesLegendWithStats()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "legend.svg");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("Knowledge Graph", content);
        Assert.Contains("nodes", content);
        Assert.Contains("edges", content);
    }

    [Fact]
    public async Task ExportAsync_NodeTitlesIncludeLabel()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode { Id = "X", Label = "MyNode", Type = "Class" });

        var path = Path.Combine(_testRoot, "titles.svg");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("<title>MyNode", content);
    }

    [Fact]
    public async Task ExportAsync_CommunityColorsApplied()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode { Id = "A", Label = "A", Type = "Class", Community = 0 });
        graph.AddNode(new GraphNode { Id = "B", Label = "B", Type = "Class", Community = 1 });

        var path = Path.Combine(_testRoot, "colors.svg");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        // Community 0 → first color, community 1 → second color
        Assert.Contains("fill=\"#4285F4\"", content);
        Assert.Contains("fill=\"#EA4335\"", content);
    }

    [Fact]
    public async Task ExportAsync_NullGraph_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _exporter.ExportAsync(null!, "test.svg"));
    }

    [Fact]
    public async Task ExportAsync_SpecialCharsInLabel_AreXmlEscaped()
    {
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode { Id = "node", Label = "A<B>&C", Type = "Class" });

        var path = Path.Combine(_testRoot, "escape.svg");
        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("A&lt;B&gt;&amp;C", content);
    }

    [Fact]
    public async Task ExportAsync_HasStyleDefinitions()
    {
        var graph = CreateSampleGraph();
        var path = Path.Combine(_testRoot, "style.svg");

        await _exporter.ExportAsync(graph, path);

        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("<style>", content);
        Assert.Contains(".edge", content);
        Assert.Contains(".node", content);
        Assert.Contains(".label", content);
    }

    private static KnowledgeGraph CreateSampleGraph()
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
