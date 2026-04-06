using System.Text;
using Graphify.Graph;
using Graphify.Models;

namespace Graphify.Export;

/// <summary>
/// Exports knowledge graph as an agent-crawlable wiki with index.md and one article per community.
/// </summary>
public sealed class WikiExporter : IGraphExporter
{
    public string Format => "wiki";

    /// <summary>
    /// Export graph to wiki format. outputPath should be a directory.
    /// </summary>
    public async Task ExportAsync(KnowledgeGraph graph, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var outputDir = new DirectoryInfo(outputPath);
        outputDir.Create();

        // Group nodes by community
        var communities = graph.GetNodes()
            .Where(n => n.Community.HasValue)
            .GroupBy(n => n.Community!.Value)
            .ToDictionary(g => g.Key, g => g.Select(n => n.Id).ToList());

        // Generate community labels (simple numbering for now)
        var communityLabels = communities.Keys.ToDictionary(id => id, id => $"Community {id}");

        // Calculate cohesion scores (simplified)
        var cohesionScores = communities.ToDictionary(
            kvp => kvp.Key,
            kvp => CalculateCohesion(graph, kvp.Value));

        // Get god nodes (top 10 by degree)
        var godNodes = graph.GetHighestDegreeNodes(10)
            .Select(x => new GodNode
            {
                Id = x.Node.Id,
                Label = x.Node.Label ?? x.Node.Id,
                EdgeCount = x.Degree
            })
            .ToList();

        // Generate community articles
        foreach (var (communityId, nodeIds) in communities.OrderByDescending(kvp => kvp.Value.Count))
        {
            var label = communityLabels[communityId];
            var cohesion = cohesionScores[communityId];
            var content = GenerateCommunityArticle(graph, communityId, nodeIds, label, communityLabels, cohesion);
            var filename = SafeFilename(label) + ".md";
            await File.WriteAllTextAsync(Path.Combine(outputDir.FullName, filename), content, cancellationToken);
        }

        // Generate god node articles
        foreach (var godNode in godNodes)
        {
            var node = graph.GetNode(godNode.Id);
            if (node != null)
            {
                var content = GenerateGodNodeArticle(graph, node, communityLabels);
                var filename = SafeFilename(godNode.Label) + ".md";
                await File.WriteAllTextAsync(Path.Combine(outputDir.FullName, filename), content, cancellationToken);
            }
        }

        // Generate index
        var indexContent = GenerateIndex(communities, communityLabels, godNodes, graph.NodeCount, graph.EdgeCount);
        await File.WriteAllTextAsync(Path.Combine(outputDir.FullName, "index.md"), indexContent, cancellationToken);
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
        return possibleEdges > 0 ? Math.Round(internalEdges / (double)possibleEdges, 2) : 0.0;
    }

    private static string GenerateCommunityArticle(
        KnowledgeGraph graph,
        int communityId,
        List<string> nodeIds,
        string label,
        Dictionary<int, string> communityLabels,
        double cohesion)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {label}");
        sb.AppendLine();
        sb.AppendLine($"> {nodeIds.Count} nodes · cohesion {cohesion:F2}");
        sb.AppendLine();

        // Key concepts (top nodes by degree)
        sb.AppendLine("## Key Concepts");
        sb.AppendLine();

        var topNodes = nodeIds
            .Select(id => (Id: id, Node: graph.GetNode(id), Degree: graph.GetDegree(id)))
            .Where(x => x.Node != null)
            .OrderByDescending(x => x.Degree)
            .Take(25)
            .ToList();

        foreach (var (id, node, degree) in topNodes)
        {
            var nodeLabel = node!.Label ?? id;
            var metadata = node.Metadata;
            var sourceFile = metadata?.TryGetValue("source_file", out var sf) == true ? sf : "";
            var sourceStr = !string.IsNullOrEmpty(sourceFile) ? $" — `{sourceFile}`" : "";
            sb.AppendLine($"- **{nodeLabel}** ({degree} connections){sourceStr}");
        }

        if (nodeIds.Count > topNodes.Count)
        {
            sb.AppendLine($"- *... and {nodeIds.Count - topNodes.Count} more nodes in this community*");
        }
        sb.AppendLine();

        // Cross-community relationships
        sb.AppendLine("## Relationships");
        sb.AppendLine();

        var crossLinks = GetCrossCommunityLinks(graph, nodeIds, communityId, communityLabels);
        if (crossLinks.Count > 0)
        {
            foreach (var (otherLabel, count) in crossLinks.Take(12))
            {
                sb.AppendLine($"- [[{otherLabel}]] ({count} shared connections)");
            }
        }
        else
        {
            sb.AppendLine("- No strong cross-community connections detected");
        }
        sb.AppendLine();

        // Source files
        var sourceFiles = nodeIds
            .Select(id => graph.GetNode(id))
            .Where(n => n != null && n.Metadata != null)
            .Select(n => n!.Metadata!.TryGetValue("source_file", out var sf) ? sf : "")
            .Where(sf => !string.IsNullOrEmpty(sf))
            .Distinct()
            .OrderBy(sf => sf)
            .Take(20)
            .ToList();

        if (sourceFiles.Count > 0)
        {
            sb.AppendLine("## Source Files");
            sb.AppendLine();
            foreach (var sourceFile in sourceFiles)
            {
                sb.AppendLine($"- `{sourceFile}`");
            }
            sb.AppendLine();
        }

        // Audit trail (confidence breakdown)
        sb.AppendLine("## Audit Trail");
        sb.AppendLine();

