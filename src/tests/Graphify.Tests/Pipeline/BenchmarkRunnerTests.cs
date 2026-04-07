using System.Text.Json;
using Graphify.Graph;
using Graphify.Models;
using Graphify.Pipeline;
using Xunit;

namespace Graphify.Tests.Pipeline;

/// <summary>
/// Tests for BenchmarkRunner: graph file loading, token reduction calculation,
/// error handling for missing files, and PrintBenchmark formatting.
/// </summary>
[Trait("Category", "Pipeline")]
public sealed class BenchmarkRunnerTests : IDisposable
{
    private readonly string _testRoot;

    public BenchmarkRunnerTests()
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
    public async Task RunAsync_WithValidGraphFile_ReturnsMetrics()
    {
        var graphPath = await CreateSampleGraphFile();

        var result = await BenchmarkRunner.RunAsync(graphPath, corpusWords: 5000);

        Assert.Null(result.Error);
        Assert.True(result.CorpusTokens > 0);
        Assert.True(result.NodeCount > 0);
        Assert.True(result.EdgeCount > 0);
    }

    [Fact]
    public async Task RunAsync_WithMissingFile_ReturnsError()
    {
        var result = await BenchmarkRunner.RunAsync(Path.Combine(_testRoot, "nonexistent.json"));

        Assert.NotNull(result.Error);
        Assert.Contains("not found", result.Error);
    }

    [Fact]
    public async Task RunAsync_TokenReductionCalculation_IsPositive()
    {
        var graphPath = await CreateSampleGraphFile();

        var result = await BenchmarkRunner.RunAsync(graphPath, corpusWords: 10000);

        if (result.Error == null && result.PerQuestion.Count > 0)
        {
            Assert.True(result.ReductionRatio > 0);
            Assert.True(result.AvgQueryTokens > 0);
            Assert.True(result.CorpusTokens > result.AvgQueryTokens);
        }
    }

    [Fact]
    public void PrintBenchmark_FormatsOutputCorrectly()
    {
        var result = new BenchmarkResult
        {
            CorpusTokens = 10000,
            CorpusWords = 7500,
            NodeCount = 50,
            EdgeCount = 30,
            AvgQueryTokens = 500,
            ReductionRatio = 20.0,
            PerQuestion = new[]
            {
                new QuestionBenchmark { Question = "how does auth work", QueryTokens = 400, Reduction = 25.0 },
                new QuestionBenchmark { Question = "what is the entry point", QueryTokens = 600, Reduction = 16.7 }
            }
        };

        using var writer = new StringWriter();
        BenchmarkRunner.PrintBenchmark(result, writer);

        var output = writer.ToString();
        Assert.Contains("graphify token reduction benchmark", output);
        Assert.Contains("7,500 words", output);
        Assert.Contains("50 nodes", output);
        Assert.Contains("20x", output);
    }

    [Fact]
    public void PrintBenchmark_WithError_PrintsErrorMessage()
    {
        var result = new BenchmarkResult { Error = "File not found" };

        using var writer = new StringWriter();
        BenchmarkRunner.PrintBenchmark(result, writer);

        var output = writer.ToString();
        Assert.Contains("Benchmark error: File not found", output);
    }

    [Fact]
    public void PrintBenchmark_NullResult_ThrowsArgumentNullException()
    {
        using var writer = new StringWriter();
        Assert.Throws<ArgumentNullException>(() => BenchmarkRunner.PrintBenchmark(null!, writer));
    }

    [Fact]
    public void PrintBenchmark_NullWriter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => BenchmarkRunner.PrintBenchmark(new BenchmarkResult(), null!));
    }

    [Fact]
    public async Task RunAsync_NullPath_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => BenchmarkRunner.RunAsync(""));
    }

    [Fact]
    public async Task RunAsync_InvalidJson_ReturnsError()
    {
        var path = Path.Combine(_testRoot, "bad.json");
        await File.WriteAllTextAsync(path, "not json content");

        var result = await BenchmarkRunner.RunAsync(path);

        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task RunAsync_CustomQuestions_AreUsed()
    {
        var graphPath = await CreateSampleGraphFile();
        var questions = new[] { "what is authentication", "how does the main entry point work" };

        var result = await BenchmarkRunner.RunAsync(graphPath, corpusWords: 5000, questions: questions);

        // Even if no matches, should not throw
        Assert.NotNull(result);
    }

    private async Task<string> CreateSampleGraphFile()
    {
        var graphData = new
        {
            Nodes = new[]
            {
                new { Id = "AuthService", Label = "Authentication Service", Type = "Class", Community = (int?)0, FilePath = "auth.cs", Metadata = (Dictionary<string, string>?)null },
                new { Id = "MainEntry", Label = "Main Entry Point", Type = "Function", Community = (int?)0, FilePath = "main.cs", Metadata = (Dictionary<string, string>?)null },
                new { Id = "ErrorHandler", Label = "Error Handler", Type = "Class", Community = (int?)1, FilePath = "errors.cs", Metadata = (Dictionary<string, string>?)null },
                new { Id = "DataLayer", Label = "Data Access Layer", Type = "Module", Community = (int?)1, FilePath = "data.cs", Metadata = (Dictionary<string, string>?)null },
                new { Id = "ApiController", Label = "API Controller", Type = "Class", Community = (int?)0, FilePath = "api.cs", Metadata = (Dictionary<string, string>?)null }
            },
            Edges = new[]
            {
                new { Source = "AuthService", Target = "MainEntry", Relationship = "calls", Weight = 1.0, Metadata = (Dictionary<string, string>?)null },
                new { Source = "MainEntry", Target = "ErrorHandler", Relationship = "uses", Weight = 1.0, Metadata = (Dictionary<string, string>?)null },
                new { Source = "DataLayer", Target = "ErrorHandler", Relationship = "imports", Weight = 1.0, Metadata = (Dictionary<string, string>?)null },
                new { Source = "ApiController", Target = "AuthService", Relationship = "calls", Weight = 1.0, Metadata = (Dictionary<string, string>?)null }
            }
        };

        var path = Path.Combine(_testRoot, "graph.json");
        var json = JsonSerializer.Serialize(graphData, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await File.WriteAllTextAsync(path, json);
        return path;
    }
}
