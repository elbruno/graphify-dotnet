using Microsoft.Extensions.Configuration;

namespace Graphify.Cli.Configuration;

/// <summary>
/// Builds a layered IConfiguration:
///   appsettings.json → appsettings.local.json → env vars (GRAPHIFY__*) → user secrets → CLI overrides
/// </summary>
public static class ConfigurationFactory
{
    public static IConfiguration Build(
        CliProviderOptions? cliOptions = null,
        CliSurrealOptions? surrealOptions = null)
    {
        var builder = new ConfigurationBuilder();

        // Layer 1: appsettings.json defaults
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        builder.AddJsonFile(appSettingsPath, optional: true, reloadOnChange: false);

        // Layer 2: appsettings.local.json (wizard-saved config)
        var localConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
        builder.AddJsonFile(localConfigPath, optional: true, reloadOnChange: false);

        // Layer 3: Environment variables (GRAPHIFY__Provider → GRAPHIFY:Provider; case-insensitive section match)
        builder.AddEnvironmentVariables();

        // Layer 4: User secrets
        builder.AddUserSecrets<Program>(optional: true);

        // Layer 5: CLI argument overrides (highest priority)
        var overrides = new Dictionary<string, string?>();

        if (cliOptions != null)
        {
            if (cliOptions.Provider != null)
                overrides["Graphify:Provider"] = cliOptions.Provider;

            if (cliOptions.Endpoint != null)
            {
                if (cliOptions.Provider?.Equals("ollama", StringComparison.OrdinalIgnoreCase) == true)
                    overrides["Graphify:Ollama:Endpoint"] = cliOptions.Endpoint;
                else if (cliOptions.Provider?.Equals("copilotsdk", StringComparison.OrdinalIgnoreCase) != true)
                    overrides["Graphify:AzureOpenAI:Endpoint"] = cliOptions.Endpoint;
            }

            if (cliOptions.ApiKey != null)
                overrides["Graphify:AzureOpenAI:ApiKey"] = cliOptions.ApiKey;

            if (cliOptions.Model != null)
            {
                if (cliOptions.Provider?.Equals("ollama", StringComparison.OrdinalIgnoreCase) == true)
                    overrides["Graphify:Ollama:ModelId"] = cliOptions.Model;
                else if (cliOptions.Provider?.Equals("copilotsdk", StringComparison.OrdinalIgnoreCase) == true)
                    overrides["Graphify:CopilotSdk:ModelId"] = cliOptions.Model;
                else
                    overrides["Graphify:AzureOpenAI:ModelId"] = cliOptions.Model;
            }

            if (cliOptions.Deployment != null)
                overrides["Graphify:AzureOpenAI:DeploymentName"] = cliOptions.Deployment;
        }

        if (surrealOptions != null)
        {
            if (surrealOptions.Endpoint != null)
                overrides["Graphify:SurrealDb:Endpoint"] = surrealOptions.Endpoint;
            if (surrealOptions.Username != null)
                overrides["Graphify:SurrealDb:Username"] = surrealOptions.Username;
            if (surrealOptions.Password != null)
                overrides["Graphify:SurrealDb:Password"] = surrealOptions.Password;
            if (surrealOptions.Namespace != null)
                overrides["Graphify:SurrealDb:Namespace"] = surrealOptions.Namespace;
            if (surrealOptions.Database != null)
                overrides["Graphify:SurrealDb:Database"] = surrealOptions.Database;
        }

        if (overrides.Count > 0)
        {
            builder.AddInMemoryCollection(overrides);
        }

        return builder.Build();
    }
}

/// <summary>
/// CLI-provided SurrealDB connection overrides.
/// </summary>
public sealed record CliSurrealOptions
{
    public string? Endpoint { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? Namespace { get; init; }
    public string? Database { get; init; }
}
