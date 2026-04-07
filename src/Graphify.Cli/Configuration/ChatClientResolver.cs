using Graphify.Sdk;
using Microsoft.Extensions.AI;

namespace Graphify.Cli.Configuration;

/// <summary>
/// Resolves a GraphifyConfig into an IChatClient (or null if no provider is set).
/// </summary>
public static class ChatClientResolver
{
    public static IChatClient? Resolve(GraphifyConfig config)
    {
        if (string.IsNullOrEmpty(config.Provider))
            return null;

        if (!Enum.TryParse<AiProvider>(config.Provider, ignoreCase: true, out var provider))
            throw new InvalidOperationException(
                $"Unknown AI provider: '{config.Provider}'. Supported: azureopenai, ollama");

        var options = provider switch
        {
            AiProvider.AzureOpenAI => new AiProviderOptions(
                Provider: AiProvider.AzureOpenAI,
                Endpoint: config.AzureOpenAI.Endpoint,
                ApiKey: config.AzureOpenAI.ApiKey,
                ModelId: config.AzureOpenAI.ModelId,
                DeploymentName: config.AzureOpenAI.DeploymentName),

            AiProvider.Ollama => new AiProviderOptions(
                Provider: AiProvider.Ollama,
                Endpoint: config.Ollama.Endpoint,
                ModelId: config.Ollama.ModelId),

            _ => throw new InvalidOperationException($"Unsupported provider: {provider}")
        };

        return ChatClientFactory.Create(options);
    }
}
