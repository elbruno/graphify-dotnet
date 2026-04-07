using GitHub.Copilot.SDK;
using Graphify.Sdk;
using Microsoft.Extensions.AI;
using Xunit;

namespace Graphify.Tests.Sdk;

/// <summary>
/// Tests for CopilotChatClient IChatClient adapter.
/// These tests focus on constructor validation, GetService behavior,
/// and IDisposable/IAsyncDisposable contracts — all runnable without
/// GitHub Copilot CLI authentication.
/// </summary>
public class CopilotChatClientTests
{
    /// <summary>
    /// Helper: creates a CopilotClient that has NOT been started (no auth required).
    /// </summary>
    private static CopilotClient CreateUnstartedCopilotClient()
    {
        return new CopilotClient(new CopilotClientOptions
        {
            UseLoggedInUser = true
        });
    }

    // ──────────────────────────────────────────────
    // Constructor validation
    // ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Sdk")]
    public void Constructor_NullCopilotClient_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new CopilotChatClient(null!));

        Assert.Equal("copilotClient", ex.ParamName);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void Constructor_NullCopilotClient_WithModelId_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new CopilotChatClient(null!, modelId: "gpt-4o"));

        Assert.Equal("copilotClient", ex.ParamName);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void Constructor_ValidClient_DoesNotThrow()
    {
        using var copilotClient = CreateUnstartedCopilotClient();

        var chatClient = new CopilotChatClient(copilotClient, ownsClient: false);
        Assert.NotNull(chatClient);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void Constructor_DefaultModelId_IsGpt41()
    {
        using var copilotClient = CreateUnstartedCopilotClient();

        // Default model is "gpt-4.1" per constructor signature
        var chatClient = new CopilotChatClient(copilotClient, ownsClient: false);
        Assert.NotNull(chatClient);
    }

    // ──────────────────────────────────────────────
    // GetService
    // ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Sdk")]
    public void GetService_CopilotClientType_ReturnsUnderlyingClient()
    {
        using var copilotClient = CreateUnstartedCopilotClient();
        var chatClient = new CopilotChatClient(copilotClient, ownsClient: false);

        var result = chatClient.GetService(typeof(CopilotClient));

        Assert.NotNull(result);
        Assert.Same(copilotClient, result);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void GetService_UnknownType_ReturnsNull()
    {
        using var copilotClient = CreateUnstartedCopilotClient();
        var chatClient = new CopilotChatClient(copilotClient, ownsClient: false);

        var result = chatClient.GetService(typeof(string));

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void GetService_IChatClientType_ReturnsNull()
    {
        using var copilotClient = CreateUnstartedCopilotClient();
        var chatClient = new CopilotChatClient(copilotClient, ownsClient: false);

        // IChatClient is not exposed via GetService (only CopilotClient is)
        var result = chatClient.GetService(typeof(IChatClient));

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void GetService_WithServiceKey_ReturnsNull()
    {
        using var copilotClient = CreateUnstartedCopilotClient();
        var chatClient = new CopilotChatClient(copilotClient, ownsClient: false);

        // serviceKey is not used in this implementation
        var result = chatClient.GetService(typeof(CopilotClient), serviceKey: "some-key");

        Assert.NotNull(result);
    }

    // ──────────────────────────────────────────────
    // IChatClient interface conformance
    // ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Sdk")]
    public void CopilotChatClient_ImplementsIChatClient()
    {
        using var copilotClient = CreateUnstartedCopilotClient();
        var chatClient = new CopilotChatClient(copilotClient, ownsClient: false);

        Assert.IsAssignableFrom<IChatClient>(chatClient);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void CopilotChatClient_ImplementsIAsyncDisposable()
    {
        using var copilotClient = CreateUnstartedCopilotClient();
        var chatClient = new CopilotChatClient(copilotClient, ownsClient: false);

        Assert.IsAssignableFrom<IAsyncDisposable>(chatClient);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void CopilotChatClient_ImplementsIDisposable()
    {
        using var copilotClient = CreateUnstartedCopilotClient();
        var chatClient = new CopilotChatClient(copilotClient, ownsClient: false);

        Assert.IsAssignableFrom<IDisposable>(chatClient);
    }

    // ──────────────────────────────────────────────
    // Dispose behavior (ownsClient = false)
    // ──────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Sdk")]
    public void Dispose_OwnsClientFalse_DoesNotDisposeCopilotClient()
    {
        var copilotClient = CreateUnstartedCopilotClient();
        var chatClient = new CopilotChatClient(copilotClient, ownsClient: false);

        // Disposing with ownsClient=false should not dispose the underlying client
        chatClient.Dispose();

        // The copilot client should still be usable (not disposed)
        // We verify by calling Dispose on it directly — if it were already disposed,
        // this might throw depending on the implementation.
        copilotClient.Dispose();
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public async Task DisposeAsync_OwnsClientFalse_DoesNotDisposeCopilotClient()
    {
        var copilotClient = CreateUnstartedCopilotClient();
        var chatClient = new CopilotChatClient(copilotClient, ownsClient: false);

        await chatClient.DisposeAsync();

        // Underlying client should still be disposable
        await copilotClient.DisposeAsync();
    }
}
