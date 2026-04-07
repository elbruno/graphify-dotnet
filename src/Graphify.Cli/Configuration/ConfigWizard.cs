namespace Graphify.Cli.Configuration;

using Spectre.Console;

/// <summary>
/// Interactive configuration wizard using Spectre.Console prompts.
/// </summary>
public static class ConfigWizard
{
    public static GraphifyConfig Run(GraphifyConfig? existing = null)
    {
        AnsiConsole.Write(new Rule("[bold blue]🔧 Graphify Configuration[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        var providerChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Select AI provider:[/]")
                .PageSize(5)
                .AddChoices([
                    "Azure OpenAI",
                    "Ollama (local)",
                    "GitHub Copilot SDK",
                    "None (AST-only mode)"
                ]));

        var config = existing ?? new GraphifyConfig();

        switch (providerChoice)
        {
            case "Azure OpenAI":
                config.Provider = "azureopenai";
                PromptAzureOpenAI(config);
                break;
            case "Ollama (local)":
                config.Provider = "ollama";
                PromptOllama(config);
                break;
            case "GitHub Copilot SDK":
                config.Provider = "copilotsdk";
                PromptCopilotSdk(config);
                break;
            case "None (AST-only mode)":
                config.Provider = null;
                break;
        }

        // Export formats
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold cyan]Export Settings[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        var formatPrompt = new MultiSelectionPrompt<string>()
            .Title("[green]Export formats:[/]")
            .PageSize(10)
            .AddChoices(["json", "html", "svg", "neo4j", "obsidian", "wiki", "report"]);
        foreach (var f in ParseSelectedFormats(config.ExportFormats))
            formatPrompt.Select(f);
        var selectedFormats = AnsiConsole.Prompt(formatPrompt);
        config.ExportFormats = string.Join(",", selectedFormats);

        AnsiConsole.WriteLine();
        ShowSummary(config);

        return config;
    }

    private static void PromptAzureOpenAI(GraphifyConfig config)
    {
        AnsiConsole.Write(new Rule("[bold cyan]Azure OpenAI Settings[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        config.AzureOpenAI.Endpoint = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Endpoint URL:[/]")
                .Validate(v => !string.IsNullOrWhiteSpace(v)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Endpoint is required"))
                .DefaultValue(config.AzureOpenAI.Endpoint ?? ""));

        config.AzureOpenAI.ApiKey = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]API Key:[/]")
                .Secret()
                .Validate(v => !string.IsNullOrWhiteSpace(v)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("API key is required")));

        config.AzureOpenAI.DeploymentName = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Deployment name:[/]")
                .Validate(v => !string.IsNullOrWhiteSpace(v)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Deployment name is required"))
                .DefaultValue(config.AzureOpenAI.DeploymentName ?? ""));

        config.AzureOpenAI.ModelId = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Model ID:[/]")
                .DefaultValue(config.AzureOpenAI.ModelId ?? "gpt-4o"));
    }

    private static void PromptOllama(GraphifyConfig config)
    {
        AnsiConsole.Write(new Rule("[bold cyan]Ollama Settings[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        config.Ollama.Endpoint = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Endpoint URL:[/]")
                .DefaultValue(config.Ollama.Endpoint));

        config.Ollama.ModelId = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Model ID:[/]")
                .DefaultValue(config.Ollama.ModelId));
    }

    private static void PromptCopilotSdk(GraphifyConfig config)
    {
        AnsiConsole.Write(new Rule("[bold cyan]GitHub Copilot SDK Settings[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        config.CopilotSdk.ModelId = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Model ID:[/]")
                .DefaultValue(config.CopilotSdk.ModelId));

        AnsiConsole.MarkupLine("[grey]Authentication: GitHub Copilot CLI (login required)[/]");
    }

    private static void ShowSummary(GraphifyConfig config)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold green]✅ Configuration Summary[/]");

        table.AddColumn("[bold]Setting[/]");
        table.AddColumn("[bold]Value[/]");

        var provider = config.Provider ?? "None (AST-only)";
        table.AddRow("Provider", $"[bold]{provider}[/]");

        switch (config.Provider?.ToLowerInvariant())
        {
            case "azureopenai":
                table.AddRow("Endpoint", config.AzureOpenAI.Endpoint ?? "(not set)");
                table.AddRow("API Key", MaskSecret(config.AzureOpenAI.ApiKey));
                table.AddRow("Deployment", config.AzureOpenAI.DeploymentName ?? "(not set)");
                table.AddRow("Model", config.AzureOpenAI.ModelId ?? "(not set)");
                break;
            case "ollama":
                table.AddRow("Endpoint", config.Ollama.Endpoint);
                table.AddRow("Model", config.Ollama.ModelId);
                break;
            case "copilotsdk":
                table.AddRow("Model", config.CopilotSdk.ModelId);
                table.AddRow("Auth", "GitHub Copilot CLI");
                break;
        }

        if (config.ExportFormats != null)
            table.AddRow("Export Formats", config.ExportFormats);

        AnsiConsole.Write(table);
    }

    public static GraphifyConfig RunFolderWizard(GraphifyConfig? existing = null)
    {
        AnsiConsole.Write(new Rule("[bold blue]📂 Project Folder Settings[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        var config = existing ?? new GraphifyConfig();

        var workingFolder = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Project folder to analyze:[/]")
                .DefaultValue(config.WorkingFolder ?? ".")
                .Validate(v =>
                {
                    if (string.IsNullOrWhiteSpace(v))
                        return ValidationResult.Error("Folder path is required");
                    if (!Directory.Exists(v))
                        AnsiConsole.MarkupLine($"[yellow]⚠ Folder '{v}' does not exist yet — will be used when created.[/]");
                    return ValidationResult.Success();
                }));
        config.WorkingFolder = workingFolder;

        var outputFolder = AnsiConsole.Prompt(
            new TextPrompt<string>("[green]Output directory:[/]")
                .DefaultValue(config.OutputFolder ?? "graphify-out"));
        config.OutputFolder = outputFolder;

        var prompt = new MultiSelectionPrompt<string>()
            .Title("[green]Export formats:[/]")
            .PageSize(10)
            .AddChoices(["json", "html", "svg", "neo4j", "obsidian", "wiki", "report"]);
        foreach (var f in ParseSelectedFormats(config.ExportFormats))
            prompt.Select(f);
        var formatChoices = AnsiConsole.Prompt(prompt);
        config.ExportFormats = string.Join(",", formatChoices);

        AnsiConsole.WriteLine();
        ShowFolderSummary(config);

        return config;
    }

    private static string[] ParseSelectedFormats(string? formats)
    {
        if (string.IsNullOrWhiteSpace(formats))
            return ["json", "html", "report"];
        return formats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void ShowFolderSummary(GraphifyConfig config)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold green]✅ Folder Settings Summary[/]");

        table.AddColumn("[bold]Setting[/]");
        table.AddColumn("[bold]Value[/]");

        table.AddRow("Working Folder", config.WorkingFolder ?? "(not set)");
        table.AddRow("Output Folder", config.OutputFolder ?? "(not set)");
        table.AddRow("Export Formats", config.ExportFormats ?? "(not set)");

        AnsiConsole.Write(table);
    }

    private static string MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "(not set)";
        if (value.Length <= 4) return "****";
        return $"****{value[^4..]}";
    }
}
