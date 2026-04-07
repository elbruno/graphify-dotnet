namespace Graphify.Cli.Configuration;

using System.Text.Json;
using System.Text.Json.Serialization;
using Spectre.Console;

/// <summary>
/// Handles saving/loading config to appsettings.local.json in the app directory.
/// </summary>
public static class ConfigPersistence
{
    private const string LocalConfigFileName = "appsettings.local.json";

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

        // Build a wrapper object matching the appsettings.json structure
        var wrapper = new Dictionary<string, object> { ["Graphify"] = BuildSerializableConfig(config) };

        try
        {
            var json = JsonSerializer.Serialize(wrapper, SerializerOptions);
            File.WriteAllText(path, json);

            AnsiConsole.MarkupLine($"[green]✅ Configuration saved to:[/] [grey]{path}[/]");
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
        catch
        {
            return null;
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
                result["AzureOpenAI"] = new
                {
                    config.AzureOpenAI.Endpoint,
                    config.AzureOpenAI.ApiKey,
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
