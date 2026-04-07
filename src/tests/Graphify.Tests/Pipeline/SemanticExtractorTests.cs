using Graphify.Models;
using Graphify.Pipeline;
using Microsoft.Extensions.AI;
using Xunit;

namespace Graphify.Tests.Pipeline;

/// <summary>
/// Tests for SemanticExtractor: mock AI client integration, graceful degradation
/// without client, file category filtering, cancellation, and error handling.
/// </summary>
[Trait("Category", "Pipeline")]
public sealed class SemanticExtractorTests
{
    [Fact]
    public async Task ExecuteAsync_WithNullClient_ReturnsEmptyResult()
    {
        var extractor = new SemanticExtractor(null);
        var file = CreateTestFile();

        var result = await extractor.ExecuteAsync(file);

        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
        Assert.Equal(ExtractionMethod.Semantic, result.Method);
    }

    [Fact]
    public async Task ExecuteAsync_WithMockClient_ReturnsParsedNodesAndEdges()
    {
        var mockResponse = """
        {
            "nodes": [
                { "id": "class_a", "label": "ClassA", "type": "Code" }
            ],
            "edges": [
                { "source": "class_a", "target": "class_b", "relation": "uses", "confidence": "INFERRED", "weight": 0.8 }
            ]
        }
        """;
        var client = new FakeChatClient(mockResponse);
        var extractor = new SemanticExtractor(client);
        var file = CreateTestFile();

        var result = await extractor.ExecuteAsync(file);

        Assert.Single(result.Nodes);
        Assert.Equal("class_a", result.Nodes[0].Id);
        Assert.Single(result.Edges);
        Assert.Equal("uses", result.Edges[0].Relation);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesAiResponseParsingFailure()
    {
        var client = new FakeChatClient("This is not JSON at all!");
        var extractor = new SemanticExtractor(client);
        var file = CreateTestFile();

        // Should not throw — returns empty result on parse failure
        var result = await extractor.ExecuteAsync(file);

        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
    }

    [Fact]
    public async Task ExecuteAsync_RespectsCancellationToken()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var client = new FakeChatClient("{}");
        var extractor = new SemanticExtractor(client);
        var file = CreateTestFile();

        // Cancelled token should cause graceful empty result (exception caught internally)
        var result = await extractor.ExecuteAsync(file, cts.Token);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_FileSizeExceedsLimit_ReturnsEmpty()
    {
        var client = new FakeChatClient("{}");
        var options = new SemanticExtractorOptions { MaxFileSizeBytes = 10 };
        var extractor = new SemanticExtractor(client, options);
        var file = CreateTestFile(sizeBytes: 1000);

        var result = await extractor.ExecuteAsync(file);

        Assert.Empty(result.Nodes);
    }

    [Fact]
    public async Task ExecuteAsync_DocumentationCategory_Processed()
    {
        var mockResponse = """{ "nodes": [{ "id": "doc", "label": "ReadMe", "type": "Document" }], "edges": [] }""";
        var client = new FakeChatClient(mockResponse);
        var extractor = new SemanticExtractor(client);
        var file = new DetectedFile(
            CreateTempFile("# README"),
            "README.md", ".md", "Markdown", FileCategory.Documentation, 100, "README.md");

        var result = await extractor.ExecuteAsync(file);

        Assert.Single(result.Nodes);
    }

    [Fact]
    public async Task ExecuteAsync_DisabledCategory_ReturnsEmpty()
    {
        var client = new FakeChatClient("{}");
        var options = new SemanticExtractorOptions { ExtractFromCode = false };
        var extractor = new SemanticExtractor(client, options);
        var file = CreateTestFile();

        var result = await extractor.ExecuteAsync(file);

        Assert.Empty(result.Nodes);
    }

    [Fact]
    public async Task ExecuteAsync_JsonInMarkdownCodeBlock_ParsedCorrectly()
    {
        var mockResponse = """
        ```json
        { "nodes": [{ "id": "x", "label": "X", "type": "Code" }], "edges": [] }
        ```
        """;
        var client = new FakeChatClient(mockResponse);
        var extractor = new SemanticExtractor(client);
        var file = CreateTestFile();

        var result = await extractor.ExecuteAsync(file);

        Assert.Single(result.Nodes);
    }

    [Fact]
    public async Task ExecuteAsync_SourceFilePathPreserved()
    {
        var client = new FakeChatClient("""{ "nodes": [], "edges": [] }""");
        var extractor = new SemanticExtractor(client);
        var file = CreateTestFile();

        var result = await extractor.ExecuteAsync(file);

        Assert.Equal(file.FilePath, result.SourceFilePath);
    }

    private static DetectedFile CreateTestFile(long sizeBytes = 100)
    {
        var tempFile = CreateTempFile("public class Foo {}");
        return new DetectedFile(tempFile, "test.cs", ".cs", "CSharp", FileCategory.Code, sizeBytes, "test.cs");
    }

    private static string CreateTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".cs");
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Minimal IChatClient implementation for testing.
    /// Returns a fixed response for any request.
    /// </summary>
    private sealed class FakeChatClient : IChatClient
    {
        private readonly string _response;

        public FakeChatClient(string response)
        {
            _response = response;
        }

        public void Dispose() { }

        public ChatClientMetadata Metadata => new();

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, _response));
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
    }
}
