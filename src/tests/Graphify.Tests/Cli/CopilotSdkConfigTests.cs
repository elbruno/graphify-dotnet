using Graphify.Cli.Configuration;
using Xunit;

namespace Graphify.Tests.Cli;

/// <summary>
/// Tests for CopilotSdkConfig, its defaults, its integration into GraphifyConfig,
/// and ChatClientResolver behavior for the CopilotSdk provider.
/// </summary>
public class CopilotSdkConfigTests
{
    // ──────────────────────────────────────────────
    // CopilotSdkConfig default values
    // ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Cli")]
    public void CopilotSdkConfig_DefaultModelId_IsGpt41()
    {
        var config = new CopilotSdkConfig();

        Assert.Equal("gpt-4.1", config.ModelId);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void CopilotSdkConfig_ModelId_CanBeOverridden()
    {
        var config = new CopilotSdkConfig { ModelId = "gpt-4o" };

        Assert.Equal("gpt-4o", config.ModelId);
    }

    [Theory]
    [InlineData("claude-sonnet")]
    [InlineData("gpt-4o-mini")]
    [InlineData("llama3.2")]
    [Trait("Category", "Cli")]
    public void CopilotSdkConfig_ModelId_AcceptsVariousValues(string modelId)
    {
        var config = new CopilotSdkConfig { ModelId = modelId };

        Assert.Equal(modelId, config.ModelId);
    }

    // ──────────────────────────────────────────────
    // GraphifyConfig includes CopilotSdk section
    // ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Cli")]
    public void GraphifyConfig_HasDefaultCopilotSdkConfig()
    {
        var config = new GraphifyConfig();

        Assert.NotNull(config.CopilotSdk);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void GraphifyConfig_CopilotSdk_DefaultModelId_IsGpt41()
    {
        var config = new GraphifyConfig();

        Assert.Equal("gpt-4.1", config.CopilotSdk.ModelId);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void GraphifyConfig_HasAllThreeProviderConfigs()
    {
        var config = new GraphifyConfig();

        Assert.NotNull(config.AzureOpenAI);
        Assert.NotNull(config.Ollama);
        Assert.NotNull(config.CopilotSdk);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void GraphifyConfig_CopilotSdk_CanBeReplaced()
    {
        var config = new GraphifyConfig
        {
            CopilotSdk = new CopilotSdkConfig { ModelId = "claude-sonnet" }
        };

        Assert.Equal("claude-sonnet", config.CopilotSdk.ModelId);
    }

    // ──────────────────────────────────────────────
    // ChatClientResolver — null/empty provider
    // ──────────────────────────────────────────────

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
    public async Task ResolveAsync_NullProvider_ReturnsNull()
    {
        var config = new GraphifyConfig { Provider = null };

        var result = await ChatClientResolver.ResolveAsync(config);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public async Task ResolveAsync_EmptyProvider_ReturnsNull()
    {
        var config = new GraphifyConfig { Provider = "" };

        var result = await ChatClientResolver.ResolveAsync(config);

        Assert.Null(result);
    }

    // ──────────────────────────────────────────────
    // ChatClientResolver — unknown provider
    // ──────────────────────────────────────────────

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
    public async Task ResolveAsync_UnknownProvider_ThrowsInvalidOperationException()
    {
        var config = new GraphifyConfig { Provider = "unknown-provider" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ChatClientResolver.ResolveAsync(config));

        Assert.Contains("Unknown AI provider", ex.Message);
        Assert.Contains("unknown-provider", ex.Message);
    }

    // ──────────────────────────────────────────────
    // ChatClientResolver — error message includes "copilotsdk"
    // ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Cli")]
    public void Resolve_UnknownProvider_ErrorMessageListsCopilotSdk()
    {
        var config = new GraphifyConfig { Provider = "not-a-real-provider" };

        var ex = Assert.Throws<InvalidOperationException>(
            () => ChatClientResolver.Resolve(config));

        Assert.Contains("copilotsdk", ex.Message);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public async Task ResolveAsync_UnknownProvider_ErrorMessageListsCopilotSdk()
    {
        var config = new GraphifyConfig { Provider = "not-a-real-provider" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ChatClientResolver.ResolveAsync(config));

        Assert.Contains("copilotsdk", ex.Message);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Resolve_UnknownProvider_ErrorMessageListsAllProviders()
    {
        var config = new GraphifyConfig { Provider = "bogus" };

        var ex = Assert.Throws<InvalidOperationException>(
            () => ChatClientResolver.Resolve(config));

        // Should mention all three supported providers
        Assert.Contains("azureopenai", ex.Message);
        Assert.Contains("ollama", ex.Message);
        Assert.Contains("copilotsdk", ex.Message);
    }

    // ──────────────────────────────────────────────
    // ChatClientResolver — CopilotSdk provider name is recognized
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData("copilotsdk")]
    [InlineData("CopilotSdk")]
    [InlineData("COPILOTSDK")]
    [Trait("Category", "Cli")]
    public void Resolve_CopilotSdkProvider_IsRecognized_NotUnknown(string providerValue)
    {
        var config = new GraphifyConfig { Provider = providerValue };

        // CopilotSdk provider is recognized (doesn't throw "Unknown AI provider").
        // It will throw a different exception because Copilot CLI isn't available in CI,
        // but crucially the error should NOT be "Unknown AI provider".
        var ex = Record.Exception(() => ChatClientResolver.Resolve(config));

        if (ex != null)
        {
            // If it throws, it should NOT be because the provider is unknown
            Assert.DoesNotContain("Unknown AI provider", ex.Message);
        }
    }
}
