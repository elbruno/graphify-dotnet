using System.Collections.Concurrent;
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
            var extractionBag = new ConcurrentBag<ExtractionResult>();
            int processed = 0;
            int skipped = 0;
            var verboseWarnings = new ConcurrentQueue<string>();

            await Parallel.ForEachAsync(
                detectedFiles,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = cancellationToken
                },
                async (file, ct) =>
                {
                    try
                    {
                        var result = await extractor.ExecuteAsync(file, ct);
                        if (result.Nodes.Count > 0 || result.Edges.Count > 0)
                        {
                            extractionBag.Add(result);
                            Interlocked.Increment(ref processed);
                        }
                        else
                        {
                            Interlocked.Increment(ref skipped);
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref skipped);
                        if (_verbose)
                        {
                            verboseWarnings.Enqueue($"      Warning: Failed to extract {file.RelativePath}: {ex.Message}");
                        }
                    }
                });

            var extractionResults = new List<ExtractionResult>(extractionBag);
            foreach (var warning in verboseWarnings)
            {
                await WriteLineAsync(warning);
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

            // Prepare community labels and cohesion scores for report and exports
            var communityLabels = BuildCommunityLabels(graph);
            var cohesionScores = CalculateCohesionScores(graph);

            // Stage 6: Export
            await WriteLineAsync("[6/6] Exporting results...");

            // Validate output directory to prevent path traversal
            var validator = new Graphify.Security.InputValidator();
            var outputValidation = validator.ValidatePath(outputDir);
            if (!outputValidation.IsValid)
            {
                throw new ArgumentException($"Invalid output directory: {string.Join("; ", outputValidation.Errors)}");
            }

            Directory.CreateDirectory(outputDir);

            foreach (var format in formats)
            {
                try
                {
                    var normalizedFormat = format.ToLowerInvariant();

                    switch (normalizedFormat)
                    {
                        case "json":
                            var jsonExporter = new JsonExporter();
                            var jsonPath = Path.Combine(outputDir, "graph.json");
                            await jsonExporter.ExportAsync(graph, jsonPath, cancellationToken);
                            await WriteLineAsync($"      Exported JSON: {jsonPath}");
                            break;

                        case "html":
                            var htmlExporter = new HtmlExporter();
                            var htmlPath = Path.Combine(outputDir, "graph.html");
                            await htmlExporter.ExportAsync(graph, htmlPath, communityLabels, cancellationToken);
                            await WriteLineAsync($"      Exported HTML: {htmlPath}");
                            break;

                        case "svg":
                            var svgExporter = new SvgExporter();
                            var svgPath = Path.Combine(outputDir, "graph.svg");
                            await svgExporter.ExportAsync(graph, svgPath, cancellationToken);
                            await WriteLineAsync($"      Exported SVG: {svgPath}");
                            break;

                        case "neo4j":
                            var neo4jExporter = new Neo4jExporter();
                            var cypherPath = Path.Combine(outputDir, "graph.cypher");
                            await neo4jExporter.ExportAsync(graph, cypherPath, cancellationToken);
                            await WriteLineAsync($"      Exported Neo4j Cypher: {cypherPath}");
                            break;

                        case "obsidian":
                            var obsidianExporter = new ObsidianExporter();
                            var obsidianPath = Path.Combine(outputDir, "obsidian");
                            await obsidianExporter.ExportAsync(graph, obsidianPath, cancellationToken);
                            await WriteLineAsync($"      Exported Obsidian vault: {obsidianPath}/");
                            break;

                        case "wiki":
                            var wikiExporter = new WikiExporter();
                            var wikiPath = Path.Combine(outputDir, "wiki");
                            await wikiExporter.ExportAsync(graph, wikiPath, cancellationToken);
                            await WriteLineAsync($"      Exported Wiki: {wikiPath}/");
                            break;

                        case "report":
                            var reportGenerator = new ReportGenerator();
                            var projectName = Path.GetFileName(Path.GetFullPath(inputPath));
                            var reportMarkdown = reportGenerator.Generate(graph, analysis, communityLabels, cohesionScores, projectName);
                            var reportPath = Path.Combine(outputDir, "GRAPH_REPORT.md");
                            await File.WriteAllTextAsync(reportPath, reportMarkdown, cancellationToken);
                            await WriteLineAsync($"      Exported Report: {reportPath}");
                            break;

                        default:
                            await WriteLineAsync($"      Warning: Unknown format '{normalizedFormat}' - skipped");
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

    private static Dictionary<int, string> BuildCommunityLabels(KnowledgeGraph graph)
    {
        var result = new Dictionary<int, string>();
        var communities = graph.GetNodes()
            .Where(n => n.Community.HasValue)
            .GroupBy(n => n.Community!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (commId, nodes) in communities)
        {
            // Use the most common node type as label
            var commonType = nodes
                .GroupBy(n => n.Type)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key ?? "Mixed";

            result[commId] = $"{commonType} (Community {commId})";
        }

        return result;
    }

    private static Dictionary<int, double> CalculateCohesionScores(KnowledgeGraph graph)
    {
        var communities = graph.GetNodes()
            .Where(n => n.Community.HasValue)
            .GroupBy(n => n.Community!.Value)
            .ToDictionary(g => g.Key, g => g.Select(n => n.Id).ToList());

        var result = new Dictionary<int, double>();
        foreach (var (commId, nodeIds) in communities)
        {
            result[commId] = CalculateCohesion(graph, nodeIds);
        }

        return result;
    }

    private static double CalculateCohesion(KnowledgeGraph graph, List<string> nodeIds)
    {
        if (nodeIds.Count < 2) return 0.0;

        // Count internal edges (edges within community)
        var internalEdges = 0;
        var nodeSet = nodeIds.ToHashSet();

        foreach (var nodeId in nodeIds)
        {
            var edges = graph.GetEdges(nodeId);
            internalEdges += edges.Count(e =>
                nodeSet.Contains(e.Source.Id) && nodeSet.Contains(e.Target.Id));
        }

        // Cohesion = internal edges / possible edges
        var possibleEdges = nodeIds.Count * (nodeIds.Count - 1);
        return possibleEdges > 0 ? (double)internalEdges / possibleEdges : 0.0;
    }
}
