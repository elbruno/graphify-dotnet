using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

namespace Graphify.Sdk;

/// <summary>
/// Factory for creating IChatClient instances configured for Azure OpenAI.
/// </summary>
public static class AzureOpenAIClientFactory
{
    /// <summary>
    /// Creates an IChatClient using Azure OpenAI with API key authentication.
    /// </summary>
    /// <param name="options">Azure OpenAI configuration (endpoint, key, deployment).</param>
    /// <returns>An IChatClient wired to the specified Azure OpenAI deployment.</returns>
    public static IChatClient Create(AzureOpenAIOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var credential = new ApiKeyCredential(options.ApiKey);
        var client = new AzureOpenAIClient(new Uri(options.Endpoint), credential);
        return client.GetChatClient(options.DeploymentName).AsIChatClient();
    }
}
