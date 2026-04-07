using System.CommandLine;
using Graphify.Pipeline;

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
    var verbose = parseResult.GetValue(runVerboseOpt);
    var provider = parseResult.GetValue(runProviderOpt);

    var formats = format.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (!string.IsNullOrEmpty(provider))
    {
        Console.WriteLine($"ℹ AI provider '{provider}' configured. Semantic extraction will be available after configuration setup.");
    }

    var runner = new Graphify.Cli.PipelineRunner(Console.Out, verbose);
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
    var verbose = parseResult.GetValue(watchVerboseOpt);
    var provider = parseResult.GetValue(watchProviderOpt);

    var formats = format.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    if (!string.IsNullOrEmpty(provider))
    {
        Console.WriteLine($"ℹ AI provider '{provider}' configured. Semantic extraction will be available after configuration setup.");
    }

    Console.WriteLine("Running initial pipeline...");
    Console.WriteLine();
    var runner = new Graphify.Cli.PipelineRunner(Console.Out, verbose);
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

// ── config show command (stub) ───────────────────────────────────────────
var configCommand = new Command("config", "Configuration management");
var configShowCommand = new Command("show", "Display resolved provider settings");

configShowCommand.SetAction(parseResult =>
{
    Console.WriteLine("Configuration display coming soon.");
});

configCommand.Subcommands.Add(configShowCommand);
rootCommand.Subcommands.Add(configCommand);

// ── invoke ───────────────────────────────────────────────────────────────
return await rootCommand.Parse(args).InvokeAsync();
