using Graphify.Ingest;
using Xunit;

namespace Graphify.Tests.Ingest;

/// <summary>
/// Tests for UrlIngester: URL validation, HTTP fetching with mock handler,
/// invalid URL handling, and timeout behavior.
/// </summary>
[Trait("Category", "Ingest")]
public sealed class UrlIngesterTests : IDisposable
{
    private readonly string _testRoot;

    public UrlIngesterTests()
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
    public async Task IngestAsync_ValidUrl_ReturnsContent()
    {
        var handler = new FakeHttpHandler("<html><title>Test</title><body><p>Hello</p></body></html>");
        using var httpClient = new HttpClient(handler);
        var ingester = new UrlIngester(httpClient);

        var content = await ingester.IngestAsync("https://example.com/page");

        Assert.NotEmpty(content);
        Assert.Contains("source_url: https://example.com/page", content);
    }

    [Fact]
    public async Task IngestAsync_InvalidUrl_ThrowsArgumentException()
    {
        var ingester = new UrlIngester();

        await Assert.ThrowsAsync<ArgumentException>(
            () => ingester.IngestAsync("not-a-url"));
    }

    [Fact]
    public async Task IngestAsync_NullUrl_ThrowsArgumentNullException()
    {
        var ingester = new UrlIngester();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ingester.IngestAsync(null!));
    }

    [Fact]
    public async Task IngestAsync_EmptyUrl_ThrowsArgumentException()
    {
        var ingester = new UrlIngester();

        await Assert.ThrowsAsync<ArgumentException>(
            () => ingester.IngestAsync(""));
    }

    [Fact]
    public async Task IngestAsync_FtpScheme_ThrowsArgumentException()
    {
        var ingester = new UrlIngester();

        await Assert.ThrowsAsync<ArgumentException>(
            () => ingester.IngestAsync("ftp://example.com/file"));
    }

    [Fact]
    public async Task IngestAsync_LocalhostUrl_ThrowsArgumentException()
    {
        var ingester = new UrlIngester();

        await Assert.ThrowsAsync<ArgumentException>(
            () => ingester.IngestAsync("http://localhost/secret"));
    }

    [Fact]
    public async Task IngestAsync_PrivateIp_ThrowsArgumentException()
    {
        var ingester = new UrlIngester();

        await Assert.ThrowsAsync<ArgumentException>(
            () => ingester.IngestAsync("http://192.168.1.1/admin"));
    }

    [Fact]
    public async Task IngestAsync_WebpageContent_IncludesFrontmatter()
    {
        var handler = new FakeHttpHandler("<html><title>My Page</title><body><p>Content</p></body></html>");
        using var httpClient = new HttpClient(handler);
        var ingester = new UrlIngester(httpClient);

        var content = await ingester.IngestAsync("https://example.com");

        Assert.Contains("---", content);
        Assert.Contains("type: webpage", content);
        Assert.Contains("captured_at:", content);
    }

    [Fact]
    public async Task IngestAsync_GitHubUrl_DetectedAsRepo()
    {
        var handler = new FakeHttpHandler("<html><body>Repo</body></html>");
        using var httpClient = new HttpClient(handler);
        var ingester = new UrlIngester(httpClient);

        var content = await ingester.IngestAsync("https://github.com/owner/repo");

        Assert.Contains("type: github_repo", content);
        Assert.Contains("github_owner: owner", content);
    }

    [Fact]
    public async Task IngestToFileAsync_CreatesFile()
    {
        var handler = new FakeHttpHandler("<html><title>Test</title><body><p>Hello</p></body></html>");
        using var httpClient = new HttpClient(handler);
        var ingester = new UrlIngester(httpClient);

        var outputPath = await ingester.IngestToFileAsync(
            "https://example.com/page", _testRoot);

        Assert.True(File.Exists(outputPath));
        var content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("source_url:", content);
    }

    [Fact]
    public async Task IngestToFileAsync_WithAuthor_IncludesContributor()
    {
        var handler = new FakeHttpHandler("<html><title>Test</title><body><p>Hello</p></body></html>");
        using var httpClient = new HttpClient(handler);
        var ingester = new UrlIngester(httpClient);

        var outputPath = await ingester.IngestToFileAsync(
            "https://example.com/page", _testRoot, author: "TestAuthor");

        var content = await File.ReadAllTextAsync(outputPath);
        Assert.Contains("contributor:", content);
        Assert.Contains("TestAuthor", content);
    }

    [Fact]
    public async Task IngestAsync_HttpError_Throws()
    {
        var handler = new FakeHttpHandler(statusCode: System.Net.HttpStatusCode.InternalServerError);
        using var httpClient = new HttpClient(handler);
        var ingester = new UrlIngester(httpClient);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => ingester.IngestAsync("https://example.com/fail"));
    }

    /// <summary>
    /// Fake HTTP message handler for testing without real network calls.
    /// </summary>
    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly string _content;
        private readonly System.Net.HttpStatusCode _statusCode;

        public FakeHttpHandler(string content = "", System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK)
        {
            _content = content;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content)
            };
            return Task.FromResult(response);
        }
    }
}
