namespace Graphify.Cli.Configuration;

/// <summary>
/// Holds parsed CLI provider arguments for layered configuration override.
/// </summary>
public record CliProviderOptions(
    string? Provider,
    string? Endpoint,
    string? ApiKey,
    string? Model,
    string? Deployment);