        var edgeCounts = new Dictionary<Confidence, int>
        {
            [Confidence.Extracted] = 0,
            [Confidence.Inferred] = 0,
            [Confidence.Ambiguous] = 0
        };

        foreach (var nodeId in nodeIds)
        {
            foreach (var edge in graph.GetEdges(nodeId))
            {
                edgeCounts[edge.Confidence]++;
            }
        }

        var totalEdges = edgeCounts.Values.Sum();
        if (totalEdges > 0)
        {
            foreach (var (confidence, count) in edgeCounts.OrderByDescending(kvp => kvp.Value))
            {
                var pct = Math.Round(count * 100.0 / totalEdges);
                sb.AppendLine($"- {confidence.ToString().ToUpperInvariant()}: {count} ({pct}%)");
            }
        }
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Part of the graphify knowledge wiki. See [[index]] to navigate.*");

        return sb.ToString();
    }

    private static string GenerateGodNodeArticle(
        KnowledgeGraph graph,
        GraphNode node,
        Dictionary<int, string> communityLabels)
    {
        var sb = new StringBuilder();
        var nodeLabel = node.Label ?? node.Id;
        var degree = graph.GetDegree(node.Id);

        sb.AppendLine($"# {nodeLabel}");
        sb.AppendLine();

        var metadata = node.Metadata;
        var sourceFile = metadata?.TryGetValue("source_file", out var sf) == true ? sf : "";
        var sourceStr = !string.IsNullOrEmpty(sourceFile) ? $" · `{sourceFile}`" : "";
        
        sb.AppendLine($"> God node · {degree} connections{sourceStr}");
        sb.AppendLine();

        if (node.Community.HasValue && communityLabels.TryGetValue(node.Community.Value, out var communityLabel))
        {
            sb.AppendLine($"**Community:** [[{communityLabel}]]");
            sb.AppendLine();
        }

        // Group connections by relation
        sb.AppendLine("## Connections by Relation");
        sb.AppendLine();

        var byRelation = new Dictionary<string, List<string>>();
        var neighbors = graph.GetNeighbors(node.Id).ToList();

        foreach (var neighbor in neighbors.OrderByDescending(n => graph.GetDegree(n.Id)))
        {
            var edges = graph.GetEdges().Where(e => 
                (e.Source.Id == node.Id && e.Target.Id == neighbor.Id) ||
                (e.Target.Id == node.Id && e.Source.Id == neighbor.Id));

            foreach (var edge in edges)
            {
                var relation = edge.Relationship ?? "related";
                var neighborLabel = neighbor.Label ?? neighbor.Id;
                var confStr = edge.Confidence != Confidence.Extracted ? $" `{edge.Confidence}`" : "";
                var entry = $"[[{neighborLabel}]]{confStr}";

                if (!byRelation.ContainsKey(relation))
                {
                    byRelation[relation] = new List<string>();
                }
                if (!byRelation[relation].Contains(entry))
                {
                    byRelation[relation].Add(entry);
                }
            }
        }

        foreach (var (relation, targets) in byRelation.OrderByDescending(kvp => kvp.Value.Count))
        {
            sb.AppendLine($"### {relation}");
            foreach (var target in targets.Take(20))
            {
                sb.AppendLine($"- {target}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Part of the graphify knowledge wiki. See [[index]] to navigate.*");

        return sb.ToString();
    }

    private static string GenerateIndex(
        Dictionary<int, List<string>> communities,
        Dictionary<int, string> communityLabels,
        IReadOnlyList<GodNode> godNodes,
        int totalNodes,
        int totalEdges)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Knowledge Graph Index");
        sb.AppendLine();
        sb.AppendLine("> Auto-generated by graphify. Start here — read community articles for context, then drill into god nodes for detail.");
        sb.AppendLine();
        sb.AppendLine($"**{totalNodes} nodes · {totalEdges} edges · {communities.Count} communities**");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        sb.AppendLine("## Communities");
        sb.AppendLine("(sorted by size, largest first)");
        sb.AppendLine();

        foreach (var (communityId, nodeIds) in communities.OrderByDescending(kvp => kvp.Value.Count))
        {
            var label = communityLabels[communityId];
            sb.AppendLine($"- [[{label}]] — {nodeIds.Count} nodes");
        }
        sb.AppendLine();

        if (godNodes.Count > 0)
        {
            sb.AppendLine("## God Nodes");
            sb.AppendLine("(most connected concepts — the load-bearing abstractions)");
            sb.AppendLine();

            foreach (var node in godNodes)
            {
                sb.AppendLine($"- [[{node.Label}]] — {node.EdgeCount} connections");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("*Generated by [graphify](https://github.com/safishamsi/graphify)*");

        return sb.ToString();
    }

    private static List<(string Label, int Count)> GetCrossCommunityLinks(
        KnowledgeGraph graph,
        List<string> nodeIds,
        int ownCommunityId,
        Dictionary<int, string> communityLabels)
    {
        var counts = new Dictionary<string, int>();

        foreach (var nodeId in nodeIds)
        {
            var neighbors = graph.GetNeighbors(nodeId);
            foreach (var neighbor in neighbors)
            {
                if (neighbor.Community.HasValue && 
                    neighbor.Community.Value != ownCommunityId &&
                    communityLabels.TryGetValue(neighbor.Community.Value, out var label))
                {
                    counts[label] = counts.GetValueOrDefault(label, 0) + 1;
                }
            }
        }

        return counts
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => (kvp.Key, kvp.Value))
            .ToList();
    }

    private static string SafeFilename(string name)
    {
        var safe = name
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace(" ", "_")
            .Replace(":", "-");
        
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(c, '_');
        }

        return safe;
    }
}
