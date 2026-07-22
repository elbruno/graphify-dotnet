using System.CommandLine;
using System.Text.Json;
using DotNetEnv;
using Graphify.Cli.Configuration;
using Graphify.Cli.Init;
using Graphify.Cli.Mcp;
using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;
using Graphify.Pipeline;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Spectre.Console;
using SurrealDb.Embedded.RocksDb;
using SurrealDb.Net;
using SurrealDb.Net.Models.Auth;

Env.Load();

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
    out Option<string?> modelOpt, out Option<string?> deploymentOpt,
    out Option<string?> surrealEndpointOpt, out Option<string?> surrealUserOpt,
    out Option<string?> surrealPassOpt, out Option<string?> surrealNsOpt,
    out Option<string?> surrealDbOpt)
{
    outputOpt = new Option<string>("--output", "-o")
    {
        Description = "Output directory",
        DefaultValueFactory = _ => "graphify-out"
    };
    formatOpt = new Option<string>("--format", "-f")
    {
        Description = "Export formats (comma-separated): json, html, svg, neo4j, ladybug, obsidian, wiki, surrealdb, report",
        DefaultValueFactory = _ => "json,html,report"
    };
    verboseOpt = new Option<bool>("--verbose", "-v")
    {
        Description = "Enable verbose output"
    };
    providerOpt = new Option<string?>("--provider", "-p")
    {
        Description = "AI provider: azureopenai, ollama, openai, copilotsdk"
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
    surrealEndpointOpt = new Option<string?>("--surreal-endpoint")
    {
        Description = "SurrealDB remote endpoint (e.g., http://localhost:8000). Sets remote mode."
    };
    surrealUserOpt = new Option<string?>("--surreal-user")
    {
        Description = "SurrealDB username (default: root)"
    };
    surrealPassOpt = new Option<string?>("--surreal-pass")
    {
        Description = "SurrealDB password"
    };
    surrealNsOpt = new Option<string?>("--surreal-ns")
    {
        Description = "SurrealDB namespace (default: graphify)"
    };
    surrealDbOpt = new Option<string?>("--surreal-db")
    {
        Description = "SurrealDB database name (default: codebase)"
    };

    cmd.Options.Add(outputOpt);
    cmd.Options.Add(formatOpt);
    cmd.Options.Add(verboseOpt);
    cmd.Options.Add(providerOpt);
    cmd.Options.Add(endpointOpt);
    cmd.Options.Add(apiKeyOpt);
    cmd.Options.Add(modelOpt);
    cmd.Options.Add(deploymentOpt);
    cmd.Options.Add(surrealEndpointOpt);
    cmd.Options.Add(surrealUserOpt);
    cmd.Options.Add(surrealPassOpt);
    cmd.Options.Add(surrealNsOpt);
    cmd.Options.Add(surrealDbOpt);
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
            if (provider == "azureopenai" || provider == "openai" || provider == "copilotsdk")
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
    out var runModelOpt, out var runDeploymentOpt,
    out var runSurrealEndpointOpt, out var runSurrealUserOpt,
    out var runSurrealPassOpt, out var runSurrealNsOpt,
    out var runSurrealDbOpt);

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
    var useConfigWizard = parseResult.GetValue(runConfigOpt);

    // ── Optionally run config wizard (saves to appsettings.local.json) ─────
    if (useConfigWizard)
    {
        var existingConfig = ConfigPersistence.Load();
        var wizardConfig = ConfigWizard.Run(existingConfig);
        ConfigPersistence.Save(wizardConfig);
        AnsiConsole.WriteLine();
    }

    // ── Build configuration from all sources ─────────────────────────────
    var surrealOptions = new CliSurrealOptions
    {
        Endpoint = parseResult.GetValue(runSurrealEndpointOpt),
        Username = parseResult.GetValue(runSurrealUserOpt),
        Password = parseResult.GetValue(runSurrealPassOpt),
        Namespace = parseResult.GetValue(runSurrealNsOpt),
        Database = parseResult.GetValue(runSurrealDbOpt)
    };

    var configuration = ConfigurationFactory.Build(cliOptions: null, surrealOptions);
    var graphifyConfig = new GraphifyConfig();
    configuration.GetSection("Graphify").Bind(graphifyConfig);

    // ── Resolve export formats with proper priority ────────────────────────
    // CLI --format > config (.env, user-secrets, saved) > built-in default
    if (format == "json,html,report" && graphifyConfig.ExportFormats != null)
    {
        format = graphifyConfig.ExportFormats;
    }
    var formats = format.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Apply config-sourced defaults when CLI args are at their defaults
    if (path == "." && graphifyConfig.WorkingFolder != null)
        path = graphifyConfig.WorkingFolder;
    if (output == "graphify-out" && graphifyConfig.OutputFolder != null)
        output = graphifyConfig.OutputFolder;

    // ── Resolve AI provider ──────────────────────────────────────────────
    var (chatClient, verbose) = await ResolveProviderAsync(parseResult,
        runVerboseOpt, runProviderOpt, runEndpointOpt, runApiKeyOpt, runModelOpt, runDeploymentOpt,
        ignoreProviderOptions: useConfigWizard);

    // ── Run pipeline ──────────────────────────────────────────────────────
    var runner = new Graphify.Cli.PipelineRunner(Console.Out, verbose, chatClient, graphifyConfig.SurrealDb);
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
    out var watchModelOpt, out var watchDeploymentOpt,
    out var watchSurrealEndpointOpt, out var watchSurrealUserOpt,
    out var watchSurrealPassOpt, out var watchSurrealNsOpt,
    out var watchSurrealDbOpt);

watchCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var path = parseResult.GetValue(watchPathArg)!;
    var output = parseResult.GetValue(watchOutputOpt)!;
    var format = parseResult.GetValue(watchFormatOpt)!;

    // ── Build configuration from all sources ─────────────────────────────
    var surrealOptions = new CliSurrealOptions
    {
        Endpoint = parseResult.GetValue(watchSurrealEndpointOpt),
        Username = parseResult.GetValue(watchSurrealUserOpt),
        Password = parseResult.GetValue(watchSurrealPassOpt),
        Namespace = parseResult.GetValue(watchSurrealNsOpt),
        Database = parseResult.GetValue(watchSurrealDbOpt)
    };

    var configuration = ConfigurationFactory.Build(cliOptions: null, surrealOptions);
    var graphifyConfig = new GraphifyConfig();
    configuration.GetSection("Graphify").Bind(graphifyConfig);

    // ── Resolve export formats with proper priority ────────────────────────
    // CLI --format > config (.env, user-secrets, saved) > built-in default
    if (format == "json,html,report" && graphifyConfig.ExportFormats != null)
    {
        format = graphifyConfig.ExportFormats;
    }
    var formats = format.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // ── Resolve AI provider ──────────────────────────────────────────────
    var (chatClient, verbose) = await ResolveProviderAsync(parseResult,
        watchVerboseOpt, watchProviderOpt, watchEndpointOpt, watchApiKeyOpt, watchModelOpt, watchDeploymentOpt);

    // ── Run pipeline ──────────────────────────────────────────────────────
    Console.WriteLine("Running initial pipeline...");
    Console.WriteLine();
    var runner = new Graphify.Cli.PipelineRunner(Console.Out, verbose, chatClient, graphifyConfig.SurrealDb);
    var graph = await runner.RunAsync(path, output, formats, useCache: true, cancellationToken);

    if (graph is null)
    {
        Console.WriteLine("Initial pipeline failed. Aborting watch.");
        return 1;
    }

    Console.WriteLine();
    using var watchMode = new WatchMode(Console.Out, verbose, new SurrealDbExportOptions(
        graphifyConfig.SurrealDb.Endpoint,
        graphifyConfig.SurrealDb.Username,
        graphifyConfig.SurrealDb.Password,
        graphifyConfig.SurrealDb.Namespace,
        graphifyConfig.SurrealDb.Database));
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

