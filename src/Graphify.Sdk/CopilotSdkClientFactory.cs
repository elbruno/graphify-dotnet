using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;

namespace Graphify.Sdk;

/// <summary>
/// Factory for creating IChatClient instances backed by GitHub Copilot SDK.
/// Requires the GitHub Copilot CLI to be installed and authenticated.
/// </summary>
public static class CopilotSdkClientFactory
{
    /// <summary>
    /// Creates an IChatClient connected to GitHub Copilot via the Copilot SDK.
    /// The user must be logged in to GitHub Copilot CLI.
    /// </summary>
    /// <param name="options">Copilot SDK configuration (model selection).</param>
    /// <returns>An IChatClient that talks to GitHub Copilot.</returns>
    public static async Task<IChatClient> CreateAsync(CopilotSdkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var copilotClient = new CopilotClient(new CopilotClientOptions
        {
            UseLoggedInUser = true
        });

        await copilotClient.StartAsync();

        return new CopilotChatClient(copilotClient, options.ModelId, ownsClient: true);
    }
}
