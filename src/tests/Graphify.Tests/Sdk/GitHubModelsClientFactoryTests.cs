using Graphify.Sdk;
using Xunit;

namespace Graphify.Tests.Sdk;

/// <summary>
/// Tests for GitHubModelsClientFactory: null/empty argument validation
/// and valid options creating a client.
/// </summary>
[Trait("Category", "SDK")]
public sealed class GitHubModelsClientFactoryTests
{
    [Fact]
    public void Create_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => GitHubModelsClientFactory.Create(null!));
    }

    [Fact]
    public void Create_EmptyApiKey_ThrowsArgumentException()
    {
        var options = new CopilotExtractorOptions { ApiKey = "" };

        Assert.Throws<ArgumentException>(() => GitHubModelsClientFactory.Create(options));
    }

    [Fact]
    public void Create_WhitespaceApiKey_ThrowsArgumentException()
    {
        var options = new CopilotExtractorOptions { ApiKey = "   " };

        Assert.Throws<ArgumentException>(() => GitHubModelsClientFactory.Create(options));
    }

    [Fact]
    public void Create_NullApiKey_ThrowsArgumentException()
    {
        var options = new CopilotExtractorOptions { ApiKey = null };

        Assert.Throws<ArgumentException>(() => GitHubModelsClientFactory.Create(options));
    }

    [Fact]
    public void Create_ValidOptions_ReturnsIChatClient()
    {
        var options = new CopilotExtractorOptions
        {
            ApiKey = "test-api-key-12345",
            ModelId = "gpt-4o",
            Endpoint = "https://models.inference.ai.azure.com"
        };

        var client = GitHubModelsClientFactory.Create(options);

        Assert.NotNull(client);
    }
}
