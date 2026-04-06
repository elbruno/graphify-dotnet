using System.Text;
using Graphify.Graph;
using Graphify.Models;

namespace Graphify.Pipeline;

/// <summary>
/// Generates a human-readable markdown report from graph analysis results.
/// Produces GRAPH_REPORT.md with summary, statistics, god nodes, communities, and insights.
/// </summary>
public sealed class ReportGenerator
{
    /// <summary>
    /// Generate a complete markdown report from graph and analysis results.
    /// </summary>
    public string Generate(
        KnowledgeGraph graph,
        AnalysisResult analysis,
        IReadOnlyDictionary<int, string> communityLabels,
        IReadOnlyDictionary<int, double> cohesionScores,
        string projectName)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(communityLabels);
        ArgumentNullException.ThrowIfNull(cohesionScores);

        var sb = new StringBuilder();
        var today = DateTimeOffset.Now.ToString("yyyy-MM-dd");

        // Header
        sb.AppendLine($"# Graph Report - {projectName}  ({today})");
        sb.AppendLine();

        // Summary
        AppendSummary(sb, graph, analysis);

        // God Nodes
        AppendGodNodes(sb, analysis.GodNodes);

        // Surprising Connections
        AppendSurprisingConnections(sb, analysis.SurprisingConnections);

        // Communities
        AppendCommunities(sb, graph, communityLabels, cohesionScores);

        // Suggested Questions
        AppendSuggestedQuestions(sb, analysis.SuggestedQuestions);

        // Knowledge Gaps
        AppendKnowledgeGaps(sb, graph, analysis);

