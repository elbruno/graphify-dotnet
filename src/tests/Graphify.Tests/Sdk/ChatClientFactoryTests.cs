using Graphify.Sdk;
using Xunit;

namespace Graphify.Tests.Sdk;

/// <summary>
/// Tests for ChatClientFactory, AiProvider enum, and AiProviderOptions.
/// </summary>
public class ChatClientFactoryTests
{
    // ──────────────────────────────────────────────
    // AiProvider enum contract tests (now using real types)
    // ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Sdk")]
    public void AiProvider_HasExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(AiProvider), AiProvider.AzureOpenAI));
        Assert.True(Enum.IsDefined(typeof(AiProvider), AiProvider.Ollama));
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void AiProvider_HasExactlyTwoValues()
    {
        var values = Enum.GetValues<AiProvider>();
        Assert.Equal(2, values.Length);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void AiProviderOptions_AzureOpenAI_ConstructionWorks()
    {
        var options = new AiProviderOptions(
            Provider: AiProvider.AzureOpenAI,
            ApiKey: "azure-key",
            Endpoint: "https://myinstance.openai.azure.com/",
            DeploymentName: "gpt-4o");

        Assert.Equal(AiProvider.AzureOpenAI, options.Provider);
        Assert.Equal("azure-key", options.ApiKey);
        Assert.Equal("https://myinstance.openai.azure.com/", options.Endpoint);
        Assert.Equal("gpt-4o", options.DeploymentName);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void AiProviderOptions_Ollama_ConstructionWorks()
    {
        var options = new AiProviderOptions(
            Provider: AiProvider.Ollama,
            Endpoint: "http://localhost:11434",
            ModelId: "llama3.2");

        Assert.Equal(AiProvider.Ollama, options.Provider);
        Assert.Equal("http://localhost:11434", options.Endpoint);
        Assert.Equal("llama3.2", options.ModelId);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void AiProviderOptions_RecordEquality_WorksCorrectly()
    {
        var a = new AiProviderOptions(Provider: AiProvider.Ollama, ModelId: "llama3.2");
        var b = new AiProviderOptions(Provider: AiProvider.Ollama, ModelId: "llama3.2");

        Assert.Equal(a, b);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void AiProviderOptions_DefaultValues_AreNull()
    {
        var options = new AiProviderOptions(Provider: AiProvider.AzureOpenAI);

        Assert.Null(options.ApiKey);
        Assert.Null(options.Endpoint);
        Assert.Null(options.ModelId);
        Assert.Null(options.DeploymentName);
    }

    // ──────────────────────────────────────────────
    // ChatClientFactory.Create tests
    // ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Sdk")]
    public void Create_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ChatClientFactory.Create(null!));
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void Create_UnknownProvider_ThrowsArgumentException()
    {
        var options = new AiProviderOptions(
            Provider: (AiProvider)999);

        Assert.Throws<ArgumentException>(() =>
            ChatClientFactory.Create(options));
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void Create_Ollama_WithDefaults_ReturnsClient()
    {
        var options = new AiProviderOptions(Provider: AiProvider.Ollama);

        var client = ChatClientFactory.Create(options);
        Assert.NotNull(client);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void Create_AzureOpenAI_MissingEndpoint_Throws()
    {
        var options = new AiProviderOptions(
            Provider: AiProvider.AzureOpenAI,
            ApiKey: "key",
            DeploymentName: "deploy");

        Assert.Throws<ArgumentException>(() =>
            ChatClientFactory.Create(options));
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void Create_AzureOpenAI_MissingApiKey_Throws()
    {
        var options = new AiProviderOptions(
            Provider: AiProvider.AzureOpenAI,
            Endpoint: "https://test.openai.azure.com/",
            DeploymentName: "deploy");

        Assert.Throws<ArgumentException>(() =>
            ChatClientFactory.Create(options));
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void Create_AzureOpenAI_MissingDeploymentName_Throws()
    {
        var options = new AiProviderOptions(
            Provider: AiProvider.AzureOpenAI,
            Endpoint: "https://test.openai.azure.com/",
            ApiKey: "key");

        Assert.Throws<ArgumentException>(() =>
            ChatClientFactory.Create(options));
    }
}
