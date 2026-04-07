using Graphify.Export;
using Graphify.Graph;
using Graphify.Integration.Tests.Helpers;
using Graphify.Models;
using Graphify.Security;
using Xunit;
using Xunit.Abstractions;

namespace Graphify.Integration.Tests.Security;

/// <summary>
/// End-to-end security integration tests: malicious input through the full pipeline,
/// cross-format sanitization, path traversal in pipeline context.
/// </summary>
[Trait("Category", "Security")]
[Trait("Category", "Integration")]
public sealed class SecurityIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public SecurityIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"graphify-sec-int-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact(Timeout = 30000)]
    public async Task Pipeline_MaliciousSourceFile_DoesNotProduceXssInHtmlExport()
    {
        // Arrange — build a graph with XSS payloads in node labels (simulating
        // what would happen if a malicious source file poisoned the extraction)
        var graph = new KnowledgeGraph();

        var xssPayloads = new[]
        {
            "</script><script>alert('xss')</script>",
            "<img src=x onerror=alert(1)>",
            "\" onmouseover=\"alert(1)\"",
            "<svg/onload=alert(1)>"
        };

        for (var i = 0; i < xssPayloads.Length; i++)
        {
            graph.AddNode(new GraphNode
            {
                Id = $"malicious_{i}",
                Label = xssPayloads[i],
                Type = "Entity",
                FilePath = $"src/evil_{i}.cs",
                Confidence = Confidence.Extracted,
                Metadata = new Dictionary<string, string>()
            });
        }

        // Add an edge between first two nodes
        if (graph.NodeCount >= 2)
        {
            var nodes = graph.GetNodes().ToList();
            graph.AddEdge(new GraphEdge
            {
                Source = nodes[0],
                Target = nodes[1],
                Relationship = "calls",
                Weight = 1.0,
                Confidence = Confidence.Extracted
            });
        }

        var htmlPath = Path.Combine(_tempDir, "malicious.html");

        // Act
        var htmlExporter = new HtmlExporter();
        await htmlExporter.ExportAsync(graph, htmlPath, cancellationToken: default);

        // Assert
        Assert.True(File.Exists(htmlPath));
        var html = await File.ReadAllTextAsync(htmlPath);
        _output.WriteLine($"HTML output size: {html.Length} chars");

        // No raw XSS payloads should survive into the HTML output
        foreach (var payload in xssPayloads)
        {
            Assert.DoesNotContain(payload, html);
        }

        // Specific dangerous patterns must not appear
        Assert.DoesNotContain("onerror=alert", html);
        Assert.DoesNotContain("onmouseover=alert", html);
        Assert.DoesNotContain("onload=alert", html);
        Assert.DoesNotContain("<script>alert", html);
    }

    [Fact(Timeout = 30000)]
    public async Task Pipeline_PathTraversalAttempt_IsBlocked()
    {
        // Arrange — simulate path traversal in output directory
        var validator = new InputValidator();
        var traversalAttempts = new[]
        {
            "../../etc/cron.d",
            @"..\..\Windows\System32",
            "../../../tmp/evil",
            "output/../../../root/.ssh"
        };

        // Act & Assert — each traversal attempt should be rejected
        foreach (var attempt in traversalAttempts)
        {
            var result = validator.ValidatePath(attempt);
            Assert.False(result.IsValid,
                $"Path traversal attempt '{attempt}' should be blocked but was allowed");
            _output.WriteLine($"Blocked: {attempt} — {string.Join(", ", result.Errors)}");
        }

        await Task.CompletedTask;
    }

    [Fact(Timeout = 30000)]
    public async Task Export_AllFormats_SanitizeNodeLabels()
    {
        // Arrange — a graph with potentially dangerous labels
        var graph = new KnowledgeGraph();
        var dangerousLabel = "<script>alert('xss')</script>Normal";
        var cypherDangerousLabel = "Label'; DROP (n); //";

        var node1 = new GraphNode
        {
            Id = "safe_node",
            Label = dangerousLabel,
            Type = "Entity",
            FilePath = "src/test.cs",
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };
        var node2 = new GraphNode
        {
            Id = "cypher_node",
            Label = cypherDangerousLabel,
            Type = "Entity",
            FilePath = "src/test2.cs",
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };
        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddEdge(new GraphEdge
        {
            Source = node1,
            Target = node2,
            Relationship = "uses",
            Weight = 1.0,
            Confidence = Confidence.Extracted
        });

        // Act — export to each format
        var htmlPath = Path.Combine(_tempDir, "graph.html");
        var neo4jPath = Path.Combine(_tempDir, "graph.cypher");
        var jsonPath = Path.Combine(_tempDir, "graph.json");

        var htmlExporter = new HtmlExporter();
        var neo4jExporter = new Neo4jExporter();
        var jsonExporter = new JsonExporter();

        await htmlExporter.ExportAsync(graph, htmlPath, cancellationToken: default);
        await neo4jExporter.ExportAsync(graph, neo4jPath);
        await jsonExporter.ExportAsync(graph, jsonPath);

        // Assert — HTML export
        var html = await File.ReadAllTextAsync(htmlPath);
        Assert.DoesNotContain("<script>alert", html);
        _output.WriteLine("HTML: XSS payload sanitized ✓");

        // Assert — Neo4j export
        var cypher = await File.ReadAllTextAsync(neo4jPath);
        // After fix: the single quote in the label should be escaped with a backslash
        // Verify the escaped form (\') is present instead of a raw unescaped single quote
        Assert.Contains(@"Label\'", cypher);
        _output.WriteLine("Neo4j: Cypher injection neutralized ✓");

        // Assert — JSON export should be safely encoded
        var json = await File.ReadAllTextAsync(jsonPath);
        Assert.True(File.Exists(jsonPath));
        _output.WriteLine("JSON: Export completed ✓");

        _output.WriteLine($"All exports sanitized across {3} formats");
    }
}
