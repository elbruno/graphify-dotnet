using Microsoft.Extensions.AI;
using OllamaSharp;

namespace Graphify.Sdk;

/// <summary>
/// Factory for creating IChatClient instances backed by a local Ollama server.
/// OllamaSharp's OllamaApiClient implements IChatClient natively.
/// </summary>
public static class OllamaClientFactory
{
    /// <summary>
    /// Creates an IChatClient connected to an Ollama instance.
    /// </summary>
    /// <param name="options">Ollama configuration (endpoint, model).</param>
    /// <returns>An IChatClient that talks to the local Ollama server.</returns>
    public static IChatClient Create(OllamaOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new OllamaApiClient(new Uri(options.Endpoint), options.ModelId);
    }
}
