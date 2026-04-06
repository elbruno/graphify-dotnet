using System.CommandLine;
using System.CommandLine.Invocation;
using Graphify.Cli;
using Graphify.Pipeline;

// Root command
var rootCommand = new RootCommand("graphify-dotnet: Transform codebases into knowledge graphs");

// Shared options
var verboseOption = new Option<bool>(
    new[] { "--verbose", "-v" },
    "Enable verbose output");

// Command: run (default)
var runCommand = new Command("run", "Run the full pipeline on a directory or file");

var pathArgument = new Argument<string>(
    "path",
    () => ".",
    "Path to directory or file to analyze");
runCommand.Add(pathArgument);

var outputOption = new Option<string>(
    new[] { "--output", "-o" },
    () => "graphify-out",
    "Output directory for results");
runCommand.Add(outputOption);

var formatOption = new Option<string[]>(
    new[] { "--format", "-f" },
    () => new[] { "json", "html" },
    "Export formats (json, html)");
runCommand.Add(formatOption);

var noCacheOption = new Option<bool>(
    "--no-cache",
    () => false,
    "Disable caching of extraction results");
runCommand.Add(noCacheOption);

runCommand.Add(verboseOption);

runCommand.Handler = CommandHandler.Create(async (string path, string output, string[] format, bool noCache, bool verbose) =>
{
    var runner = new PipelineRunner(Console.Out, verbose);
    var graph = await runner.RunAsync(path, output, format, !noCache, CancellationToken.None);
    Environment.ExitCode = graph != null ? 0 : 1;
});

rootCommand.Add(runCommand);

// Command: benchmark
var benchmarkCommand = new Command("benchmark", "Measure token reduction vs naive full-corpus approach");

var graphPathArgument = new Argument<string>(
    "graph-path",
    () => "graphify-out/graph.json",
    "Path to graph.json file");
benchmarkCommand.Add(graphPathArgument);

var corpusWordsOption = new Option<int?>(
    "--corpus-words",
    "Total word count in corpus (if known)");
benchmarkCommand.Add(corpusWordsOption);

benchmarkCommand.Handler = CommandHandler.Create(async (string graphPath, int? corpusWords) =>
{
    var result = await BenchmarkRunner.RunAsync(graphPath, corpusWords);
    BenchmarkRunner.PrintBenchmark(result, Console.Out);
    Environment.ExitCode = string.IsNullOrEmpty(result.Error) ? 0 : 1;
});

rootCommand.Add(benchmarkCommand);

// Command: query (placeholder for future implementation)
var queryCommand = new Command("query", "Search the knowledge graph for nodes matching a term");
var termArgument = new Argument<string>("term");
queryCommand.Add(termArgument);
queryCommand.Handler = CommandHandler.Create((string term) =>
{
    Console.WriteLine($"Query feature not yet implemented. Search term: {term}");
    Console.WriteLine("This will search the knowledge graph once the query API is complete.");
});
rootCommand.Add(queryCommand);

// Command: explain (placeholder for future implementation)
var explainCommand = new Command("explain", "Explain a node's role and connections");
var nodeIdArgument = new Argument<string>("node-id");
explainCommand.Add(nodeIdArgument);
explainCommand.Handler = CommandHandler.Create((string nodeId) =>
{
    Console.WriteLine($"Explain feature not yet implemented. Node ID: {nodeId}");
    Console.WriteLine("This will explain a node's role and connections once the query API is complete.");
});
rootCommand.Add(explainCommand);

// Command: export (placeholder - already part of run, but can be standalone)
var exportCommand = new Command("export", "Export the graph in a specific format");
var exportFormatArgument = new Argument<string>("format");
exportCommand.Add(exportFormatArgument);
var graphInputOption = new Option<string>(
    "--graph",
    () => "graphify-out/graph.json",
    "Path to existing graph (if not re-running pipeline)");
exportCommand.Add(graphInputOption);
exportCommand.Handler = CommandHandler.Create((string format, string graph) =>
{
    Console.WriteLine($"Export feature not yet implemented as standalone command.");
    Console.WriteLine($"Use 'run --format {format}' to build and export in one step.");
});
rootCommand.Add(exportCommand);

// Command: analyze (placeholder)
var analyzeCommand = new Command("analyze", "Run analysis and print insights");
var analyzeGraphOption = new Option<string>(
    "--graph",
    () => "graphify-out/graph.json",
    "Path to graph.json");
analyzeCommand.Add(analyzeGraphOption);
analyzeCommand.Handler = CommandHandler.Create((string graph) =>
{
    Console.WriteLine($"Analyze feature not yet implemented as standalone command.");
    Console.WriteLine($"Use 'run' to build, analyze, and export in one step.");
});
rootCommand.Add(analyzeCommand);

// Parse and execute
return rootCommand.Invoke(args);
