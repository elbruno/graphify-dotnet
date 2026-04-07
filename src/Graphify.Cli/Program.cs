using Graphify.Pipeline;

if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
{
    Console.WriteLine("graphify-dotnet: Transform codebases into knowledge graphs");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  graphify run [path] [options]       Run the full pipeline");
    Console.WriteLine("  graphify watch [path] [options]     Watch for changes and re-process");
    Console.WriteLine("  graphify benchmark [graph.json]     Measure token reduction");
    Console.WriteLine();
    Console.WriteLine("Run / Watch Options:");
    Console.WriteLine("  --output, -o <path>     Output directory (default: graphify-out)");
    Console.WriteLine("  --format, -f <formats>  Export formats: json,html (default: json,html)");
    Console.WriteLine("  --verbose, -v           Enable verbose output");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  graphify run .                          # Analyze current directory");
    Console.WriteLine("  graphify run ./src --output ./docs     # Custom output");
    Console.WriteLine("  graphify watch .                        # Watch and re-process on changes");
    Console.WriteLine("  graphify benchmark graphify-out/graph.json");
    Console.WriteLine();
    return 0;
}

var command = args[0].ToLowerInvariant();

(string path, string output, string[] formats, bool verbose) ParseRunOptions()
{
    var p = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : ".";
    var o = "graphify-out";
    var f = new[] { "json", "html" };
    var v = args.Contains("--verbose") || args.Contains("-v");

    for (int i = 1; i < args.Length; i++)
    {
        if (args[i] is "--output" or "-o")
        {
            if (i + 1 < args.Length) o = args[++i];
        }
        else if (args[i] is "--format" or "-f")
        {
            if (i + 1 < args.Length) f = args[++i].Split(',');
        }
    }
    return (p, o, f, v);
}

if (command == "run")
{
    var (path, output, formats, verbose) = ParseRunOptions();
    var runner = new Graphify.Cli.PipelineRunner(Console.Out, verbose);
    var graph = await runner.RunAsync(path, output, formats, useCache: true, CancellationToken.None);
    return graph != null ? 0 : 1;
}
else if (command == "watch")
{
    var (path, output, formats, verbose) = ParseRunOptions();

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    // Run initial pipeline
    Console.WriteLine("Running initial pipeline...");
    Console.WriteLine();
    var runner = new Graphify.Cli.PipelineRunner(Console.Out, verbose);
    var graph = await runner.RunAsync(path, output, formats, useCache: true, cts.Token);

    if (graph is null)
    {
        Console.WriteLine("Initial pipeline failed. Aborting watch.");
        return 1;
    }

    Console.WriteLine();
    using var watchMode = new WatchMode(Console.Out, verbose);
    watchMode.SetInitialGraph(graph);
    await watchMode.WatchAsync(path, output, formats, cts.Token);
    return 0;
}
else if (command == "benchmark")
{
    var graphPath = args.Length > 1 ? args[1] : "graphify-out/graph.json";
    var result = await Graphify.Pipeline.BenchmarkRunner.RunAsync(graphPath, corpusWords: null);
    Graphify.Pipeline.BenchmarkRunner.PrintBenchmark(result, Console.Out);
    return string.IsNullOrEmpty(result.Error) ? 0 : 1;
}
else
{
    Console.WriteLine($"Unknown command: {command}");
    Console.WriteLine("Run 'graphify --help' for usage.");
    return 1;
}
