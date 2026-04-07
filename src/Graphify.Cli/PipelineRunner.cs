using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;
using Graphify.Pipeline;
using Microsoft.Extensions.AI;

namespace Graphify.Cli;

/// <summary>
/// Orchestrates the full graphify pipeline: detect → extract → build → cluster → analyze → export.
/// </summary>
public sealed class PipelineRunner
{
    private readonly TextWriter _output;
    private readonly bool _verbose;
    private readonly IChatClient? _chatClient;

    public PipelineRunner(TextWriter output, bool verbose = false, IChatClient? chatClient = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _verbose = verbose;
        _chatClient = chatClient;
    }

    public async Task<KnowledgeGraph?> RunAsync(
        string inputPath,
        string outputDir,
        string[] formats,
        bool useCache,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await WriteLineAsync("graphify-dotnet: Transform codebases into knowledge graphs");
            await WriteLineAsync(new string('─', 60));
            await WriteLineAsync();

            // Stage 1: Detect files
            await WriteLineAsync("[1/6] Detecting files...");
            var fileDetector = new FileDetector();
            var detectorOptions = new FileDetectorOptions(
                RootPath: inputPath,
                MaxFileSizeBytes: 1024 * 1024, // 1MB
                RespectGitIgnore: true
            );

            var detectedFiles = await fileDetector.ExecuteAsync(detectorOptions, cancellationToken);
            await WriteLineAsync($"      Found {detectedFiles.Count} files to process");
            if (_verbose)
            {
                foreach (var file in detectedFiles.Take(5))
                {
                    await WriteLineAsync($"        - {file.RelativePath} ({file.Language})");
                }
                if (detectedFiles.Count > 5)
                {
                    await WriteLineAsync($"        ... and {detectedFiles.Count - 5} more");
                }
            }
            await WriteLineAsync();

            // Stage 2: Extract nodes and edges
            await WriteLineAsync("[2/6] Extracting code structure...");
            var extractor = new Extractor();
            var extractionResults = new List<ExtractionResult>();
            int processed = 0;
            int skipped = 0;

            foreach (var file in detectedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var result = await extractor.ExecuteAsync(file, cancellationToken);
                    if (result.Nodes.Count > 0 || result.Edges.Count > 0)
                    {
                        extractionResults.Add(result);
                        processed++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (Exception ex)
                {
                    if (_verbose)
                    {
                        await WriteLineAsync($"      Warning: Failed to extract {file.RelativePath}: {ex.Message}");
                    }
                    skipped++;
                }
            }

            await WriteLineAsync($"      Processed {processed} files, skipped {skipped}");
            var totalNodes = extractionResults.Sum(r => r.Nodes.Count);
            var totalEdges = extractionResults.Sum(r => r.Edges.Count);
            await WriteLineAsync($"      Extracted {totalNodes} nodes, {totalEdges} edges");
            await WriteLineAsync();

            // Stage 2b: AI-enhanced semantic extraction (if provider configured)
            if (_chatClient != null)
            {
                await WriteLineAsync("[2b/6] Running AI-enhanced semantic extraction...");
                var semanticExtractor = new SemanticExtractor(_chatClient);
                int semanticProcessed = 0;

                foreach (var file in detectedFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var result = await semanticExtractor.ExecuteAsync(file, cancellationToken);
                        if (result.Nodes.Count > 0 || result.Edges.Count > 0)
                        {
                            extractionResults.Add(result);
                            semanticProcessed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_verbose)
                            await WriteLineAsync($"      Warning: Semantic extraction failed for {file.RelativePath}: {ex.Message}");
                    }
                }

                await WriteLineAsync($"      AI extracted from {semanticProcessed} files");
                totalNodes = extractionResults.Sum(r => r.Nodes.Count);
                totalEdges = extractionResults.Sum(r => r.Edges.Count);
                await WriteLineAsync($"      Total: {totalNodes} nodes, {totalEdges} edges (AST + AI)");
                await WriteLineAsync();
            }
            else
            {
                await WriteLineAsync("      \u2139 No AI provider configured. Using AST-only extraction.");
                await WriteLineAsync("        Use --provider to enable AI-enhanced semantic extraction.");
                await WriteLineAsync();
            }

            // Stage 3: Build graph
            await WriteLineAsync("[3/6] Building knowledge graph...");
            var graphBuilder = new GraphBuilder(new GraphBuilderOptions
            {
                CreateFileNodes = true,
                MinEdgeWeight = 0.1,
                MergeStrategy = MergeStrategy.MostRecent
            });
            var graph = await graphBuilder.ExecuteAsync(extractionResults, cancellationToken);
            await WriteLineAsync($"      Graph: {graph.NodeCount} nodes, {graph.EdgeCount} edges");
            await WriteLineAsync();

            // Stage 4: Detect communities (clustering)
            await WriteLineAsync("[4/6] Detecting communities...");
            var clusterEngine = new ClusterEngine(new ClusterOptions
            {
                MaxIterations = 100,
                Resolution = 1.0,
                MinSplitSize = 5,
                MaxCommunityFraction = 0.2
            });
            graph = await clusterEngine.ExecuteAsync(graph, cancellationToken);
            var communityCount = graph.GetNodes()
                .Where(n => n.Community.HasValue)
                .Select(n => n.Community!.Value)
                .Distinct()
                .Count();
            await WriteLineAsync($"      Found {communityCount} communities");
            await WriteLineAsync();

            // Stage 5: Analyze graph
            await WriteLineAsync("[5/6] Analyzing graph structure...");
            var analyzer = new Analyzer(new AnalyzerOptions
            {
                TopGodNodesCount = 10,
                TopSurprisingConnections = 5,
                MaxSuggestedQuestions = 10
            });
            var analysis = await analyzer.ExecuteAsync(graph, cancellationToken);
            await WriteLineAsync($"      God nodes: {analysis.GodNodes.Count}");
            await WriteLineAsync($"      Surprising connections: {analysis.SurprisingConnections.Count}");
            await WriteLineAsync($"      Suggested questions: {analysis.SuggestedQuestions.Count}");
            await WriteLineAsync();

            // Stage 6: Export
            await WriteLineAsync("[6/6] Exporting results...");
            Directory.CreateDirectory(outputDir);

            foreach (var format in formats)
            {
                try
                {
                    var outputPath = Path.Combine(outputDir, $"graph.{format}");

                    switch (format.ToLowerInvariant())
                    {
                        case "json":
                            var jsonExporter = new JsonExporter();
                            await jsonExporter.ExportAsync(graph, outputPath, cancellationToken);
                            await WriteLineAsync($"      Exported JSON: {outputPath}");
                            break;

                        case "html":
                            var htmlExporter = new HtmlExporter();
                            await htmlExporter.ExportAsync(graph, outputPath, cancellationToken: cancellationToken);
                            await WriteLineAsync($"      Exported HTML: {outputPath}");
                            break;

                        default:
                            await WriteLineAsync($"      Warning: Unknown format '{format}' - skipped");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    await WriteLineAsync($"      Error exporting {format}: {ex.Message}");
                }
            }

            await WriteLineAsync();
            await WriteLineAsync("✓ Pipeline completed successfully");
            await WriteLineAsync();

            // Print summary
            await WriteLineAsync("Summary:");
            await WriteLineAsync($"  Nodes:         {analysis.Statistics.NodeCount}");
            await WriteLineAsync($"  Edges:         {analysis.Statistics.EdgeCount}");
            await WriteLineAsync($"  Communities:   {analysis.Statistics.CommunityCount}");
            await WriteLineAsync($"  Avg Degree:    {analysis.Statistics.AverageDegree:F2}");
            await WriteLineAsync($"  Isolated:      {analysis.Statistics.IsolatedNodeCount}");

            if (analysis.GodNodes.Count > 0)
            {
                await WriteLineAsync();
                await WriteLineAsync("Top God Nodes:");
                foreach (var godNode in analysis.GodNodes.Take(5))
                {
                    await WriteLineAsync($"  [{godNode.EdgeCount,3}] {godNode.Label}");
                }
            }

            return graph;
        }
        catch (OperationCanceledException)
        {
            await WriteLineAsync();
            await WriteLineAsync("Pipeline cancelled by user");
            return null;
        }
        catch (Exception ex)
        {
            await WriteLineAsync();
            await WriteLineAsync($"Error: {ex.Message}");
            if (_verbose)
            {
                await WriteLineAsync(ex.StackTrace ?? string.Empty);
            }
            return null;
        }
    }

    private async Task WriteLineAsync(string message = "")
    {
        await _output.WriteLineAsync(message);
    }
}
