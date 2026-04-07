using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;

namespace Graphify.Sdk;

/// <summary>
/// IChatClient adapter that wraps GitHub Copilot SDK's CopilotClient.
/// Bridges the Copilot SDK session-based API to the standard IChatClient interface
/// used throughout the graphify pipeline.
/// </summary>
public sealed class CopilotChatClient : IChatClient, IAsyncDisposable
{
    private readonly CopilotClient _copilotClient;
    private readonly string _modelId;
    private readonly bool _ownsClient;

    /// <summary>
    /// Creates a new CopilotChatClient wrapping the given CopilotClient.
    /// </summary>
    /// <param name="copilotClient">An initialized CopilotClient (must have StartAsync called).</param>
    /// <param name="modelId">Model to use for chat completions.</param>
    /// <param name="ownsClient">If true, disposes the CopilotClient when this adapter is disposed.</param>
    public CopilotChatClient(CopilotClient copilotClient, string modelId = "gpt-4.1", bool ownsClient = true)
    {
        _copilotClient = copilotClient ?? throw new ArgumentNullException(nameof(copilotClient));
        _modelId = modelId;
        _ownsClient = ownsClient;
    }

    /// <inheritdoc />
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var model = options?.ModelId ?? _modelId;

        var session = await _copilotClient.CreateSessionAsync(
            new SessionConfig
            {
                Model = model,
                OnPermissionRequest = PermissionHandler.ApproveAll
            },
            cancellationToken);

        // Build the prompt from messages
        var prompt = BuildPrompt(chatMessages);

        // Send and wait for response
        var response = await session.SendAndWaitAsync(
            new MessageOptions { Prompt = prompt },
            timeout: null,
            cancellationToken);

        var responseText = response?.Data?.Content ?? string.Empty;

        return new ChatResponse(new List<ChatMessage>
        {
            new(ChatRole.Assistant, responseText)
        });
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // For streaming, use the non-streaming path and yield the full response as a single update.
        var response = await GetResponseAsync(chatMessages, options, cancellationToken);
        var text = response.Messages.FirstOrDefault()?.Text ?? string.Empty;

        yield return new ChatResponseUpdate(ChatRole.Assistant, text);
    }

    /// <inheritdoc />
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(CopilotClient))
            return _copilotClient;

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsClient)
        {
            _copilotClient.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_ownsClient)
        {
            await _copilotClient.DisposeAsync();
        }
    }

    private static string BuildPrompt(IEnumerable<ChatMessage> messages)
    {
        var messageList = messages.ToList();
        if (messageList.Count == 1)
            return messageList[0].Text ?? string.Empty;

        // For multi-message conversations, format with role prefixes
        var parts = new List<string>();
        foreach (var msg in messageList)
        {
            var role = msg.Role == ChatRole.System ? "System"
                     : msg.Role == ChatRole.User ? "User"
                     : msg.Role == ChatRole.Assistant ? "Assistant"
                     : msg.Role.Value;

            parts.Add($"{role}: {msg.Text}");
        }
        return string.Join("\n\n", parts);
    }
}
