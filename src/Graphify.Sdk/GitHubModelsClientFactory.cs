using Microsoft.Extensions.AI;

namespace Graphify.Sdk;

/// <summary>
/// Factory for creating IChatClient instances configured for GitHub Models API.
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

        // Note: Microsoft.Extensions.AI.OpenAI package is required for OpenAI-compatible endpoints
        // This creates a client using the OpenAI-compatible API format that GitHub Models supports
        // The actual implementation would use Microsoft.Extensions.AI.OpenAI.OpenAIChatClient
        // but we're keeping this as a placeholder factory for now since the exact package
        // reference needs to be added to the project.
        
        // TODO: Add Microsoft.Extensions.AI.OpenAI package reference and implement:
        // return new OpenAIChatClient(
        //     new OpenAIClientOptions 
        //     { 
        //         Endpoint = new Uri(options.Endpoint),
        //         ApiKey = options.ApiKey
        //     },
        //     model: options.ModelId
        // ).AsChatClient();

        throw new NotImplementedException(
            "GitHub Models client creation requires Microsoft.Extensions.AI.OpenAI package. " +
            "Add the package reference and implement OpenAIChatClient configuration.");
    }
}
