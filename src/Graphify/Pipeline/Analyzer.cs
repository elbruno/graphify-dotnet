using Graphify.Graph;
using Graphify.Models;

namespace Graphify.Pipeline;

/// <summary>
/// Analyzes a clustered knowledge graph to surface insights:
/// god nodes, surprising connections, suggested questions, and statistics.
/// Ported from Python graphify/analyze.py.
/// </summary>
public sealed class Analyzer : IPipelineStage<KnowledgeGraph, AnalysisResult>
{
    private readonly AnalyzerOptions _options;
    private static readonly HashSet<string> CodeExtensions = ["cs", "py", "ts", "tsx", "js", "go", "rs", "java", "rb", "cpp", "c", "h", "kt", "scala", "php"];
    private static readonly HashSet<string> StructuralRelations = ["imports", "imports_from", "contains", "method"];

    public Analyzer(AnalyzerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public Task<AnalysisResult> ExecuteAsync(KnowledgeGraph graph, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var godNodes = FindGodNodes(graph);
        var surprisingConnections = FindSurprisingConnections(graph);
        var suggestedQuestions = GenerateSuggestedQuestions(graph);
        var statistics = CalculateStatistics(graph);

        var result = new AnalysisResult
        {
            GodNodes = godNodes,
            SurprisingConnections = surprisingConnections,
            SuggestedQuestions = suggestedQuestions,
            Statistics = statistics
        };

        return Task.FromResult(result);
    }

    private IReadOnlyList<GodNode> FindGodNodes(KnowledgeGraph graph)
    {
        var result = new List<GodNode>();
        var allNodes = graph.GetNodes().ToList();

        // Calculate degrees and sort
        var nodesByDegree = allNodes
            .Select(node => new { Node = node, Degree = graph.GetDegree(node.Id) })
            .OrderByDescending(x => x.Degree)
            .ToList();

        // Filter out file nodes and concept nodes
        foreach (var item in nodesByDegree)
        {
            if (IsFileNode(item.Node) || IsConceptNode(item.Node))
                continue;

            result.Add(new GodNode
            {
                Id = item.Node.Id,
                Label = item.Node.Label,
                EdgeCount = item.Degree
            });

            if (result.Count >= _options.TopGodNodesCount)
                break;
        }

        return result;
    }

    private IReadOnlyList<SurprisingConnection> FindSurprisingConnections(KnowledgeGraph graph)
    {
        // Identify unique source files
        var sourceFiles = graph.GetNodes()
            .Where(n => !string.IsNullOrEmpty(n.FilePath))
            .Select(n => n.FilePath!)
            .Distinct()
            .ToHashSet();

        bool isMultiSource = sourceFiles.Count > 1;

        if (isMultiSource)
        {
            return FindCrossFileSurprises(graph);
        }
        else
        {
            return FindCrossCommunityBridges(graph);
        }
    }

    private IReadOnlyList<SurprisingConnection> FindCrossFileSurprises(KnowledgeGraph graph)
    {
        var candidates = new List<(int Score, SurprisingConnection Connection)>();

        foreach (var edge in graph.GetEdges())
        {
            // Skip structural edges
            if (StructuralRelations.Contains(edge.Relationship))
                continue;

            // Skip concept and file nodes
            if (IsConceptNode(edge.Source) || IsConceptNode(edge.Target))
                continue;
            if (IsFileNode(edge.Source) || IsFileNode(edge.Target))
                continue;

            var sourceFile = edge.Source.FilePath ?? "";
            var targetFile = edge.Target.FilePath ?? "";

            // Only cross-file connections
            if (string.IsNullOrEmpty(sourceFile) || string.IsNullOrEmpty(targetFile) || sourceFile == targetFile)
                continue;

            var (score, reasons) = CalculateSurpriseScore(graph, edge, sourceFile, targetFile);

            candidates.Add((score, new SurprisingConnection
            {
                Source = edge.Source.Label,
                Target = edge.Target.Label,
                SourceFiles = [sourceFile, targetFile],
                Relationship = edge.Relationship,
                Confidence = edge.Confidence,
                Why = reasons.Count > 0 ? string.Join("; ", reasons) : "cross-file semantic connection"
            }));
        }

        return candidates
            .OrderByDescending(c => c.Score)
            .Take(_options.TopSurprisingConnections)
            .Select(c => c.Connection)
            .ToList();
    }

    private (int Score, List<string> Reasons) CalculateSurpriseScore(
        KnowledgeGraph graph,
        GraphEdge edge,
        string sourceFile,
        string targetFile)
    {
        int score = 0;
        var reasons = new List<string>();

        // 1. Confidence weight
        var confBonus = edge.Confidence switch
        {
            Confidence.Ambiguous => 3,
            Confidence.Inferred => 2,
            Confidence.Extracted => 1,
            _ => 1
        };
        score += confBonus;
        if (edge.Confidence is Confidence.Ambiguous or Confidence.Inferred)
        {
            reasons.Add($"{edge.Confidence.ToString().ToLowerInvariant()} connection - not explicitly stated in source");
        }

        // 2. Cross file-type bonus
        var catSource = GetFileCategory(sourceFile);
        var catTarget = GetFileCategory(targetFile);
        if (catSource != catTarget)
        {
            score += 2;
            reasons.Add($"crosses file types ({catSource} ↔ {catTarget})");
        }

        // 3. Cross-directory bonus
        if (GetTopLevelDir(sourceFile) != GetTopLevelDir(targetFile))
        {
            score += 2;
            reasons.Add("connects across different repos/directories");
        }

        // 4. Cross-community bonus
        if (edge.Source.Community.HasValue && edge.Target.Community.HasValue &&
            edge.Source.Community != edge.Target.Community)
        {
            score += 1;
            reasons.Add("bridges separate communities");
        }

        // 5. Peripheral to hub connection
        var degSource = graph.GetDegree(edge.Source.Id);
        var degTarget = graph.GetDegree(edge.Target.Id);
        if (Math.Min(degSource, degTarget) <= 2 && Math.Max(degSource, degTarget) >= 5)
        {
            score += 1;
            var peripheral = degSource <= 2 ? edge.Source.Label : edge.Target.Label;
            var hub = degSource <= 2 ? edge.Target.Label : edge.Source.Label;
            reasons.Add($"peripheral node `{peripheral}` unexpectedly reaches hub `{hub}`");
        }

        return (score, reasons);
    }

    private IReadOnlyList<SurprisingConnection> FindCrossCommunityBridges(KnowledgeGraph graph)
    {
        var result = new List<SurprisingConnection>();

        // Build community map
        var nodeCommunityMap = graph.GetNodes()
            .Where(n => n.Community.HasValue)
            .ToDictionary(n => n.Id, n => n.Community!.Value);

        if (nodeCommunityMap.Count == 0)
        {
            return result;
        }

        var seenPairs = new HashSet<(int, int)>();

        foreach (var edge in graph.GetEdges())
        {
            if (!nodeCommunityMap.TryGetValue(edge.Source.Id, out var commSource))
                continue;
            if (!nodeCommunityMap.TryGetValue(edge.Target.Id, out var commTarget))
                continue;
            if (commSource == commTarget)
                continue;

            // Skip file nodes and structural edges
            if (IsFileNode(edge.Source) || IsFileNode(edge.Target))
                continue;
            if (StructuralRelations.Contains(edge.Relationship))
                continue;

            // Deduplicate by community pair
            var pair = commSource < commTarget ? (commSource, commTarget) : (commTarget, commSource);
            if (seenPairs.Contains(pair))
                continue;
            seenPairs.Add(pair);

            result.Add(new SurprisingConnection
            {
                Source = edge.Source.Label,
                Target = edge.Target.Label,
                SourceFiles = [edge.Source.FilePath ?? "", edge.Target.FilePath ?? ""],
                Relationship = edge.Relationship,
                Confidence = edge.Confidence,
                Why = $"Bridges community {commSource} → community {commTarget}"
            });
        }

        // Sort by confidence: AMBIGUOUS, INFERRED, EXTRACTED
        var sorted = result.OrderBy(c => c.Confidence switch
        {
            Confidence.Ambiguous => 0,
            Confidence.Inferred => 1,
            Confidence.Extracted => 2,
            _ => 3
        }).ToList();

        return sorted.Take(_options.TopSurprisingConnections).ToList();
    }

    private IReadOnlyList<SuggestedQuestion> GenerateSuggestedQuestions(KnowledgeGraph graph)
    {
        var questions = new List<SuggestedQuestion>();

        // 1. AMBIGUOUS edges
        foreach (var edge in graph.GetEdges())
        {
            if (edge.Confidence == Confidence.Ambiguous)
            {
                questions.Add(new SuggestedQuestion
                {
                    Type = "ambiguous_edge",
                    Question = $"What is the exact relationship between `{edge.Source.Label}` and `{edge.Target.Label}`?",
                    Why = $"Edge tagged AMBIGUOUS (relation: {edge.Relationship}) - confidence is low."
                });
            }
        }

        // 2. Bridge nodes (nodes connecting multiple communities)
        var nodeCommunityMap = graph.GetNodes()
            .Where(n => n.Community.HasValue)
            .ToDictionary(n => n.Id, n => n.Community!.Value);

        var communityLabels = BuildCommunityLabels(graph);

        foreach (var node in graph.GetNodes())
        {
            if (IsFileNode(node) || IsConceptNode(node))
                continue;
            if (!nodeCommunityMap.TryGetValue(node.Id, out var nodeCommunity))
                continue;

            var neighbors = graph.GetNeighbors(node.Id).ToList();
            var neighborCommunities = neighbors
                .Where(n => nodeCommunityMap.ContainsKey(n.Id) && nodeCommunityMap[n.Id] != nodeCommunity)
                .Select(n => nodeCommunityMap[n.Id])
                .Distinct()
                .ToList();

            if (neighborCommunities.Count >= 2 && questions.Count(q => q.Type == "bridge_node") < 3)
            {
                var commLabel = communityLabels.GetValueOrDefault(nodeCommunity, $"Community {nodeCommunity}");
                var otherLabels = neighborCommunities
                    .Select(c => communityLabels.GetValueOrDefault(c, $"Community {c}"))
                    .ToList();

                questions.Add(new SuggestedQuestion
                {
                    Type = "bridge_node",
                    Question = $"Why does `{node.Label}` connect `{commLabel}` to {string.Join(", ", otherLabels.Select(l => $"`{l}`"))}?",
                    Why = $"This node bridges multiple communities - it's a cross-cutting concern."
                });
            }
        }

        // 3. God nodes with many INFERRED edges
        var godNodesWithInferred = graph.GetNodes()
            .Where(n => !IsFileNode(n))
            .Select(n => new
            {
                Node = n,
                InferredEdges = graph.GetEdges(n.Id).Count(e => e.Confidence == Confidence.Inferred)
            })
            .Where(x => x.InferredEdges >= 2)
            .OrderByDescending(x => x.InferredEdges)
            .Take(3)
            .ToList();

        foreach (var item in godNodesWithInferred)
        {
            var inferredEdges = graph.GetEdges(item.Node.Id)
                .Where(e => e.Confidence == Confidence.Inferred)
                .Take(2)
                .ToList();

            if (inferredEdges.Count >= 2)
            {
                var other1 = inferredEdges[0].Source.Id == item.Node.Id
                    ? inferredEdges[0].Target.Label
                    : inferredEdges[0].Source.Label;
                var other2 = inferredEdges[1].Source.Id == item.Node.Id
                    ? inferredEdges[1].Target.Label
                    : inferredEdges[1].Source.Label;

                questions.Add(new SuggestedQuestion
                {
                    Type = "verify_inferred",
                    Question = $"Are the {item.InferredEdges} inferred relationships involving `{item.Node.Label}` (e.g. with `{other1}` and `{other2}`) actually correct?",
                    Why = $"`{item.Node.Label}` has {item.InferredEdges} INFERRED edges - model-reasoned connections that need verification."
                });
            }
        }

        // 4. Isolated nodes
        var isolated = graph.GetNodes()
            .Where(n => !IsFileNode(n) && !IsConceptNode(n) && graph.GetDegree(n.Id) <= 1)
            .Take(3)
            .ToList();

        if (isolated.Count > 0)
        {
            var labels = isolated.Select(n => $"`{n.Label}`").ToList();
            var totalIsolated = graph.GetNodes()
                .Count(n => !IsFileNode(n) && !IsConceptNode(n) && graph.GetDegree(n.Id) <= 1);

            questions.Add(new SuggestedQuestion
            {
                Type = "isolated_nodes",
                Question = $"What connects {string.Join(", ", labels)} to the rest of the system?",
                Why = $"{totalIsolated} weakly-connected nodes found - possible documentation gaps or missing edges."
            });
        }

        // If no questions generated
        if (questions.Count == 0)
        {
            questions.Add(new SuggestedQuestion
            {
                Type = "no_signal",
                Question = null,
                Why = "Not enough signal to generate questions. The graph has no ambiguous edges, no bridge nodes, and all communities are well-connected."
            });
        }

        return questions.Take(_options.MaxSuggestedQuestions).ToList();
    }

    private GraphStatistics CalculateStatistics(KnowledgeGraph graph)
    {
        var nodes = graph.GetNodes().ToList();
        var edges = graph.GetEdges().ToList();

        int nodeCount = nodes.Count;
        int edgeCount = edges.Count;

        // Count communities
        int communityCount = nodes
            .Where(n => n.Community.HasValue)
            .Select(n => n.Community!.Value)
            .Distinct()
            .Count();

        // Calculate average degree
        double averageDegree = nodeCount > 0
            ? nodes.Sum(n => graph.GetDegree(n.Id)) / (double)nodeCount
            : 0.0;

        // Count isolated nodes (degree <= 1)
        int isolatedCount = nodes.Count(n => graph.GetDegree(n.Id) <= 1);

        return new GraphStatistics
        {
            NodeCount = nodeCount,
            EdgeCount = edgeCount,
            CommunityCount = communityCount,
            AverageDegree = Math.Round(averageDegree, 2),
            IsolatedNodeCount = isolatedCount
        };
    }

    private Dictionary<int, string> BuildCommunityLabels(KnowledgeGraph graph)
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

    private static bool IsFileNode(GraphNode node)
    {
        var label = node.Label;
        if (string.IsNullOrEmpty(label))
            return false;

        // File-level hub: label is a filename with code extension
        var parts = label.Split('.');
        if (parts.Length > 1 && CodeExtensions.Contains(parts[^1]))
            return true;

        // Method stub: .method_name()
        if (label.StartsWith('.') && label.EndsWith("()"))
            return true;

        // Module-level function stub: function_name() with degree <= 1
        if (label.EndsWith("()") && !label.Contains('.'))
            return true;

        return false;
    }

    private static bool IsConceptNode(GraphNode node)
    {
        // Concept nodes have no source file
        if (string.IsNullOrEmpty(node.FilePath))
            return true;

        // Or source file has no extension (not a real file)
        var fileName = Path.GetFileName(node.FilePath);
        if (!fileName.Contains('.'))
            return true;

        return false;
    }

    private static string GetFileCategory(string path)
    {
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        if (CodeExtensions.Contains(ext))
            return "code";
        if (ext == "pdf")
            return "paper";
        if (new[] { "png", "jpg", "jpeg", "webp", "gif", "svg" }.Contains(ext))
            return "image";
        return "doc";
    }

    private static string GetTopLevelDir(string path)
    {
        var parts = path.Replace('\\', '/').Split('/');
        return parts.Length > 0 ? parts[0] : path;
    }
}
