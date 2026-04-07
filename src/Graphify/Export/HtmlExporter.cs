using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Graphify.Graph;
using Graphify.Models;

namespace Graphify.Export;

/// <summary>
/// Exports a knowledge graph to an interactive HTML visualization using vis.js.
/// </summary>
public sealed class HtmlExporter : IGraphExporter
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.Default,
        WriteIndented = false
    };

    /// <inheritdoc />
    public string Format => "html";

    /// <inheritdoc />
    public Task ExportAsync(KnowledgeGraph graph, string outputPath, CancellationToken cancellationToken = default) =>
        ExportAsync(graph, outputPath, communityLabels: null, cancellationToken);

    /// <summary>
    /// Export the graph to an interactive HTML file.
    /// </summary>
    /// <param name="graph">The knowledge graph to export.</param>
    /// <param name="outputPath">Path to write the HTML file.</param>
    /// <param name="communityLabels">Optional labels for communities (e.g., "Database Layer", "UI Components").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if the graph is too large for visualization.</exception>
    public async Task ExportAsync(
        KnowledgeGraph graph,
        string outputPath,
        IReadOnlyDictionary<int, string>? communityLabels = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (graph.NodeCount > HtmlTemplate.MaxNodesForVisualization)
        {
            throw new InvalidOperationException(
                $"Graph has {graph.NodeCount} nodes - too large for HTML visualization. " +
                $"Maximum is {HtmlTemplate.MaxNodesForVisualization} nodes.");
        }

        // Collect community information
        var communities = BuildCommunityMap(graph);
        var degrees = graph.GetNodes().ToDictionary(n => n.Id, n => graph.GetDegree(n.Id));
        var maxDegree = degrees.Values.DefaultIfEmpty(1).Max();

        // Build vis.js node data
        var visNodes = BuildVisNodes(graph, communities, degrees, maxDegree, communityLabels);

        // Build vis.js edge data
        var visEdges = BuildVisEdges(graph);

        // Build legend data
        var legendData = BuildLegend(communities, communityLabels);

        // Serialize to JSON
        var nodesJson = JsonSerializer.Serialize(visNodes, _jsonOptions);
        var edgesJson = JsonSerializer.Serialize(visEdges, _jsonOptions);
        var legendJson = JsonSerializer.Serialize(legendData, _jsonOptions);

        // Generate stats
        var stats = $"{graph.NodeCount} nodes &middot; {graph.EdgeCount} edges &middot; {communities.Count} communities";

        // Generate HTML
        var title = SanitizeLabel(Path.GetFileName(outputPath));
        var html = HtmlTemplate.Generate(title, nodesJson, edgesJson, legendJson, stats);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to file
        await File.WriteAllTextAsync(outputPath, html, cancellationToken);
    }

    private static Dictionary<int, List<string>> BuildCommunityMap(KnowledgeGraph graph)
    {
        var communities = new Dictionary<int, List<string>>();

        foreach (var node in graph.GetNodes())
        {
            var communityId = node.Community ?? 0;
            if (!communities.ContainsKey(communityId))
            {
                communities[communityId] = [];
            }
            communities[communityId].Add(node.Id);
        }

        return communities;
    }

    private List<object> BuildVisNodes(
        KnowledgeGraph graph,
        Dictionary<int, List<string>> communities,
        Dictionary<string, int> degrees,
        int maxDegree,
        IReadOnlyDictionary<int, string>? communityLabels)
    {
        var visNodes = new List<object>();

        foreach (var node in graph.GetNodes())
        {
            var communityId = node.Community ?? 0;
            var color = HtmlTemplate.CommunityColors[communityId % HtmlTemplate.CommunityColors.Length];
            var label = SanitizeLabel(node.Label);
            var degree = degrees.GetValueOrDefault(node.Id, 1);

            // Node size proportional to degree (10-40 range)
            var size = 10 + 30 * ((double)degree / maxDegree);

            // Only show label for high-degree nodes by default; others show on hover
            var fontSize = degree >= maxDegree * 0.15 ? 12 : 0;

            var communityName = communityLabels?.GetValueOrDefault(communityId) ?? $"Community {communityId}";

            visNodes.Add(new
            {
                id = node.Id,
                label = label,
                color = new
                {
                    background = color,
                    border = color,
                    highlight = new { background = "#ffffff", border = color }
                },
                size = Math.Round(size, 1),
                font = new { size = fontSize, color = "#ffffff" },
                title = label,
                community = communityId,
                community_name = communityName,
                source_file = SanitizeLabel(node.FilePath ?? ""),
                file_type = node.Type,
                degree = degree
            });
        }

        return visNodes;
    }

    private static List<object> BuildVisEdges(KnowledgeGraph graph)
    {
        var visEdges = new List<object>();

        foreach (var edge in graph.GetEdges())
        {
            var confidence = edge.Confidence.ToString().ToUpperInvariant();
            var relation = edge.Relationship;

            visEdges.Add(new
            {
                from = edge.Source.Id,
                to = edge.Target.Id,
                label = relation,
                title = $"{relation} [{confidence}]",
                dashes = confidence != "EXTRACTED",
                width = confidence == "EXTRACTED" ? 2 : 1,
                color = new { opacity = confidence == "EXTRACTED" ? 0.7 : 0.35 },
                confidence = confidence
            });
        }

        return visEdges;
    }

    private static List<object> BuildLegend(
        Dictionary<int, List<string>> communities,
        IReadOnlyDictionary<int, string>? communityLabels)
    {
        var legendData = new List<object>();

        foreach (var (communityId, nodeIds) in communities.OrderBy(kvp => kvp.Key))
        {
            var color = HtmlTemplate.CommunityColors[communityId % HtmlTemplate.CommunityColors.Length];
            var label = communityLabels?.GetValueOrDefault(communityId) ?? $"Community {communityId}";
            var count = nodeIds.Count;

            legendData.Add(new
            {
                cid = communityId,
                color = color,
                label = label,
                count = count
            });
        }

        return legendData;
    }

    /// <summary>
    /// Sanitize labels for HTML output (strip control chars, HTML tags, limit length).
    /// Matches Python implementation in graphify/security.py.
    /// </summary>
    private static string SanitizeLabel(string input, int maxLength = 200)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // Remove control characters
        var sb = new StringBuilder();
        foreach (var c in input)
        {
            if (!char.IsControl(c))
            {
                sb.Append(c);
            }
        }

        var result = sb.ToString();

        // Strip HTML/script tags (simple pattern matching)
        result = System.Text.RegularExpressions.Regex.Replace(result, @"<script[^>]*>.*?</script>", "", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        result = System.Text.RegularExpressions.Regex.Replace(result, @"<[^>]+>", "");

        // Trim and limit length
        result = result.Trim();
        if (result.Length > maxLength)
        {
            result = result[..maxLength];
        }

        return result;
    }
}