// ── init command ─────────────────────────────────────────────────────────
var initInstallOpt = new Option<string?>("--install")
{
    Description = "Target specific agents (comma-separated, e.g. copilot,claude)"
};

var initUninstallOpt = new Option<bool>("--uninstall")
{
    Description = "Remove graphify instructions from all agent files"
};

var initForceOpt = new Option<bool>("--force")
{
    Description = "Regenerate even if already installed"
};

var initScopeOpt = new Option<string>("--scope")
{
    Description = "Scope of installation: project (default) or global",
    DefaultValueFactory = _ => "project"
};

var initCommand = new Command("init", "Install MCP agent instructions for coding agents");
initCommand.Options.Add(initInstallOpt);
initCommand.Options.Add(initUninstallOpt);
initCommand.Options.Add(initForceOpt);
initCommand.Options.Add(initScopeOpt);

initCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var projectDir = Directory.GetCurrentDirectory();
    var install = parseResult.GetValue(initInstallOpt);
    var uninstall = parseResult.GetValue(initUninstallOpt);
    var force = parseResult.GetValue(initForceOpt);
    var scope = parseResult.GetValue(initScopeOpt);

    var initService = new InitService(Console.Out, projectDir);

    if (uninstall)
    {
        return await initService.RunAsync(uninstall: true);
    }

    return await initService.RunAsync(install, force: force);
});

rootCommand.Subcommands.Add(initCommand);

// ── serve command ────────────────────────────────────────────────────────
var servePathArg = new Argument<string>("graph-path")
{
    Description = "Path to the graph JSON file",
    DefaultValueFactory = _ => "graphify-out/graph.json"
};

var serveVerboseOpt = new Option<bool>("--verbose", "-v")
{
    Description = "Enable verbose logging"
};

var serveSurrealPathOpt = new Option<string?>("--surreal-path")
{
    Description = "Path to embedded SurrealDB RocksDB file (e.g., graphify-out/codebase.db)"
};

var serveSurrealEndpointOpt = new Option<string?>("--surreal-endpoint")
{
    Description = "Remote SurrealDB endpoint (e.g., http://localhost:8000)"
};

var serveSurrealUserOpt = new Option<string?>("--surreal-user")
{
    Description = "SurrealDB username (default: root)"
};

var serveSurrealPassOpt = new Option<string?>("--surreal-pass")
{
    Description = "SurrealDB password"
};

var serveSurrealNsOpt = new Option<string?>("--surreal-ns")
{
    Description = "SurrealDB namespace (default: graphify)"
};

var serveSurrealDbOpt = new Option<string?>("--surreal-db")
{
    Description = "SurrealDB database name (default: codebase)"
};

var serveCommand = new Command("serve", "Serve the knowledge graph over MCP (Model Context Protocol)");
serveCommand.Arguments.Add(servePathArg);
serveCommand.Options.Add(serveVerboseOpt);
serveCommand.Options.Add(serveSurrealPathOpt);
serveCommand.Options.Add(serveSurrealEndpointOpt);
serveCommand.Options.Add(serveSurrealUserOpt);
serveCommand.Options.Add(serveSurrealPassOpt);
serveCommand.Options.Add(serveSurrealNsOpt);
serveCommand.Options.Add(serveSurrealDbOpt);

serveCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var graphPath = parseResult.GetValue(servePathArg)!;
    var verbose = parseResult.GetValue(serveVerboseOpt);
    var surrealPath = parseResult.GetValue(serveSurrealPathOpt);
    var surrealEndpoint = parseResult.GetValue(serveSurrealEndpointOpt);
    var surrealUser = parseResult.GetValue(serveSurrealUserOpt);
    var surrealPass = parseResult.GetValue(serveSurrealPassOpt);
    var surrealNs = parseResult.GetValue(serveSurrealNsOpt);
    var surrealDb = parseResult.GetValue(serveSurrealDbOpt);

    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = verbose ? LogLevel.Trace : LogLevel.Warning;
    });

    if (surrealEndpoint is not null || surrealPath is not null)
    {
        // ── SurrealDB mode ────────────────────────────────────────────
        try
        {
            ISurrealDbClient db;

            if (surrealPath is not null)
            {
                db = new SurrealDbRocksDbClient(surrealPath);
            }
            else
            {
                var options = SurrealDbOptions.Create()
                    .WithEndpoint(surrealEndpoint!)
                    .WithNamespace(surrealNs ?? "graphify")
                    .WithDatabase(surrealDb ?? "codebase")
                    .WithUsername(surrealUser)
                    .WithPassword(surrealPass)
                    .Build();

                db = new SurrealDbClient(options);

                if (surrealUser is not null)
                {
                    await db.SignIn(new RootAuth
                    {
                        Username = surrealUser,
                        Password = surrealPass ?? ""
                    });
                }
            }

            builder.Services.AddSingleton<ISurrealDbClient>(db);
            builder.Services.AddSingleton<IGraphBackend, SurrealDbGraphBackend>();

            if (verbose)
            {
                await Console.Error.WriteLineAsync($"SurrealDB backend: {(surrealPath ?? surrealEndpoint)}");
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error connecting to SurrealDB: {ex.Message}");
            return 1;
        }
    }
    else
    {
        // ── JSON file mode (existing behavior) ────────────────────────
        KnowledgeGraph graph;
        try
        {
            if (!File.Exists(graphPath))
            {
                await Console.Error.WriteLineAsync($"Error: Graph file not found at '{graphPath}'");
                return 1;
            }

            var json = await File.ReadAllTextAsync(graphPath, cancellationToken);
            var graphData = JsonSerializer.Deserialize<GraphJsonData>(json);

            if (graphData == null)
            {
                await Console.Error.WriteLineAsync($"Error: Failed to parse graph JSON from '{graphPath}'");
                return 1;
            }

            graph = new KnowledgeGraph();

            foreach (var node in graphData.Nodes)
            {
                graph.AddNode(node);
            }

            foreach (var edge in graphData.Edges)
            {
                graph.AddEdge(edge);
            }

            if (verbose)
            {
                await Console.Error.WriteLineAsync($"Loaded graph: {graph.NodeCount} nodes, {graph.EdgeCount} edges");
            }

            builder.Services.AddSingleton(graph);
            builder.Services.AddSingleton<IGraphBackend>(new MemoryGraphBackend(graph));
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Error loading graph: {ex.Message}");
            return 1;
        }
    }

    builder.Services.AddSingleton<GraphTools>();
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    var app = builder.Build();

    if (verbose)
    {
        await Console.Error.WriteLineAsync("Graphify MCP Server started");
    }

    await app.RunAsync(cancellationToken);
    return 0;
});

rootCommand.Subcommands.Add(serveCommand);

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

    // OpenAI section
    var openAiTable = new Table().Border(TableBorder.Simple).Title("[bold cyan]OpenAI[/]");
    openAiTable.AddColumn("[bold]Setting[/]");
    openAiTable.AddColumn("[bold]Value[/]");
    openAiTable.AddRow("Endpoint", FormatValue(config.OpenAi.Endpoint));
    openAiTable.AddRow("Model", FormatValue(config.OpenAi.ModelId));
    openAiTable.AddRow("API Key", MaskSecret(config.OpenAi.ApiKey));
    AnsiConsole.Write(openAiTable);

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

file record GraphJsonData
{
    public List<GraphNode> Nodes { get; init; } = new();
    public List<GraphEdge> Edges { get; init; } = new();
}
