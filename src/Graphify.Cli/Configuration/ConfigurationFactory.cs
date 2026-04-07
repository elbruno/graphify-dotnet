using Microsoft.Extensions.Configuration;

namespace Graphify.Cli.Configuration;

/// <summary>
/// Builds a layered IConfiguration:
///   appsettings.json → env vars (GRAPHIFY__*) → user secrets → CLI overrides
/// </summary>
public static class ConfigurationFactory
{
    public static IConfiguration Build(CliProviderOptions? cliOptions = null)
    {
        var builder = new ConfigurationBuilder();

        // Layer 1: appsettings.json defaults
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        builder.AddJsonFile(appSettingsPath, optional: true, reloadOnChange: false);

        // Layer 2: Environment variables (GRAPHIFY__Provider → GRAPHIFY:Provider; case-insensitive section match)
        builder.AddEnvironmentVariables();

        // Layer 3: User secrets
        builder.AddUserSecrets<Program>(optional: true);

        // Layer 4: CLI argument overrides (highest priority)
        if (cliOptions != null)
        {
            var overrides = new Dictionary<string, string?>();

            if (cliOptions.Provider != null)
                overrides["Graphify:Provider"] = cliOptions.Provider;

            if (cliOptions.Endpoint != null)
            {
                if (cliOptions.Provider?.Equals("ollama", StringComparison.OrdinalIgnoreCase) == true)
                    overrides["Graphify:Ollama:Endpoint"] = cliOptions.Endpoint;
                else
                    overrides["Graphify:AzureOpenAI:Endpoint"] = cliOptions.Endpoint;
            }

            if (cliOptions.ApiKey != null)
                overrides["Graphify:AzureOpenAI:ApiKey"] = cliOptions.ApiKey;

            if (cliOptions.Model != null)
            {
                if (cliOptions.Provider?.Equals("ollama", StringComparison.OrdinalIgnoreCase) == true)
                    overrides["Graphify:Ollama:ModelId"] = cliOptions.Model;
                else
                    overrides["Graphify:AzureOpenAI:ModelId"] = cliOptions.Model;
            }

            if (cliOptions.Deployment != null)
                overrides["Graphify:AzureOpenAI:DeploymentName"] = cliOptions.Deployment;

            builder.AddInMemoryCollection(overrides);
        }

        return builder.Build();
    }
}
