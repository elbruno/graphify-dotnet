using Graphify.Cli.Configuration;
using Xunit;

namespace Graphify.Tests.Cli;

public class ChatClientResolverTests
{
    [Fact]
    [Trait("Category", "Cli")]
    public void Resolve_NullProvider_ReturnsNull()
    {
        var config = new GraphifyConfig { Provider = null };
        var result = ChatClientResolver.Resolve(config);
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Resolve_EmptyProvider_ReturnsNull()
    {
        var config = new GraphifyConfig { Provider = "" };
        var result = ChatClientResolver.Resolve(config);
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Resolve_UnknownProvider_ThrowsInvalidOperationException()
    {
        var config = new GraphifyConfig { Provider = "unknown-provider" };

        var ex = Assert.Throws<InvalidOperationException>(
            () => ChatClientResolver.Resolve(config));
        Assert.Contains("Unknown AI provider", ex.Message);
        Assert.Contains("unknown-provider", ex.Message);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Resolve_OllamaProvider_ReturnsNonNullClient()
    {
        var config = new GraphifyConfig { Provider = "ollama" };
        // Uses default Ollama endpoint/model; no network call in constructor
        var result = ChatClientResolver.Resolve(config);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("ollama")]
    [InlineData("Ollama")]
    [InlineData("OLLAMA")]
    [Trait("Category", "Cli")]
    public void Resolve_OllamaProvider_IsCaseInsensitive(string providerValue)
    {
        var config = new GraphifyConfig { Provider = providerValue };
        var result = ChatClientResolver.Resolve(config);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("azureopenai")]
    [InlineData("AzureOpenAI")]
    [InlineData("AZUREOPENAI")]
    [Trait("Category", "Cli")]
    public void Resolve_AzureOpenAIProvider_IsCaseInsensitive(string providerValue)
    {
        // All casings should be recognized as AzureOpenAI (though it will throw
        // because required fields are missing — that's the expected behavior)
        var config = new GraphifyConfig { Provider = providerValue };

        Assert.Throws<ArgumentException>(
            () => ChatClientResolver.Resolve(config));
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Resolve_AzureOpenAI_MissingEndpoint_Throws()
    {
        var config = new GraphifyConfig
        {
            Provider = "azureopenai",
            AzureOpenAI = new AzureOpenAIConfig
            {
                ApiKey = "key",
                DeploymentName = "deploy"
            }
        };

        var ex = Assert.Throws<ArgumentException>(
            () => ChatClientResolver.Resolve(config));
        Assert.Contains("Endpoint", ex.Message);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Resolve_AzureOpenAI_MissingApiKey_Throws()
    {
        var config = new GraphifyConfig
        {
            Provider = "azureopenai",
            AzureOpenAI = new AzureOpenAIConfig
            {
                Endpoint = "https://myinstance.openai.azure.com/",
                DeploymentName = "deploy"
            }
        };

        var ex = Assert.Throws<ArgumentException>(
            () => ChatClientResolver.Resolve(config));
        Assert.Contains("ApiKey", ex.Message);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Resolve_AzureOpenAI_MissingDeploymentName_Throws()
    {
        var config = new GraphifyConfig
        {
            Provider = "azureopenai",
            AzureOpenAI = new AzureOpenAIConfig
            {
                Endpoint = "https://myinstance.openai.azure.com/",
                ApiKey = "key"
            }
        };

        var ex = Assert.Throws<ArgumentException>(
            () => ChatClientResolver.Resolve(config));
        Assert.Contains("DeploymentName", ex.Message);
    }
}
