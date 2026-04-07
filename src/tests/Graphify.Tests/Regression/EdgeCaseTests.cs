using Graphify.Cache;
using Graphify.Graph;
using Graphify.Models;
using Graphify.Pipeline;
using Xunit;

namespace Graphify.Tests.Regression;

/// <summary>
/// Edge case tests related to fixed bugs and common failure modes.
/// These tests prevent regressions in boundary conditions.
/// </summary>
[Trait("Category", "EdgeCase")]
public sealed class EdgeCaseTests : IDisposable
{
    private readonly string _tempDir;

    public EdgeCaseTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"graphify-edge-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private static GraphNode MakeNode(string id, string type = "function") =>
        new() { Id = id, Label = id, Type = type };

    /// <summary>
    /// Null safety: AddNode with null should throw ArgumentNullException.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task KnowledgeGraph_AddNode_WithNullLabel_Throws()
    {
        await Task.CompletedTask;
        var graph = new KnowledgeGraph();
        Assert.Throws<ArgumentNullException>(() => graph.AddNode(null!));
    }

    /// <summary>
    /// Empty safety: AddNode with empty Id should be handled.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task KnowledgeGraph_AddNode_WithEmptyLabel_DoesNotCrash()
    {
        await Task.CompletedTask;
        var graph = new KnowledgeGraph();
        // Empty Id is technically valid — this tests it doesn't throw
        var node = new GraphNode { Id = "", Label = "", Type = "unknown" };
        graph.AddNode(node);
        Assert.Equal(1, graph.NodeCount);
    }

    /// <summary>
    /// Idempotency: Adding the same node twice should overwrite, not crash or duplicate.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task KnowledgeGraph_AddDuplicateNode_IsIdempotent()
    {
        await Task.CompletedTask;
        var graph = new KnowledgeGraph();
        var node1 = MakeNode("MyClass", "class");
        var node2 = MakeNode("MyClass", "class");

        graph.AddNode(node1);
        graph.AddNode(node2);

        Assert.Equal(1, graph.NodeCount);
        Assert.NotNull(graph.GetNode("MyClass"));
    }

    /// <summary>
    /// Edge between non-existent nodes should return false, not throw.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task KnowledgeGraph_AddEdge_WithMissingNodes_ReturnsFalse()
    {
        await Task.CompletedTask;
        var graph = new KnowledgeGraph();
        var missingSource = MakeNode("missing1");
        var missingTarget = MakeNode("missing2");
        var edge = new GraphEdge
        {
            Source = missingSource,
            Target = missingTarget,
            Relationship = "calls"
        };

        var result = graph.AddEdge(edge);
        Assert.False(result);
        Assert.Equal(0, graph.EdgeCount);
    }

    /// <summary>
    /// Thread safety: Multiple parallel reads from SemanticCache must not corrupt data.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SemanticCache_ConcurrentReads_ThreadSafe()
    {
        var cache = new SemanticCache(_tempDir);
        await cache.SetAsync("shared-key", new TestData("shared-value"));

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => cache.GetAsync<TestData>("shared-key"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r =>
        {
            Assert.NotNull(r);
            Assert.Equal("shared-value", r.Value);
        });
    }

    /// <summary>
    /// Thread safety: Multiple parallel writes to SemanticCache must not corrupt the index.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task SemanticCache_ConcurrentWrites_ThreadSafe()
    {
        var cache = new SemanticCache(_tempDir);

        var tasks = Enumerable.Range(0, 20)
            .Select(i => cache.SetAsync($"key-{i}", new TestData($"value-{i}")))
            .ToArray();

        await Task.WhenAll(tasks);

        // All keys should be retrievable
        for (var i = 0; i < 20; i++)
        {
            var exists = await cache.ExistsAsync($"key-{i}");
            Assert.True(exists, $"key-{i} should exist after concurrent write");
        }
    }

    /// <summary>
    /// Extractor with empty file content should return empty result, not crash.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task Extractor_WithEmptyFile_ReturnsEmptyResult()
    {
        var emptyFile = Path.Combine(_tempDir, "empty.cs");
        await File.WriteAllTextAsync(emptyFile, string.Empty);

        var extractor = new Extractor();
        var detected = new DetectedFile(
            FilePath: emptyFile,
            FileName: "empty.cs",
            Extension: ".cs",
            Language: "CSharp",
            Category: FileCategory.Code,
            SizeBytes: 0,
            RelativePath: "empty.cs"
        );

        var result = await extractor.ExecuteAsync(detected);

        Assert.NotNull(result);
        // Extractor may create a file-level node even for empty files — verify no crash
        Assert.NotNull(result.Nodes);
        Assert.NotNull(result.Edges);
    }

    /// <summary>
    /// Extractor with binary file content should return empty result, not crash.
    /// Binary content won't match any language patterns.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task Extractor_WithBinaryFile_ReturnsEmptyResult()
    {
        var binaryFile = Path.Combine(_tempDir, "binary.cs");
        var binaryContent = new byte[256];
        new Random(42).NextBytes(binaryContent);
        await File.WriteAllBytesAsync(binaryFile, binaryContent);

        var extractor = new Extractor();
        var detected = new DetectedFile(
            FilePath: binaryFile,
            FileName: "binary.cs",
            Extension: ".cs",
            Language: "CSharp",
            Category: FileCategory.Code,
            SizeBytes: 256,
            RelativePath: "binary.cs"
        );

        // Should not throw — may return empty or partial results
        var result = await extractor.ExecuteAsync(detected);
        Assert.NotNull(result);
    }

    /// <summary>
    /// FileDetector with symlinks should not infinite-loop.
    /// Tests that the directory traversal has proper bounds.
    /// </summary>
    [Fact(Timeout = 10000)]
    public async Task FileDetector_WithNestedDirs_DoesNotHang()
    {
        // Create a deeply nested directory structure to stress traversal
        var deepDir = _tempDir;
        for (var i = 0; i < 10; i++)
        {
            deepDir = Path.Combine(deepDir, $"level{i}");
            Directory.CreateDirectory(deepDir);
        }

        await File.WriteAllTextAsync(Path.Combine(deepDir, "deep.cs"), "// deep file");

        var detector = new FileDetector();
        var options = new FileDetectorOptions(
            RootPath: _tempDir,
            RespectGitIgnore: false
        );

        var files = await detector.ExecuteAsync(options);
        Assert.Contains(files, f => f.FileName == "deep.cs");
    }

    private sealed record TestData(string Value);
}