        return sb.ToString();
    }

    private static void AppendSummary(StringBuilder sb, KnowledgeGraph graph, AnalysisResult analysis)
    {
        sb.AppendLine("## Summary");

        // Calculate confidence distribution
        var edges = graph.GetEdges().ToList();
        var totalEdges = edges.Count > 0 ? edges.Count : 1;
        var extractedCount = edges.Count(e => e.Confidence == Confidence.Extracted);
        var inferredCount = edges.Count(e => e.Confidence == Confidence.Inferred);
        var ambiguousCount = edges.Count(e => e.Confidence == Confidence.Ambiguous);

        var extractedPct = Math.Round(extractedCount * 100.0 / totalEdges);
        var inferredPct = Math.Round(inferredCount * 100.0 / totalEdges);
        var ambiguousPct = Math.Round(ambiguousCount * 100.0 / totalEdges);

        sb.AppendLine($"- {analysis.Statistics.NodeCount} nodes · {analysis.Statistics.EdgeCount} edges · {analysis.Statistics.CommunityCount} communities detected");
        sb.AppendLine($"- Extraction: {extractedPct}% EXTRACTED · {inferredPct}% INFERRED · {ambiguousPct}% AMBIGUOUS");
        
        if (inferredCount > 0)
        {
            var inferredEdges = edges.Where(e => e.Confidence == Confidence.Inferred).ToList();
            var avgConfidence = inferredEdges.Average(e => e.Weight);
            sb.AppendLine($"  INFERRED: {inferredCount} edges (avg confidence: {avgConfidence:F2})");
        }

        sb.AppendLine();
    }

    private static void AppendGodNodes(StringBuilder sb, IReadOnlyList<GodNode> godNodes)
    {
        sb.AppendLine("## God Nodes (most connected - your core abstractions)");
        
        if (godNodes.Count == 0)
        {
            sb.AppendLine("- No highly connected nodes detected");
        }
        else
        {
            for (int i = 0; i < godNodes.Count; i++)
            {
                var node = godNodes[i];
                sb.AppendLine($"{i + 1}. `{node.Label}` - {node.EdgeCount} edges");
            }
        }
        
        sb.AppendLine();
    }

    private static void AppendSurprisingConnections(StringBuilder sb, IReadOnlyList<SurprisingConnection> connections)
    {
        sb.AppendLine("## Surprising Connections (you probably didn't know these)");
        
        if (connections.Count == 0)
        {
            sb.AppendLine("- None detected - all connections are within the same source files.");
        }
        else
        {
            foreach (var conn in connections)
            {
                var confTag = conn.Confidence.ToString().ToUpperInvariant();
                var semanticTag = conn.Relationship == "semantically_similar_to" ? " [semantically similar]" : "";
                
                sb.AppendLine($"- `{conn.Source}` --{conn.Relationship}--> `{conn.Target}`  [{confTag}]{semanticTag}");
                
                if (conn.SourceFiles.Count >= 2)
                {
                    sb.Append($"  {conn.SourceFiles[0]} → {conn.SourceFiles[1]}");
                }
                
                if (!string.IsNullOrWhiteSpace(conn.Why))
                {
                    sb.Append($"  _{conn.Why}_");
                }
                
                sb.AppendLine();
            }
        }
        
        sb.AppendLine();
    }

    private static void AppendCommunities(
        StringBuilder sb, 
        KnowledgeGraph graph,
        IReadOnlyDictionary<int, string> communityLabels,
        IReadOnlyDictionary<int, double> cohesionScores)
    {
        sb.AppendLine("## Communities");
        
        var communitiesById = graph.GetNodes()
            .Where(n => n.Community.HasValue)
            .GroupBy(n => n.Community!.Value)
            .OrderByDescending(g => g.Count())
            .ToList();

        if (communitiesById.Count == 0)
        {
            sb.AppendLine("- No communities detected");
            sb.AppendLine();
            return;
        }

        foreach (var community in communitiesById)
        {
            var communityId = community.Key;
            var nodes = community.ToList();
            var label = communityLabels.TryGetValue(communityId, out var l) ? l : $"Community {communityId}";
            var cohesion = cohesionScores.TryGetValue(communityId, out var c) ? c : 0.0;

            sb.AppendLine();
            sb.AppendLine($"### Community {communityId} - \"{label}\"");
            sb.AppendLine($"Cohesion: {cohesion:F2}");

            // Show top nodes by degree
            var displayNodes = nodes
                .OrderByDescending(n => graph.GetDegree(n.Id))
                .Take(8)
                .Select(n => n.Label ?? n.Id)
                .ToList();

            var suffix = nodes.Count > 8 ? $" (+{nodes.Count - 8} more)" : "";
            sb.AppendLine($"Nodes ({nodes.Count}): {string.Join(", ", displayNodes)}{suffix}");
        }
        
        sb.AppendLine();
    }

    private static void AppendSuggestedQuestions(StringBuilder sb, IReadOnlyList<SuggestedQuestion> questions)
    {
        sb.AppendLine("## Suggested Questions");
        
        if (questions.Count == 0)
        {
            sb.AppendLine("- No questions suggested");
            sb.AppendLine();
            return;
        }

        var noSignal = questions.Count == 1 && questions[0].Type == "no_signal";
        
        if (noSignal)
        {
            sb.AppendLine($"_{questions[0].Why}_");
        }
        else
        {
            sb.AppendLine("_Questions this graph is uniquely positioned to answer:_");
            sb.AppendLine();
            
            foreach (var q in questions)
            {
                if (!string.IsNullOrWhiteSpace(q.Question))
                {
                    sb.AppendLine($"- **{q.Question}**");
                    sb.AppendLine($"  _{q.Why}_");
                }
            }
        }
        
        sb.AppendLine();
    }

    private static void AppendKnowledgeGaps(StringBuilder sb, KnowledgeGraph graph, AnalysisResult analysis)
    {
        var isolatedNodes = graph.GetNodes()
            .Where(n => graph.GetDegree(n.Id) <= 1)
            .Take(10)
            .ToList();

        var edges = graph.GetEdges().ToList();
        var totalEdges = edges.Count > 0 ? edges.Count : 1;
        var ambiguousPct = Math.Round(edges.Count(e => e.Confidence == Confidence.Ambiguous) * 100.0 / totalEdges);

        if (isolatedNodes.Count > 0 || ambiguousPct > 20)
        {
            sb.AppendLine("## Knowledge Gaps");
            
            if (isolatedNodes.Count > 0)
            {
                var isolatedLabels = isolatedNodes.Take(5).Select(n => $"`{n.Label ?? n.Id}`");
                var suffix = isolatedNodes.Count > 5 ? $" (+{isolatedNodes.Count - 5} more)" : "";
                sb.AppendLine($"- **{isolatedNodes.Count} isolated node(s):** {string.Join(", ", isolatedLabels)}{suffix}");
                sb.AppendLine("  These have ≤1 connection - possible missing edges or undocumented components.");
            }
            
            if (ambiguousPct > 20)
            {
                sb.AppendLine($"- **High ambiguity: {ambiguousPct}% of edges are AMBIGUOUS.** Review and refine extraction rules.");
            }
            
            sb.AppendLine();
        }
    }
}
