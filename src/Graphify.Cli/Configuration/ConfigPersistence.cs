namespace Graphify.Cli.Configuration;

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

/// <summary>
/// Handles saving/loading config to appsettings.local.json in the app directory.
/// API keys are stored separately via dotnet user-secrets for security.
/// </summary>
public static class ConfigPersistence
{
    private const string LocalConfigFileName = "appsettings.local.json";
    private const string UserSecretsId = "graphify-dotnet-3134eb8e-5948-4541-b6e4-ab9f52f3df62";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string GetLocalConfigPath()
    {
        return Path.Combine(AppContext.BaseDirectory, LocalConfigFileName);
    }

    public static void Save(GraphifyConfig config)
    {
        var path = GetLocalConfigPath();

        // Persist API key separately via user-secrets (never write it to JSON)
        if (!string.IsNullOrWhiteSpace(config.AzureOpenAI.ApiKey))
        {
            var stored = StoreApiKeyInUserSecrets(config.AzureOpenAI.ApiKey);
            if (stored)
            {
                AnsiConsole.MarkupLine("[green]🔑 API key stored securely via dotnet user-secrets.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]⚠️  Could not store API key in user-secrets. " +
                    "Run 'dotnet user-secrets set \"Graphify:AzureOpenAI:ApiKey\" \"<your-key>\"' manually.[/]");
            }
        }

        // Build a wrapper object matching the appsettings.json structure (no API key)
        var wrapper = new Dictionary<string, object> { ["Graphify"] = BuildSerializableConfig(config) };

        try
        {
            var json = JsonSerializer.Serialize(wrapper, SerializerOptions);
            File.WriteAllText(path, json);

            AnsiConsole.MarkupLine($"[green]✅ Configuration saved to:[/] [grey]{path}[/]");
            AnsiConsole.MarkupLine("[grey]   (API keys are stored separately in user-secrets, not in this file)[/]");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            AnsiConsole.Write(new Panel(
                    $"[red]Could not write to:[/] {path}\n" +
                    $"[red]Error:[/] {ex.Message}\n\n" +
                    "[yellow]Tip:[/] Use environment variables (GRAPHIFY__Provider, etc.) instead.")
                .Header("[bold red]⚠ Save Failed[/]")
                .BorderColor(Color.Red));
        }
    }

    public static GraphifyConfig? Load()
    {
        var path = GetLocalConfigPath();
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("Graphify", out var section))
                return null;

            return JsonSerializer.Deserialize<GraphifyConfig>(section.GetRawText(), SerializerOptions);
        }
        catch (JsonException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠️  Could not load config: {ex.Message}[/]");
            return null;
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠️  Could not load config: {ex.Message}[/]");
            return null;
        }
    }

    /// <summary>
    /// Stores the API key securely using dotnet user-secrets.
    /// </summary>
    private static bool StoreApiKeyInUserSecrets(string apiKey)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"user-secrets set \"Graphify:AzureOpenAI:ApiKey\" \"{apiKey}\" --id \"{UserSecretsId}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
                return false;

            process.WaitForExit(TimeSpan.FromSeconds(10));
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, object?> BuildSerializableConfig(GraphifyConfig config)
    {
        var result = new Dictionary<string, object?> { ["Provider"] = config.Provider };

        if (config.WorkingFolder != null)
            result["WorkingFolder"] = config.WorkingFolder;
        if (config.OutputFolder != null)
            result["OutputFolder"] = config.OutputFolder;
        if (config.ExportFormats != null)
            result["ExportFormats"] = config.ExportFormats;

        switch (config.Provider?.ToLowerInvariant())
        {
            case "azureopenai":
                // API key is stored in user-secrets, NOT in the JSON file
                result["AzureOpenAI"] = new
                {
                    config.AzureOpenAI.Endpoint,
                    config.AzureOpenAI.DeploymentName,
                    config.AzureOpenAI.ModelId
                };
                break;
            case "ollama":
                result["Ollama"] = new
                {
                    config.Ollama.Endpoint,
                    config.Ollama.ModelId
                };
                break;
            case "copilotsdk":
                result["CopilotSdk"] = new
                {
                    config.CopilotSdk.ModelId
                };
                break;
        }

        return result;
    }
}
