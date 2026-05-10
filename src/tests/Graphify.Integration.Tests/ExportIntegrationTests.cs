using System.Text.Json;
using Graphify.Export;
using Graphify.Graph;
using Graphify.Integration.Tests.Helpers;
using Graphify.Models;
using Graphify.Pipeline;
using Xunit;
using Xunit.Abstractions;

namespace Graphify.Integration.Tests;

[Trait("Category", "Integration")]
public sealed class ExportIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public ExportIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"graphify-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    private async Task<KnowledgeGraph> BuildClusteredGraphAsync()
    {
        var graph = TestGraphFactory.CreateSmallGraph();
        var clusterEngine = new ClusterEngine(new ClusterOptions { MaxIterations = 100, Resolution = 1.0, MinSplitSize = 5, MaxCommunityFraction = 0.5 });
        return await clusterEngine.ExecuteAsync(graph);
    }

    [Fact(Timeout = 30000)]
    public async Task JsonExport_ThenReimport_PreservesGraph()
    {
        // Arrange
        var graph = await BuildClusteredGraphAsync();
        var jsonPath = Path.Combine(_tempDir, "graph.json");

        // Act: export
        var exporter = new JsonExporter();
        await exporter.ExportAsync(graph, jsonPath);

        Assert.True(File.Exists(jsonPath), "JSON file should exist after export");

        // Act: read back
        var json = await File.ReadAllTextAsync(jsonPath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var exportedNodes = root.GetProperty("nodes").GetArrayLength();
        var exportedEdges = root.GetProperty("edges").GetArrayLength();

        _output.WriteLine($"Original: {graph.NodeCount} nodes, {graph.EdgeCount} edges");
        _output.WriteLine($"Reimported JSON: {exportedNodes} nodes, {exportedEdges} edges");

        // Assert: counts match
        Assert.Equal(graph.NodeCount, exportedNodes);
        Assert.Equal(graph.EdgeCount, exportedEdges);

        // Verify metadata
        var metadata = root.GetProperty("metadata");
        Assert.Equal(graph.NodeCount, metadata.GetProperty("node_count").GetInt32());
        Assert.Equal(graph.EdgeCount, metadata.GetProperty("edge_count").GetInt32());
    }

    [Fact(Timeout = 30000)]
    public async Task HtmlExport_ProducesValidHtml()
    {
        // Arrange
        var graph = await BuildClusteredGraphAsync();
        var htmlPath = Path.Combine(_tempDir, "graph.html");

        // Act
        var exporter = new HtmlExporter();
        await exporter.ExportAsync(graph, htmlPath, communityLabels: null);

        Assert.True(File.Exists(htmlPath), "HTML file should exist after export");
        var html = await File.ReadAllTextAsync(htmlPath);

        _output.WriteLine($"HTML size: {html.Length} chars");

        // Assert: contains expected vis.js structures
        Assert.Contains("vis-network", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nodes", html);
        Assert.Contains("edges", html);
        Assert.True(html.Length > 500, "HTML output should be substantial");
    }

    [Fact(Timeout = 30000)]
    public async Task MultiFormatExport_AllFormatsSucceed()
    {
        // Arrange
        var graph = await BuildClusteredGraphAsync();
        var formats = new (string Name, IGraphExporter Exporter)[]
        {
            ("json", new JsonExporter()),
            ("html", new HtmlExporter()),
            ("svg", new SvgExporter()),
            ("neo4j", new Neo4jExporter()),
            ("ladybug", new LadybugExporter()),
        };

        // Act & Assert: file-based exporters
        foreach (var (name, exporter) in formats)
        {
            var outputPath = Path.Combine(_tempDir, $"graph.{name}");
            await exporter.ExportAsync(graph, outputPath);

            Assert.True(File.Exists(outputPath), $"{name} file should exist");
            var fileInfo = new FileInfo(outputPath);
            Assert.True(fileInfo.Length > 0, $"{name} file should not be empty");
            _output.WriteLine($"  {name}: {fileInfo.Length} bytes");
        }

        // Directory-based exporters (obsidian, wiki)
        var obsidianDir = Path.Combine(_tempDir, "obsidian-vault");
        var obsidianExporter = new ObsidianExporter();
        await obsidianExporter.ExportAsync(graph, obsidianDir);
        Assert.True(Directory.Exists(obsidianDir), "Obsidian vault directory should exist");
        Assert.True(Directory.GetFiles(obsidianDir, "*.md").Length > 0, "Obsidian vault should contain .md files");

        var wikiDir = Path.Combine(_tempDir, "wiki");
        var wikiExporter = new WikiExporter();
        await wikiExporter.ExportAsync(graph, wikiDir);
        Assert.True(Directory.Exists(wikiDir), "Wiki directory should exist");
        Assert.True(Directory.GetFiles(wikiDir, "*.md").Length > 0, "Wiki should contain .md files");

        _output.WriteLine($"All 7 export formats succeeded");
    }

    [Fact(Timeout = 30000)]
    public async Task Export_ToNonExistentDirectory_CreatesDirectory()
    {
        // Arrange
        var graph = TestGraphFactory.CreateSmallGraph();
        var nestedDir = Path.Combine(_tempDir, "deep", "nested", "output");
        var jsonPath = Path.Combine(nestedDir, "graph.json");

        Assert.False(Directory.Exists(nestedDir), "Directory should not exist initially");

        // Act
        var exporter = new JsonExporter();
        await exporter.ExportAsync(graph, jsonPath);

        // Assert
        Assert.True(Directory.Exists(nestedDir), "Directory should be created by exporter");
        Assert.True(File.Exists(jsonPath), "File should be written");
        var info = new FileInfo(jsonPath);
        Assert.True(info.Length > 0, "File should have content");

        _output.WriteLine($"Created {jsonPath} ({info.Length} bytes)");
    }

    [Fact(Timeout = 30000)]
    public async Task ReportGeneration_ProducesMarkdownReport()
    {
        // Arrange
        var graph = await BuildClusteredGraphAsync();
        var analyzer = new Analyzer(new AnalyzerOptions
        {
            TopGodNodesCount = 10,
            TopSurprisingConnections = 5,
            MaxSuggestedQuestions = 10
        });
        var analysis = await analyzer.ExecuteAsync(graph);

        // Act
        var reportGenerator = new ReportGenerator();
        var communityLabels = new Dictionary<int, string> { { 0, "Core" }, { 1, "Utilities" } };
        var cohesionScores = new Dictionary<int, double> { { 0, 0.8 }, { 1, 0.6 } };
        var report = reportGenerator.Generate(graph, analysis, communityLabels, cohesionScores, "TestProject");

        // Assert
        Assert.NotEmpty(report);
        Assert.Contains("# Graph Report", report);
        Assert.Contains("TestProject", report);
        Assert.Contains("nodes", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("edges", report, StringComparison.OrdinalIgnoreCase);

        _output.WriteLine($"Report length: {report.Length} characters");
        _output.WriteLine("Report excerpt:");
        _output.WriteLine(report[..Math.Min(500, report.Length)]);
    }

    [Fact(Timeout = 30000)]
    public async Task SvgExport_ProducesValidSvg()
    {
        // Arrange
        var graph = await BuildClusteredGraphAsync();
        var svgPath = Path.Combine(_tempDir, "graph.svg");

        // Act
        var exporter = new SvgExporter();
        await exporter.ExportAsync(graph, svgPath);

        Assert.True(File.Exists(svgPath), "SVG file should exist after export");
        var svg = await File.ReadAllTextAsync(svgPath);

        _output.WriteLine($"SVG size: {svg.Length} chars");

        // Assert: contains expected SVG structures
        Assert.Contains("<?xml version", svg);
        Assert.Contains("<svg", svg);
        Assert.Contains("xmlns", svg);
        Assert.Contains("<circle", svg); // nodes
        Assert.Contains("<line", svg); // edges
        Assert.True(svg.Length > 200, "SVG output should be substantial");
    }

    [Fact(Timeout = 30000)]
    public async Task Neo4jExport_ProducesValidCypher()
    {
        // Arrange
        var graph = await BuildClusteredGraphAsync();
        var cypherPath = Path.Combine(_tempDir, "graph.cypher");

        // Act
        var exporter = new Neo4jExporter();
        await exporter.ExportAsync(graph, cypherPath);

        Assert.True(File.Exists(cypherPath), "Cypher file should exist after export");
        var cypher = await File.ReadAllTextAsync(cypherPath);

        _output.WriteLine($"Cypher size: {cypher.Length} chars");

        // Assert: contains expected Cypher structures
        Assert.Contains("CREATE (", cypher);
        Assert.Contains("Knowledge Graph Export", cypher);
        Assert.True(cypher.Length > 100, "Cypher output should be substantial");
    }
}
