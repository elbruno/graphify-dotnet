using System.Text.Json;
using Graphify.Graph;

namespace Graphify.Pipeline;

/// <summary>
/// Benchmark runner that measures token reduction achieved by graphify.
/// Compares full corpus size vs. graph-based query size.
/// Ported from Python graphify/benchmark.py.
/// </summary>
public static class BenchmarkRunner
{
    private const int CharsPerToken = 4; // Standard approximation: 1 token ≈ 4 characters

    private static readonly string[] SampleQuestions =
    [
        "how does authentication work",
        "what is the main entry point",
        "how are errors handled",
        "what connects the data layer to the api",
        "what are the core abstractions"
    ];

    /// <summary>
    /// Run benchmark comparing corpus tokens vs. graphify query tokens.
    /// </summary>
    /// <param name="graphPath">Path to the exported graph JSON file.</param>
    /// <param name="corpusWords">Total word count from corpus (if known). If null, estimated from graph.</param>
    /// <param name="questions">Custom questions to benchmark. If null, uses default sample questions.</param>
    /// <returns>Benchmark result with token counts and reduction ratios.</returns>
    public static async Task<BenchmarkResult> RunAsync(
        string graphPath,
        int? corpusWords = null,
        IReadOnlyList<string>? questions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphPath);

        if (!File.Exists(graphPath))
        {
            return new BenchmarkResult
            {
                Error = $"Graph file not found: {graphPath}"
            };
        }

        // Load graph
        KnowledgeGraph graph;
        try
        {
            var jsonText = await File.ReadAllTextAsync(graphPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var graphData = JsonSerializer.Deserialize<GraphJsonDto>(jsonText, options);

            if (graphData == null || graphData.Nodes == null || graphData.Edges == null)
            {
                return new BenchmarkResult
                {
                    Error = "Invalid graph JSON format"
                };
            }

            graph = LoadGraphFromJson(graphData);
        }
        catch (Exception ex)
        {
            return new BenchmarkResult
            {
                Error = $"Failed to load graph: {ex.Message}"
            };
        }

        // Estimate corpus size
        int estimatedCorpusWords = corpusWords ?? EstimateCorpusWords(graph);
        int corpusTokens = WordsToTokens(estimatedCorpusWords);

        // Run queries
        var questionList = questions ?? SampleQuestions;
        var perQuestionResults = new List<QuestionBenchmark>();

        foreach (var question in questionList)
        {
            int queryTokens = EstimateQueryTokens(graph, question);
            if (queryTokens > 0)
            {
                double reduction = (double)corpusTokens / queryTokens;
                perQuestionResults.Add(new QuestionBenchmark
                {
                    Question = question,
                    QueryTokens = queryTokens,
                    Reduction = Math.Round(reduction, 1)
                });
            }
        }

        if (perQuestionResults.Count == 0)
        {
            return new BenchmarkResult
            {
                Error = "No matching nodes found for sample questions. Build the graph first."
            };
        }

        int avgQueryTokens = (int)perQuestionResults.Average(q => q.QueryTokens);
        double reductionRatio = (double)corpusTokens / avgQueryTokens;

        return new BenchmarkResult
        {
            CorpusTokens = corpusTokens,
            CorpusWords = estimatedCorpusWords,
            NodeCount = graph.NodeCount,
            EdgeCount = graph.EdgeCount,
            AvgQueryTokens = avgQueryTokens,
            ReductionRatio = Math.Round(reductionRatio, 1),
            PerQuestion = perQuestionResults
        };
    }

    /// <summary>
    /// Print a human-readable benchmark report to the console.
    /// </summary>
    public static void PrintBenchmark(BenchmarkResult result, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(output);

        if (!string.IsNullOrEmpty(result.Error))
        {
            output.WriteLine($"Benchmark error: {result.Error}");
            return;
        }

        output.WriteLine();
        output.WriteLine("graphify token reduction benchmark");
        output.WriteLine(new string('─', 50));
        output.WriteLine($"  Corpus:          {result.CorpusWords:N0} words → ~{result.CorpusTokens:N0} tokens (naive)");
        output.WriteLine($"  Graph:           {result.NodeCount:N0} nodes, {result.EdgeCount:N0} edges");
        output.WriteLine($"  Avg query cost:  ~{result.AvgQueryTokens:N0} tokens");
        output.WriteLine($"  Reduction:       {result.ReductionRatio}x fewer tokens per query");
        output.WriteLine();
        output.WriteLine("  Per question:");
        foreach (var q in result.PerQuestion)
        {
            var truncated = q.Question.Length > 55 ? q.Question[..52] + "..." : q.Question;
            output.WriteLine($"    [{q.Reduction}x] {truncated}");
        }
        output.WriteLine();
    }

