using Graphify.Sdk;
using Xunit;

namespace Graphify.Tests.Sdk;

/// <summary>
/// Tests for CopilotSdkClientFactory.
/// Only tests that can run WITHOUT GitHub Copilot CLI authentication are included.
/// Actual client creation (CreateAsync with valid options) requires Copilot CLI
/// and is covered in integration tests.
/// </summary>
public class CopilotSdkClientFactoryTests
{
    [Fact]
    [Trait("Category", "Sdk")]
    public async Task CreateAsync_NullOptions_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CopilotSdkClientFactory.CreateAsync(null!));
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void Options_DefaultConstruction_HasExpectedModelId()
    {
        var options = new CopilotSdkOptions();

        Assert.Equal("gpt-4.1", options.ModelId);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void Options_CustomModelId_IsPreserved()
    {
        var options = new CopilotSdkOptions(ModelId: "claude-sonnet");

        Assert.Equal("claude-sonnet", options.ModelId);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void ChatClientFactory_CopilotSdk_SyncCreate_ThrowsInvalidOperationException()
    {
        var options = new AiProviderOptions(Provider: AiProvider.CopilotSdk);

        var ex = Assert.Throws<InvalidOperationException>(
            () => ChatClientFactory.Create(options));

        Assert.Contains("CopilotSdk", ex.Message);
        Assert.Contains("CreateAsync", ex.Message);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public async Task ChatClientFactory_CreateAsync_NullOptions_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => ChatClientFactory.CreateAsync(null!));
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void AiProviderOptions_CopilotSdk_DefaultModelIdIsNull()
    {
        var options = new AiProviderOptions(Provider: AiProvider.CopilotSdk);

        Assert.Equal(AiProvider.CopilotSdk, options.Provider);
        Assert.Null(options.ModelId);
        Assert.Null(options.Endpoint);
        Assert.Null(options.ApiKey);
        Assert.Null(options.DeploymentName);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void AiProviderOptions_CopilotSdk_WithModelId_IsPreserved()
    {
        var options = new AiProviderOptions(
            Provider: AiProvider.CopilotSdk,
            ModelId: "gpt-4.1");

        Assert.Equal("gpt-4.1", options.ModelId);
    }
}
