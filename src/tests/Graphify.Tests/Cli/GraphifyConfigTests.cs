using Graphify.Cli.Configuration;
using Xunit;

namespace Graphify.Tests.Cli;

public class GraphifyConfigTests
{
    [Fact]
    [Trait("Category", "Cli")]
    public void GraphifyConfig_Provider_IsNullByDefault()
    {
        var config = new GraphifyConfig();
        Assert.Null(config.Provider);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void GraphifyConfig_HasDefaultOllamaAndAzureSubConfigs()
    {
        var config = new GraphifyConfig();
        Assert.NotNull(config.Ollama);
        Assert.NotNull(config.AzureOpenAI);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void OllamaConfig_DefaultEndpoint_IsLocalhost()
    {
        var config = new OllamaConfig();
        Assert.Equal("http://localhost:11434", config.Endpoint);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void OllamaConfig_DefaultModelId_IsLlama32()
    {
        var config = new OllamaConfig();
        Assert.Equal("llama3.2", config.ModelId);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void AzureOpenAIConfig_AllProperties_AreNullByDefault()
    {
        var config = new AzureOpenAIConfig();
        Assert.Null(config.Endpoint);
        Assert.Null(config.ApiKey);
        Assert.Null(config.DeploymentName);
        Assert.Null(config.ModelId);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void GraphifyConfig_Provider_CanBeSet()
    {
        var config = new GraphifyConfig { Provider = "ollama" };
        Assert.Equal("ollama", config.Provider);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void OllamaConfig_Properties_CanBeOverridden()
    {
        var config = new OllamaConfig
        {
            Endpoint = "http://remote:11434",
            ModelId = "codellama"
        };
        Assert.Equal("http://remote:11434", config.Endpoint);
        Assert.Equal("codellama", config.ModelId);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void AzureOpenAIConfig_Properties_CanBeSet()
    {
        var config = new AzureOpenAIConfig
        {
            Endpoint = "https://myinstance.openai.azure.com/",
            ApiKey = "test-key",
            DeploymentName = "gpt-4o",
            ModelId = "gpt-4o"
        };
        Assert.Equal("https://myinstance.openai.azure.com/", config.Endpoint);
        Assert.Equal("test-key", config.ApiKey);
        Assert.Equal("gpt-4o", config.DeploymentName);
        Assert.Equal("gpt-4o", config.ModelId);
    }
}
