using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Graphify.Sdk;

/// <summary>
/// Factory for creating IChatClient instances configured for GitHub Models API.
/// GitHub Models uses an OpenAI-compatible API format.
/// </summary>
public static class GitHubModelsClientFactory
{
    /// <summary>
    /// Creates an IChatClient configured to use GitHub Models API endpoint.
    /// </summary>
    /// <param name="options">Configuration options for the client.</param>
    /// <returns>An IChatClient instance ready to use with GitHub Models.</returns>
    /// <exception cref="ArgumentNullException">Thrown when options or ApiKey is null.</exception>
    public static IChatClient Create(CopilotExtractorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException("ApiKey is required for GitHub Models API access.", nameof(options));
        }

        var credential = new ApiKeyCredential(options.ApiKey);
        var clientOptions = new OpenAIClientOptions { Endpoint = new Uri(options.Endpoint) };
        var client = new OpenAIClient(credential, clientOptions);
        return client.GetChatClient(options.ModelId).AsIChatClient();
    }
}
