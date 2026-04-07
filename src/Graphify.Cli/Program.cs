using System.CommandLine;
using Graphify.Cli.Configuration;
using Graphify.Pipeline;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;

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
        Description = "Export formats (comma-separated)",
        DefaultValueFactory = _ => "json,html"
    };
    verboseOpt = new Option<bool>("--verbose", "-v")
    {
        Description = "Enable verbose output"
    };
    providerOpt = new Option<string?>("--provider", "-p")
    {
        Description = "AI provider: azureopenai, ollama"
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

static (IChatClient? chatClient, bool verbose) ResolveProvider(
    System.CommandLine.ParseResult parseResult,
    Option<bool> verboseOpt,
    Option<string?> providerOpt,
    Option<string?> endpointOpt,
    Option<string?> apiKeyOpt,
    Option<string?> modelOpt,
    Option<string?> deploymentOpt)
{
    var verbose = parseResult.GetValue(verboseOpt);

    var cliOptions = new CliProviderOptions(
        Provider: parseResult.GetValue(providerOpt),
        Endpoint: parseResult.GetValue(endpointOpt),
        ApiKey: parseResult.GetValue(apiKeyOpt),
        Model: parseResult.GetValue(modelOpt),
        Deployment: parseResult.GetValue(deploymentOpt));

    var configuration = ConfigurationFactory.Build(cliOptions);
    var graphifyConfig = new GraphifyConfig();
    configuration.GetSection("Graphify").Bind(graphifyConfig);

    IChatClient? chatClient = null;
    try
    {
        chatClient = ChatClientResolver.Resolve(graphifyConfig);
        if (chatClient != null)
            Console.WriteLine($"\u2713 AI provider: {graphifyConfig.Provider}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\u26a0 AI provider error: {ex.Message}");
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

runCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var path = parseResult.GetValue(runPathArg)!;
    var output = parseResult.GetValue(runOutputOpt)!;
    var format = parseResult.GetValue(runFormatOpt)!;
    var formats = format.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var (chatClient, verbose) = ResolveProvider(parseResult,
        runVerboseOpt, runProviderOpt, runEndpointOpt, runApiKeyOpt, runModelOpt, runDeploymentOpt);

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

    var (chatClient, verbose) = ResolveProvider(parseResult,
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

// ── config show command ──────────────────────────────────────────────────
var configCommand = new Command("config", "Configuration management");
var configShowCommand = new Command("show", "Display resolved provider settings");

configShowCommand.SetAction(parseResult =>
{
    var configuration = ConfigurationFactory.Build();
    var config = new GraphifyConfig();
    configuration.GetSection("Graphify").Bind(config);

    Console.WriteLine("Graphify Configuration (resolved):");
    Console.WriteLine($"  Provider:     {config.Provider ?? "(not set - AST-only mode)"}");
    Console.WriteLine();

    Console.WriteLine("  Azure OpenAI:");
    Console.WriteLine($"    Endpoint:     {config.AzureOpenAI.Endpoint ?? "(not set)"}");
    Console.WriteLine($"    Deployment:   {config.AzureOpenAI.DeploymentName ?? "(not set)"}");
    Console.WriteLine($"    Model:        {config.AzureOpenAI.ModelId ?? "(not set)"}");
    Console.WriteLine($"    API Key:      {MaskSecret(config.AzureOpenAI.ApiKey)}");
    Console.WriteLine();

    Console.WriteLine("  Ollama:");
    Console.WriteLine($"    Endpoint:     {config.Ollama.Endpoint}");
    Console.WriteLine($"    Model:        {config.Ollama.ModelId}");
    Console.WriteLine();

    Console.WriteLine("Configuration sources (highest priority first):");
    Console.WriteLine("  1. CLI arguments (--provider, --endpoint, etc.)");
    Console.WriteLine("  2. Environment variables (GRAPHIFY__*)");
    Console.WriteLine("  3. User secrets (dotnet user-secrets)");
    Console.WriteLine("  4. appsettings.json");
});

configCommand.Subcommands.Add(configShowCommand);
rootCommand.Subcommands.Add(configCommand);

// ── invoke ───────────────────────────────────────────────────────────────
return await rootCommand.Parse(args).InvokeAsync();

static string MaskSecret(string? value)
{
    if (string.IsNullOrEmpty(value)) return "(not set)";
    if (value.Length <= 4) return "****";
    return $"****{value[^4..]}";
}
