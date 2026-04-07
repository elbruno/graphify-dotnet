namespace Graphify.Sdk;

/// <summary>
/// Configuration options for the GitHub Copilot SDK provider.
/// Authentication is handled by the GitHub Copilot CLI (user must be logged in).
/// </summary>
/// <param name="ModelId">Model to use (e.g., "gpt-4.1", "gpt-4o", "claude-sonnet").</param>
public record CopilotSdkOptions(
    string ModelId = "gpt-4.1"
);
