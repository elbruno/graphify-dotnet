using System.CommandLine;
using Graphify.Cli.Configuration;
using Graphify.Pipeline;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

var rootCommand = new RootCommand("graphify-dotnet: AI-powered knowledge graph builder for codebases");

// ── Shared option/argument factory helpers ───────────────────────────────
static Argument<string> PathArg(string description)
{
    return new Argument<string>("path")
    {
        Description = description,
        DefaultValueFactory = _ => "."
    };
}

static void AddPipelineOptions(Command cmd,
    out Option<string> outputOpt, out Option<string> formatOpt,
    out Option<bool> verboseOpt, out Option<string?> providerOpt,
    out Option<string?> endpointOpt, out Option<string?> apiKeyOpt,
    out Option<string?> modelOpt, out Option<string?> deploymentOpt)
{
    outputOpt = new Option<string>("--output", "-o")
    {
        Description = "Output directory",
        DefaultValueFactory = _ => "graphify-out"
    };
    formatOpt = new Option<string>("--format", "-f")
    {
        Description = "Export formats (comma-separated): json, html, svg, neo4j, ladybug, obsidian, wiki, report",
        DefaultValueFactory = _ => "json,html,report"
    };
    verboseOpt = new Option<bool>("--verbose", "-v")
    {
        Description = "Enable verbose output"
    };
    providerOpt = new Option<string?>("--provider", "-p")
    {
        Description = "AI provider: azureopenai, ollama, copilotsdk"
    };
    endpointOpt = new Option<string?>("--endpoint")
    {
        Description = "AI service endpoint URL"
    };
    apiKeyOpt = new Option<string?>("--api-key")
    {
        Description = "API key for the AI provider"
    };
    modelOpt = new Option<string?>("--model")
    {
        Description = "Model ID (e.g., gpt-4o, llama3.2)"
    };
    deploymentOpt = new Option<string?>("--deployment")
    {
        Description = "Azure OpenAI deployment name"
    };

    cmd.Options.Add(outputOpt);
    cmd.Options.Add(formatOpt);
    cmd.Options.Add(verboseOpt);
    cmd.Options.Add(providerOpt);
    cmd.Options.Add(endpointOpt);
    cmd.Options.Add(apiKeyOpt);
    cmd.Options.Add(modelOpt);
    cmd.Options.Add(deploymentOpt);
}

static async Task<(IChatClient? chatClient, bool verbose)> ResolveProviderAsync(
    System.CommandLine.ParseResult parseResult,
    Option<bool> verboseOpt,
    Option<string?> providerOpt,
    Option<string?> endpointOpt,
    Option<string?> apiKeyOpt,
    Option<string?> modelOpt,
    Option<string?> deploymentOpt,
    bool ignoreProviderOptions = false)
{
    var verbose = parseResult.GetValue(verboseOpt);

    CliProviderOptions? cliOptions = null;
    if (!ignoreProviderOptions)
    {
        cliOptions = new CliProviderOptions(
            Provider: parseResult.GetValue(providerOpt),
            Endpoint: parseResult.GetValue(endpointOpt),
            ApiKey: parseResult.GetValue(apiKeyOpt),
            Model: parseResult.GetValue(modelOpt),
            Deployment: parseResult.GetValue(deploymentOpt));
    }

    var configuration = ConfigurationFactory.Build(cliOptions);
    var graphifyConfig = new GraphifyConfig();
    configuration.GetSection("Graphify").Bind(graphifyConfig);

    IChatClient? chatClient = null;
    try
    {
        chatClient = await ChatClientResolver.ResolveAsync(graphifyConfig);
        if (chatClient != null)
        {
            Console.WriteLine($"\u2713 AI provider: {graphifyConfig.Provider}");

            // Data privacy warning for cloud AI providers
            var provider = graphifyConfig.Provider?.ToLowerInvariant();
            if (provider == "azureopenai" || provider == "copilotsdk")
            {
                Console.WriteLine($"\u26a0\ufe0f  Note: Source code contents will be sent to {graphifyConfig.Provider} for semantic analysis. Use --provider ast for local-only analysis.");
            }
        }
    }
    catch (Exception ex)
    {
        if (verbose)
            Console.WriteLine($"\u26a0 AI provider error: {ex.Message}");
        else
            Console.WriteLine("\u26a0 AI provider initialization failed. Use --verbose for details.");
        Console.WriteLine("  Continuing with AST-only extraction.");
    }

    return (chatClient, verbose);
}

// ── run command ──────────────────────────────────────────────────────────
var runPathArg = PathArg("Path to the project to analyze");

var runCommand = new Command("run", "Run the full extraction and graph-building pipeline");
runCommand.Arguments.Add(runPathArg);
AddPipelineOptions(runCommand,
    out var runOutputOpt, out var runFormatOpt, out var runVerboseOpt,
    out var runProviderOpt, out var runEndpointOpt, out var runApiKeyOpt,
    out var runModelOpt, out var runDeploymentOpt);

var runConfigOpt = new Option<bool>("--config", "-c")
{
    Description = "Launch interactive configuration wizard before running"
};
runCommand.Options.Add(runConfigOpt);

runCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var path = parseResult.GetValue(runPathArg)!;
    var output = parseResult.GetValue(runOutputOpt)!;
    var format = parseResult.GetValue(runFormatOpt)!;
    var formats = format.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var useConfigWizard = parseResult.GetValue(runConfigOpt);

    // Apply saved config defaults when CLI arguments are at their default values
    var savedConfig = ConfigPersistence.Load();
    if (savedConfig != null)
    {
        if (path == "." && savedConfig.WorkingFolder != null)
            path = savedConfig.WorkingFolder;
        if (output == "graphify-out" && savedConfig.OutputFolder != null)
            output = savedConfig.OutputFolder;
        if (format == "json,html,report" && savedConfig.ExportFormats != null)
        {
            format = savedConfig.ExportFormats;
            formats = format.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    if (useConfigWizard)
    {
        var existingConfig = ConfigPersistence.Load();
        var wizardConfig = ConfigWizard.Run(existingConfig);
        ConfigPersistence.Save(wizardConfig);
        AnsiConsole.WriteLine();
    }

    var (chatClient, verbose) = await ResolveProviderAsync(parseResult,
        runVerboseOpt, runProviderOpt, runEndpointOpt, runApiKeyOpt, runModelOpt, runDeploymentOpt,
        ignoreProviderOptions: useConfigWizard);

    var runner = new Graphify.Cli.PipelineRunner(Console.Out, verbose, chatClient);
    var graph = await runner.RunAsync(path, output, formats, useCache: true, cancellationToken);
    return graph != null ? 0 : 1;
});

rootCommand.Subcommands.Add(runCommand);

// ── watch command ────────────────────────────────────────────────────────
var watchPathArg = PathArg("Path to the project to watch");

var watchCommand = new Command("watch", "Watch for changes and re-process");
watchCommand.Arguments.Add(watchPathArg);
AddPipelineOptions(watchCommand,
    out var watchOutputOpt, out var watchFormatOpt, out var watchVerboseOpt,
    out var watchProviderOpt, out var watchEndpointOpt, out var watchApiKeyOpt,
    out var watchModelOpt, out var watchDeploymentOpt);

watchCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var path = parseResult.GetValue(watchPathArg)!;
    var output = parseResult.GetValue(watchOutputOpt)!;
    var format = parseResult.GetValue(watchFormatOpt)!;
    var formats = format.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var (chatClient, verbose) = await ResolveProviderAsync(parseResult,
        watchVerboseOpt, watchProviderOpt, watchEndpointOpt, watchApiKeyOpt, watchModelOpt, watchDeploymentOpt);

    Console.WriteLine("Running initial pipeline...");
    Console.WriteLine();
    var runner = new Graphify.Cli.PipelineRunner(Console.Out, verbose, chatClient);
    var graph = await runner.RunAsync(path, output, formats, useCache: true, cancellationToken);

    if (graph is null)
    {
        Console.WriteLine("Initial pipeline failed. Aborting watch.");
        return 1;
    }

    Console.WriteLine();
    using var watchMode = new WatchMode(Console.Out, verbose);
    watchMode.SetInitialGraph(graph);
    await watchMode.WatchAsync(path, output, formats, cancellationToken);
    return 0;
});

rootCommand.Subcommands.Add(watchCommand);

// ── benchmark command ────────────────────────────────────────────────────
var benchmarkPathArg = new Argument<string>("graph-path")
{
    Description = "Path to the graph JSON file",
    DefaultValueFactory = _ => "graphify-out/graph.json"
};

var benchmarkCommand = new Command("benchmark", "Measure token reduction");
benchmarkCommand.Arguments.Add(benchmarkPathArg);

benchmarkCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var graphPath = parseResult.GetValue(benchmarkPathArg)!;
    var result = await BenchmarkRunner.RunAsync(graphPath, corpusWords: null);
    BenchmarkRunner.PrintBenchmark(result, Console.Out);
    return string.IsNullOrEmpty(result.Error) ? 0 : 1;
});

rootCommand.Subcommands.Add(benchmarkCommand);

// ── config command ───────────────────────────────────────────────────
var configCommand = new Command("config", "Configuration management");

// config show subcommand — styled with Spectre.Console
var configShowCommand = new Command("show", "Display resolved provider settings");

configShowCommand.SetAction(parseResult =>
{
    ShowStyledConfig();
});

// config set subcommand — launches interactive wizard
var configSetCommand = new Command("set", "Set up AI provider interactively");

configSetCommand.SetAction(parseResult =>
{
    var existingConfig = ConfigPersistence.Load();
    var wizardConfig = ConfigWizard.Run(existingConfig);
    ConfigPersistence.Save(wizardConfig);
});

