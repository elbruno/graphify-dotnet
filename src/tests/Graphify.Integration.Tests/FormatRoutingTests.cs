using Graphify.Cli;
using Graphify.Graph;
using Xunit;
using Xunit.Abstractions;

namespace Graphify.Integration.Tests;

/// <summary>
/// Integration tests verifying PipelineRunner correctly routes to each export format
/// and produces expected output files.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FormatRoutingTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public FormatRoutingTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"graphify-routing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    private string CreateTestProject()
    {
        var projectDir = Path.Combine(_tempDir, "testproject");
        Directory.CreateDirectory(projectDir);

        // Create simple C# files
        File.WriteAllText(Path.Combine(projectDir, "Program.cs"),
            "using System;\nnamespace TestApp { class Program { static void Main() { } } }");
        File.WriteAllText(Path.Combine(projectDir, "Service.cs"),
            "namespace TestApp { class Service { public void Execute() { } } }");
        File.WriteAllText(Path.Combine(projectDir, "Model.cs"),
            "namespace TestApp { class Model { public int Id { get; set; } } }");

        return projectDir;
    }

    [Fact(Timeout = 60000)]
    public async Task RunAsync_WithJsonFormat_CreatesJsonFile()
    {
        // Arrange
        var inputPath = CreateTestProject();
        var outputDir = Path.Combine(_tempDir, "output-json");
        var runner = new PipelineRunner(TextWriter.Null, verbose: false);

        // Act
        var result = await runner.RunAsync(
            inputPath,
            outputDir,
            formats: ["json"],
            useCache: false,
            default);

        // Assert
        Assert.NotNull(result);
        var jsonFile = Path.Combine(outputDir, "graph.json");
        Assert.True(File.Exists(jsonFile), "graph.json should be created");
        var content = await File.ReadAllTextAsync(jsonFile);
        Assert.Contains("nodes", content);
        Assert.Contains("edges", content);
        _output.WriteLine($"JSON file size: {content.Length} bytes");
    }

    [Fact(Timeout = 60000)]
    public async Task RunAsync_WithHtmlFormat_CreatesHtmlFile()
    {
        // Arrange
        var inputPath = CreateTestProject();
        var outputDir = Path.Combine(_tempDir, "output-html");
        var runner = new PipelineRunner(TextWriter.Null, verbose: false);

        // Act
        var result = await runner.RunAsync(
            inputPath,
            outputDir,
            formats: ["html"],
            useCache: false,
            default);

        // Assert
        Assert.NotNull(result);
        var htmlFile = Path.Combine(outputDir, "graph.html");
        Assert.True(File.Exists(htmlFile), "graph.html should be created");
        var content = await File.ReadAllTextAsync(htmlFile);
        Assert.Contains("<!DOCTYPE html>", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vis-network", content);
        _output.WriteLine($"HTML file size: {content.Length} bytes");
    }

    [Fact(Timeout = 60000)]
    public async Task RunAsync_WithSvgFormat_CreatesSvgFile()
    {
        // Arrange
        var inputPath = CreateTestProject();
        var outputDir = Path.Combine(_tempDir, "output-svg");
        var runner = new PipelineRunner(TextWriter.Null, verbose: false);

        // Act
        var result = await runner.RunAsync(
            inputPath,
            outputDir,
            formats: ["svg"],
            useCache: false,
            default);

        // Assert
        Assert.NotNull(result);
        var svgFile = Path.Combine(outputDir, "graph.svg");
        Assert.True(File.Exists(svgFile), "graph.svg should be created");
        var content = await File.ReadAllTextAsync(svgFile);
        Assert.Contains("<?xml version", content);
        Assert.Contains("<svg", content);
        _output.WriteLine($"SVG file size: {content.Length} bytes");
    }

    [Fact(Timeout = 60000)]
    public async Task RunAsync_WithNeo4jFormat_CreatesCypherFile()
    {
        // Arrange
        var inputPath = CreateTestProject();
        var outputDir = Path.Combine(_tempDir, "output-neo4j");
        var runner = new PipelineRunner(TextWriter.Null, verbose: false);

        // Act
        var result = await runner.RunAsync(
            inputPath,
            outputDir,
            formats: ["neo4j"],
            useCache: false,
            default);

        // Assert
        Assert.NotNull(result);
        var cypherFile = Path.Combine(outputDir, "graph.neo4j");
        Assert.True(File.Exists(cypherFile), "graph.neo4j should be created");
        var content = await File.ReadAllTextAsync(cypherFile);
        Assert.Contains("CREATE (", content);
        _output.WriteLine($"Cypher file size: {content.Length} bytes");
    }

    [Fact(Timeout = 60000)]
    public async Task RunAsync_WithObsidianFormat_CreatesObsidianVault()
    {
        // Arrange
        var inputPath = CreateTestProject();
        var outputDir = Path.Combine(_tempDir, "output-obsidian");
        var runner = new PipelineRunner(TextWriter.Null, verbose: false);

        // Act
        var result = await runner.RunAsync(
            inputPath,
            outputDir,
            formats: ["obsidian"],
            useCache: false,
            default);

        // Assert
        Assert.NotNull(result);
        var obsidianDir = Path.Combine(outputDir, "graph.obsidian");
        Assert.True(Directory.Exists(obsidianDir), "obsidian directory should be created");
        var mdFiles = Directory.GetFiles(obsidianDir, "*.md");
        Assert.True(mdFiles.Length > 0, "obsidian vault should contain .md files");
        Assert.Contains(mdFiles, f => Path.GetFileName(f) == "_Index.md");
        _output.WriteLine($"Obsidian vault contains {mdFiles.Length} markdown files");
    }

    [Fact(Timeout = 60000)]
    public async Task RunAsync_WithWikiFormat_CreatesWikiPages()
    {
        // Arrange
        var inputPath = CreateTestProject();
        var outputDir = Path.Combine(_tempDir, "output-wiki");
        var runner = new PipelineRunner(TextWriter.Null, verbose: false);

        // Act
        var result = await runner.RunAsync(
            inputPath,
            outputDir,
            formats: ["wiki"],
            useCache: false,
            default);

        // Assert
        Assert.NotNull(result);
        var wikiDir = Path.Combine(outputDir, "graph.wiki");
        Assert.True(Directory.Exists(wikiDir), "wiki directory should be created");
        var mdFiles = Directory.GetFiles(wikiDir, "*.md");
        Assert.True(mdFiles.Length > 0, "wiki should contain .md files");
        Assert.Contains(mdFiles, f => Path.GetFileName(f) == "Index.md");
        _output.WriteLine($"Wiki contains {mdFiles.Length} markdown files");
    }

    [Fact(Timeout = 60000)]
    public async Task RunAsync_WithReportFormat_CreatesGraphReport()
    {
        // Arrange
        var inputPath = CreateTestProject();
        var outputDir = Path.Combine(_tempDir, "output-report");
        var runner = new PipelineRunner(TextWriter.Null, verbose: false);

        // Act
        var result = await runner.RunAsync(
            inputPath,
            outputDir,
            formats: ["report"],
            useCache: false,
            default);

        // Assert
        Assert.NotNull(result);
        var reportFile = Path.Combine(outputDir, "graph.report");
        Assert.True(File.Exists(reportFile), "graph.report should be created");
        var content = await File.ReadAllTextAsync(reportFile);
        Assert.Contains("# Graph Report", content);
        _output.WriteLine($"Report file size: {content.Length} bytes");
    }

    [Fact(Timeout = 90000)]
    public async Task RunAsync_WithAllFormats_CreatesAllOutputFiles()
    {
        // Arrange
        var inputPath = CreateTestProject();
        var outputDir = Path.Combine(_tempDir, "output-all");
        var runner = new PipelineRunner(TextWriter.Null, verbose: false);

        // Act
        var result = await runner.RunAsync(
            inputPath,
            outputDir,
            formats: ["json", "html", "svg", "neo4j", "obsidian", "wiki", "report"],
            useCache: false,
            default);

        // Assert
        Assert.NotNull(result);

        // File-based formats
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.json")), "json output missing");
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.html")), "html output missing");
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.svg")), "svg output missing");
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.neo4j")), "neo4j output missing");
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.report")), "report output missing");

        // Directory-based formats
        Assert.True(Directory.Exists(Path.Combine(outputDir, "graph.obsidian")), "obsidian vault missing");
        Assert.True(Directory.Exists(Path.Combine(outputDir, "graph.wiki")), "wiki directory missing");

        _output.WriteLine("All 7 formats successfully created output");
    }

    [Fact(Timeout = 60000)]
    public async Task RunAsync_WithUnknownFormat_LogsWarning()
    {
        // Arrange
        var inputPath = CreateTestProject();
        var outputDir = Path.Combine(_tempDir, "output-unknown");
        var output = new StringWriter();
        var runner = new PipelineRunner(output, verbose: false);

        // Act
        var result = await runner.RunAsync(
            inputPath,
            outputDir,
            formats: ["json", "unknownformat", "html"],
            useCache: false,
            default);

        // Assert
        Assert.NotNull(result);
        var outputText = output.ToString();
        Assert.Contains("unknown", outputText, StringComparison.OrdinalIgnoreCase);

        // Known formats should still succeed
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.json")));
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.html")));
        _output.WriteLine("Unknown format logged warning, known formats succeeded");
    }

    [Fact(Timeout = 60000)]
    public async Task RunAsync_WithCommaFormats_ParsesCorrectly()
    {
        // Arrange - formats can be passed as separate strings or comma-separated
        var inputPath = CreateTestProject();
        var outputDir = Path.Combine(_tempDir, "output-comma");
        var runner = new PipelineRunner(TextWriter.Null, verbose: false);

        // Act - pass formats as array (CLI would split comma-separated string)
        var result = await runner.RunAsync(
            inputPath,
            outputDir,
            formats: ["json", "html", "svg"],
            useCache: false,
            default);

        // Assert
        Assert.NotNull(result);
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.json")));
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.html")));
        Assert.True(File.Exists(Path.Combine(outputDir, "graph.svg")));
        _output.WriteLine("Multiple formats processed successfully");
    }

    [Fact(Timeout = 60000)]
    public async Task RunAsync_WithEmptyFormats_CompletesSuccessfully()
    {
        // Arrange
        var inputPath = CreateTestProject();
        var outputDir = Path.Combine(_tempDir, "output-empty");
        var runner = new PipelineRunner(TextWriter.Null, verbose: false);

        // Act
        var result = await runner.RunAsync(
            inputPath,
            outputDir,
            formats: [],
            useCache: false,
            default);

        // Assert - pipeline should complete without crashing
        Assert.NotNull(result);
        Assert.True(result.NodeCount > 0, "Graph should be built even without exports");
        _output.WriteLine($"Pipeline completed with {result.NodeCount} nodes, no exports");
    }
}
