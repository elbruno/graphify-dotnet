using Graphify.Graph;
using Graphify.Integration.Tests.Helpers;
using Graphify.Models;
using Graphify.Pipeline;
using Xunit;
using Xunit.Abstractions;

namespace Graphify.Integration.Tests;

[Trait("Category", "Integration")]
public sealed class PipelineIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public PipelineIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"graphify-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* cleanup best-effort */ }
    }

    [Fact(Timeout = 30000)]
    public async Task FileDetection_ToGraphBuild_EndToEnd()
    {
        // Arrange: create sample source files
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Program.cs"),
            "using System;\npublic class Program { static void Main() {} }");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "utils.py"),
            "def helper():\n    pass\n\nclass Utils:\n    pass");

        // Act: Stage 1 — detect files
        var detector = new FileDetector();
        var options = new FileDetectorOptions(RootPath: _tempDir, RespectGitIgnore: false);
        var detectedFiles = await detector.ExecuteAsync(options);

        _output.WriteLine($"Detected {detectedFiles.Count} files");
        foreach (var f in detectedFiles)
            _output.WriteLine($"  {f.RelativePath} ({f.Language})");

        // Assert: both files detected
        Assert.True(detectedFiles.Count >= 2, $"Expected ≥2 files, got {detectedFiles.Count}");
        Assert.Contains(detectedFiles, f => f.Extension == ".cs");
        Assert.Contains(detectedFiles, f => f.Extension == ".py");

        // Act: Stage 2 — build graph from mock extraction results
        var extractionResults = TestGraphFactory.CreateMockExtractionResults("src/Sample.cs");
        var builder = new GraphBuilder(new GraphBuilderOptions { CreateFileNodes = true, MinEdgeWeight = 0.0, MergeStrategy = MergeStrategy.MostRecent });
        var graph = await builder.ExecuteAsync(extractionResults);

        _output.WriteLine($"Graph: {graph.NodeCount} nodes, {graph.EdgeCount} edges");

        // Assert: graph has nodes and edges
        Assert.True(graph.NodeCount > 0, "Graph should have nodes");
        Assert.True(graph.EdgeCount > 0, "Graph should have edges");
    }

    [Fact(Timeout = 30000)]
    public async Task FullPipeline_WithMockExtractor_ProducesValidGraph()
    {
        // Arrange: multi-file extraction results
        var extractionResults = TestGraphFactory.CreateMultiFileExtractionResults();

        // Act: GraphBuilder → ClusterEngine → Analyzer
        var builder = new GraphBuilder(new GraphBuilderOptions { CreateFileNodes = true, MergeStrategy = MergeStrategy.MostRecent });
        var graph = await builder.ExecuteAsync(extractionResults);

        _output.WriteLine($"After build: {graph.NodeCount} nodes, {graph.EdgeCount} edges");

        var clusterEngine = new ClusterEngine(new ClusterOptions { MaxIterations = 100, Resolution = 1.0, MinSplitSize = 5, MaxCommunityFraction = 0.5 });
        graph = await clusterEngine.ExecuteAsync(graph);

        var communityCount = graph.GetNodes().Where(n => n.Community.HasValue).Select(n => n.Community!.Value).Distinct().Count();
        _output.WriteLine($"After cluster: {communityCount} communities");

        var analyzer = new Analyzer(new AnalyzerOptions { TopGodNodesCount = 10, TopSurprisingConnections = 5, MaxSuggestedQuestions = 10 });
        var analysis = await analyzer.ExecuteAsync(graph);

        // Assert: full analysis result is valid
        Assert.NotNull(analysis);
        Assert.NotNull(analysis.GodNodes);
        Assert.NotNull(analysis.SurprisingConnections);
        Assert.NotNull(analysis.SuggestedQuestions);
        Assert.NotNull(analysis.Statistics);
        Assert.True(analysis.Statistics.NodeCount > 0, "Statistics should report nodes");
        Assert.True(analysis.Statistics.EdgeCount > 0, "Statistics should report edges");

        _output.WriteLine($"Analysis: {analysis.GodNodes.Count} god nodes, {analysis.Statistics.NodeCount} total nodes");
    }

    [Fact(Timeout = 30000)]
    public async Task Pipeline_WithEmptyDirectory_ProducesEmptyGraph()
    {
        // Arrange: empty temp dir (already created)
        var detector = new FileDetector();
        var options = new FileDetectorOptions(RootPath: _tempDir, RespectGitIgnore: false);

        // Act
        var detectedFiles = await detector.ExecuteAsync(options);

        _output.WriteLine($"Detected {detectedFiles.Count} files in empty dir");

        // Build graph from empty extraction results
        var builder = new GraphBuilder();
        var graph = await builder.ExecuteAsync(new List<ExtractionResult>());

        // Assert
        Assert.Empty(detectedFiles);
        Assert.Equal(0, graph.NodeCount);
        Assert.Equal(0, graph.EdgeCount);
    }

    [Fact(Timeout = 30000)]
    public async Task Pipeline_WithNestedDirectories_FindsAllFiles()
    {
        // Arrange: create nested directory structure
        var srcDir = Path.Combine(_tempDir, "src");
        var libDir = Path.Combine(_tempDir, "src", "lib");
        var testDir = Path.Combine(_tempDir, "tests");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(libDir);
        Directory.CreateDirectory(testDir);

        await File.WriteAllTextAsync(Path.Combine(srcDir, "App.cs"), "public class App {}");
        await File.WriteAllTextAsync(Path.Combine(libDir, "Helper.cs"), "public class Helper {}");
        await File.WriteAllTextAsync(Path.Combine(testDir, "AppTests.cs"), "public class AppTests {}");

        // Act
        var detector = new FileDetector();
        var options = new FileDetectorOptions(RootPath: _tempDir, RespectGitIgnore: false);
        var detectedFiles = await detector.ExecuteAsync(options);

        _output.WriteLine($"Found {detectedFiles.Count} files across nested dirs:");
        foreach (var f in detectedFiles)
            _output.WriteLine($"  {f.RelativePath}");

        // Assert: all 3 files found
        Assert.Equal(3, detectedFiles.Count);
        Assert.Contains(detectedFiles, f => f.FileName == "App.cs");
        Assert.Contains(detectedFiles, f => f.FileName == "Helper.cs");
        Assert.Contains(detectedFiles, f => f.FileName == "AppTests.cs");
    }

    [Fact(Timeout = 30000)]
    public async Task Pipeline_RespectsCancellation()
    {
        // Arrange: already-cancelled token
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Big.cs"), "public class Big {}");

        var detector = new FileDetector();
        var options = new FileDetectorOptions(RootPath: _tempDir, RespectGitIgnore: false);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => detector.ExecuteAsync(options, cts.Token));
    }
}
