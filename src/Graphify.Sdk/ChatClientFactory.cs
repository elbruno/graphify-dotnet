using Microsoft.Extensions.AI;

namespace Graphify.Sdk;

/// <summary>
/// Supported AI providers.
/// </summary>
public enum AiProvider
{
    AzureOpenAI,
    Ollama,
    CopilotSdk
}

/// <summary>
/// Unified configuration for any AI provider. Supply the fields relevant to your chosen provider.
/// </summary>
/// <param name="Provider">Which backend to use.</param>
/// <param name="Endpoint">Service endpoint URL (required for AzureOpenAI; optional override for Ollama).</param>
/// <param name="ApiKey">API key (required for AzureOpenAI; not needed for Ollama).</param>
/// <param name="ModelId">Model identifier, e.g. "gpt-4o" or "llama3.2".</param>
/// <param name="DeploymentName">Azure OpenAI deployment name (only used by AzureOpenAI provider).</param>
public record AiProviderOptions(
    AiProvider Provider,
    string? Endpoint = null,
    string? ApiKey = null,
    string? ModelId = null,
    string? DeploymentName = null
);

/// <summary>
/// Unified factory that creates the correct IChatClient for any supported AI provider.
/// </summary>
public static class ChatClientFactory
{
    /// <summary>
    /// Creates an IChatClient configured for the specified provider.
    /// For synchronous providers (AzureOpenAI, Ollama). Use CreateAsync for CopilotSdk.
    /// </summary>
    public static IChatClient Create(AiProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Provider switch
        {
            AiProvider.AzureOpenAI => AzureOpenAIClientFactory.Create(
                new AzureOpenAIOptions(
                    Endpoint: options.Endpoint ?? throw new ArgumentException("Endpoint is required for Azure OpenAI.", nameof(options)),
                    ApiKey: options.ApiKey ?? throw new ArgumentException("ApiKey is required for Azure OpenAI.", nameof(options)),
                    DeploymentName: options.DeploymentName ?? throw new ArgumentException("DeploymentName is required for Azure OpenAI.", nameof(options)),
                    ModelId: options.ModelId)),

            AiProvider.Ollama => OllamaClientFactory.Create(
                new OllamaOptions(
                    Endpoint: options.Endpoint ?? "http://localhost:11434",
                    ModelId: options.ModelId ?? "llama3.2")),

            AiProvider.CopilotSdk => throw new InvalidOperationException(
                "CopilotSdk requires async initialization. Use ChatClientFactory.CreateAsync() instead."),

            _ => throw new ArgumentException($"Unknown AI provider: {options.Provider}", nameof(options))
        };
    }

    /// <summary>
    /// Creates an IChatClient configured for the specified provider (async version).
    /// Required for CopilotSdk; also works for other providers.
    /// </summary>
    public static async Task<IChatClient> CreateAsync(AiProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Provider == AiProvider.CopilotSdk)
        {
            return await CopilotSdkClientFactory.CreateAsync(
                new CopilotSdkOptions(
                    ModelId: options.ModelId ?? "gpt-4.1"));
        }

        // For non-async providers, delegate to sync factory
        return Create(options);
    }
}
