using Microsoft.Extensions.Configuration;

namespace Graphify.Cli.Configuration;

/// <summary>
/// Builds a layered IConfiguration:
///   appsettings.json → appsettings.local.json → env vars (GRAPHIFY__*) → user secrets → CLI overrides
/// </summary>
public static class ConfigurationFactory
{
    public static IConfiguration Build(CliProviderOptions? cliOptions = null)
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
        if (cliOptions != null)
        {
            var overrides = new Dictionary<string, string?>();

            if (cliOptions.Provider != null)
                overrides["Graphify:Provider"] = cliOptions.Provider;

            if (cliOptions.Endpoint != null)
            {
                if (cliOptions.Provider?.Equals("ollama", StringComparison.OrdinalIgnoreCase) == true)
                    overrides["Graphify:Ollama:Endpoint"] = cliOptions.Endpoint;
                else if (cliOptions.Provider?.Equals("copilotsdk", StringComparison.OrdinalIgnoreCase) != true)
                    overrides["Graphify:AzureOpenAI:Endpoint"] = cliOptions.Endpoint;
                // CopilotSdk does not use endpoints — silently ignore
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

            builder.AddInMemoryCollection(overrides);
        }

        return builder.Build();
    }
}