// config (no subcommand) — interactive menu
configCommand.SetAction(parseResult =>
{
    var action = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[bold]What would you like to do?[/]")
            .AddChoices([
                "📋 View current configuration",
                "🔧 Set up AI provider",
                "📂 Set folder to analyze"
            ]));

    if (action.StartsWith("📋"))
    {
        ShowStyledConfig();
    }
    else if (action.StartsWith("📂"))
    {
        var existingConfig = ConfigPersistence.Load();
        var wizardConfig = ConfigWizard.RunFolderWizard(existingConfig);
        ConfigPersistence.Save(wizardConfig);
    }
    else
    {
        var existingConfig = ConfigPersistence.Load();
        var wizardConfig = ConfigWizard.Run(existingConfig);
        ConfigPersistence.Save(wizardConfig);
    }
});

configCommand.Subcommands.Add(configShowCommand);
configCommand.Subcommands.Add(configSetCommand);

// config folder subcommand — launches folder wizard
var configFolderCommand = new Command("folder", "Set the default project folder to analyze");

configFolderCommand.SetAction(parseResult =>
{
    var existingConfig = ConfigPersistence.Load();
    var wizardConfig = ConfigWizard.RunFolderWizard(existingConfig);
    ConfigPersistence.Save(wizardConfig);
});

configCommand.Subcommands.Add(configFolderCommand);
rootCommand.Subcommands.Add(configCommand);

// ── invoke ───────────────────────────────────────────────────────────────
return await rootCommand.Parse(args).InvokeAsync();

static string MaskSecret(string? value)
{
    if (string.IsNullOrEmpty(value)) return "[grey](not set)[/]";
    if (value.Length <= 4) return "[yellow]****[/]";
    return $"[yellow]****{value[^4..]}[/]";
}

static void ShowStyledConfig()
{
    var configuration = ConfigurationFactory.Build();
    var config = new GraphifyConfig();
    configuration.GetSection("Graphify").Bind(config);

    AnsiConsole.Write(new Rule("[bold blue]Graphify Configuration (resolved)[/]").RuleStyle("blue"));
    AnsiConsole.WriteLine();

    var providerText = config.Provider != null
        ? $"[green]{config.Provider}[/]"
        : "[grey](not set — AST-only mode)[/]";
    AnsiConsole.MarkupLine($"  [bold]Provider:[/]  {providerText}");
    AnsiConsole.WriteLine();

    // Project settings section
    var savedConfig = ConfigPersistence.Load();
    var folderTable = new Table().Border(TableBorder.Simple).Title("[bold cyan]Project Settings[/]");
    folderTable.AddColumn("[bold]Setting[/]");
    folderTable.AddColumn("[bold]Value[/]");
    folderTable.AddRow("Working Folder", FormatValue(savedConfig?.WorkingFolder));
    folderTable.AddRow("Output Folder", FormatValue(savedConfig?.OutputFolder));
    folderTable.AddRow("Export Formats", FormatValue(savedConfig?.ExportFormats));
    AnsiConsole.Write(folderTable);

    // Azure OpenAI section
    var azureTable = new Table().Border(TableBorder.Simple).Title("[bold cyan]Azure OpenAI[/]");
    azureTable.AddColumn("[bold]Setting[/]");
    azureTable.AddColumn("[bold]Value[/]");
    azureTable.AddRow("Endpoint", FormatValue(config.AzureOpenAI.Endpoint));
    azureTable.AddRow("Deployment", FormatValue(config.AzureOpenAI.DeploymentName));
    azureTable.AddRow("Model", FormatValue(config.AzureOpenAI.ModelId));
    azureTable.AddRow("API Key", MaskSecret(config.AzureOpenAI.ApiKey));
    AnsiConsole.Write(azureTable);

    // Ollama section
    var ollamaTable = new Table().Border(TableBorder.Simple).Title("[bold cyan]Ollama[/]");
    ollamaTable.AddColumn("[bold]Setting[/]");
    ollamaTable.AddColumn("[bold]Value[/]");
    ollamaTable.AddRow("Endpoint", $"[green]{config.Ollama.Endpoint}[/]");
    ollamaTable.AddRow("Model", $"[green]{config.Ollama.ModelId}[/]");
    AnsiConsole.Write(ollamaTable);

    // Copilot SDK section
    var copilotTable = new Table().Border(TableBorder.Simple).Title("[bold cyan]Copilot SDK[/]");
    copilotTable.AddColumn("[bold]Setting[/]");
    copilotTable.AddColumn("[bold]Value[/]");
    copilotTable.AddRow("Model", $"[green]{config.CopilotSdk.ModelId}[/]");
    copilotTable.AddRow("Auth", "GitHub Copilot CLI (login required)");
    AnsiConsole.Write(copilotTable);

    // Config sources
    AnsiConsole.WriteLine();
    AnsiConsole.Write(new Panel(
            "[dim]1.[/] CLI arguments (--provider, --endpoint, etc.)\n" +
            "[dim]2.[/] Environment variables (GRAPHIFY__*)\n" +
            "[dim]3.[/] User secrets (dotnet user-secrets)\n" +
            "[dim]4.[/] appsettings.local.json (wizard-saved)\n" +
            "[dim]5.[/] appsettings.json (defaults)")
        .Header("[bold]Configuration sources (highest priority first)[/]")
        .BorderColor(Color.Grey));
}

static string FormatValue(string? value)
{
    return value != null ? $"[green]{value}[/]" : "[grey](not set)[/]";
}