    private static KnowledgeGraph LoadGraphFromJson(GraphJsonDto data)
    {
        var graph = new KnowledgeGraph();

        // Add nodes
        foreach (var nodeDto in data.Nodes)
        {
            var node = new Models.GraphNode
            {
                Id = nodeDto.Id,
                Label = nodeDto.Label,
                Type = nodeDto.Type ?? "Entity",
                FilePath = nodeDto.FilePath,
                Community = nodeDto.Community,
                Metadata = nodeDto.Metadata ?? new Dictionary<string, string>()
            };
            graph.AddNode(node);
        }

        // Add edges
        foreach (var edgeDto in data.Edges)
        {
            var sourceNode = graph.GetNode(edgeDto.Source);
            var targetNode = graph.GetNode(edgeDto.Target);

            if (sourceNode != null && targetNode != null)
            {
                var edge = new Models.GraphEdge
                {
                    Source = sourceNode,
                    Target = targetNode,
                    Relationship = edgeDto.Relationship,
                    Weight = edgeDto.Weight,
                    Metadata = edgeDto.Metadata ?? new Dictionary<string, string>()
                };
                graph.AddEdge(edge);
            }
        }

        return graph;
    }

    private static int EstimateCorpusWords(KnowledgeGraph graph)
    {
        // Rough estimate: each node label is ~3 words, plus source context
        return graph.NodeCount * 50;
    }

    private static int WordsToTokens(int words)
    {
        // Approximate conversion: 100 words ≈ 133 tokens
        return words * 100 / 75;
    }

    private static int CharactersToTokens(string text)
    {
        return Math.Max(1, text.Length / CharsPerToken);
    }

    private static int EstimateQueryTokens(KnowledgeGraph graph, string question, int depth = 3)
    {
        // Parse question terms
        var terms = question.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .Select(t => t.ToLowerInvariant())
            .ToList();

        if (terms.Count == 0)
        {
            return 0;
        }

        // Find best-matching nodes
        var scored = new List<(int Score, string NodeId)>();
        foreach (var node in graph.GetNodes())
        {
            var label = node.Label.ToLowerInvariant();
            int score = terms.Count(t => label.Contains(t));
            if (score > 0)
            {
                scored.Add((score, node.Id));
            }
        }

        if (scored.Count == 0)
        {
            return 0;
        }

        // Take top 3 start nodes
        var startNodes = scored.OrderByDescending(s => s.Score)
            .Take(3)
            .Select(s => s.NodeId)
            .ToList();

        // BFS to depth N
        var visited = new HashSet<string>(startNodes);
        var frontier = new HashSet<string>(startNodes);
        var edgesSeen = new List<Models.GraphEdge>();

        for (int i = 0; i < depth; i++)
        {
            var nextFrontier = new HashSet<string>();
            foreach (var nodeId in frontier)
            {
                foreach (var neighbor in graph.GetNeighbors(nodeId))
                {
                    if (!visited.Contains(neighbor.Id))
                    {
                        nextFrontier.Add(neighbor.Id);
                        // Find the edge
                        var edges = graph.GetEdges(nodeId)
                            .Where(e => e.Target.Id == neighbor.Id || e.Source.Id == neighbor.Id);
                        edgesSeen.AddRange(edges);
                    }
                }
            }
            visited.UnionWith(nextFrontier);
            frontier = nextFrontier;
        }

        // Build subgraph context string
        var lines = new List<string>();
        foreach (var nodeId in visited)
        {
            var node = graph.GetNode(nodeId);
            if (node != null)
            {
                var filePath = node.FilePath ?? "";
                var location = node.Metadata?.GetValueOrDefault("source_location", "") ?? "";
                lines.Add($"NODE {node.Label} src={filePath} loc={location}");
            }
        }

        foreach (var edge in edgesSeen.Distinct())
        {
            if (visited.Contains(edge.Source.Id) && visited.Contains(edge.Target.Id))
            {
                lines.Add($"EDGE {edge.Source.Label} --{edge.Relationship}--> {edge.Target.Label}");
            }
        }

        var contextText = string.Join("\n", lines);
        return CharactersToTokens(contextText);
    }

    // DTOs for JSON deserialization
    private sealed class GraphJsonDto
    {
        public List<NodeDto> Nodes { get; set; } = [];
        public List<EdgeDto> Edges { get; set; } = [];
    }

    private sealed class NodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Type { get; set; }
        public int? Community { get; set; }
        public string? FilePath { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    private sealed class EdgeDto
    {
        public string Source { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string Relationship { get; set; } = string.Empty;
        public double Weight { get; set; } = 1.0;
        public Dictionary<string, string>? Metadata { get; set; }
    }
}

/// <summary>
/// Result of a benchmark run.
/// </summary>
public sealed record BenchmarkResult
{
    public string? Error { get; init; }
    public int CorpusTokens { get; init; }
    public int CorpusWords { get; init; }
    public int NodeCount { get; init; }
    public int EdgeCount { get; init; }
    public int AvgQueryTokens { get; init; }
    public double ReductionRatio { get; init; }
    public IReadOnlyList<QuestionBenchmark> PerQuestion { get; init; } = [];
}

/// <summary>
/// Benchmark result for a single question.
/// </summary>
public sealed record QuestionBenchmark
{
    public required string Question { get; init; }
    public required int QueryTokens { get; init; }
    public required double Reduction { get; init; }
}
