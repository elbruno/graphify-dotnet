using System.Text;
using Graphify.Graph;
using Graphify.Models;

namespace Graphify.Export;

/// <summary>
/// Exports knowledge graph as a Ladybug compatible Cypher script.
/// Generates a single .cypher file containing DDL (CREATE NODE TABLE / CREATE REL TABLE)
/// and DML (MATCH/CREATE) statements that can be executed with the Ladybug CLI or embedded engine.
///
/// Schema design:
/// - One node table: GraphNode(id STRING PRIMARY KEY, label STRING, nodeType STRING, ...)
/// - Metadata stored as MAP(STRING, STRING) — Ladybug's native dictionary type
/// - One relationship table: GraphEdge(FROM GraphNode TO GraphNode, relationship STRING, ...)
/// </summary>
public sealed class LadybugExporter : IGraphExporter
{
    public string Format => "ladybug";

    public async Task ExportAsync(KnowledgeGraph graph, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var cypher = GenerateLadybugCypher(graph);
        await File.WriteAllTextAsync(outputPath, cypher, cancellationToken);
    }

    private static string GenerateLadybugCypher(KnowledgeGraph graph)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("// Ladybug Knowledge Graph Export");
        sb.AppendLine($"// Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"// Nodes: {graph.NodeCount}, Edges: {graph.EdgeCount}");
        sb.AppendLine();

        // DDL — Node table
        sb.AppendLine("// Create node table");
        sb.AppendLine("CREATE NODE TABLE GraphNode (");
        sb.AppendLine("    id STRING PRIMARY KEY,");
        sb.AppendLine("    label STRING,");
        sb.AppendLine("    nodeType STRING,");
        sb.AppendLine("    filePath STRING,");
        sb.AppendLine("    relativePath STRING,");
        sb.AppendLine("    language STRING,");
        sb.AppendLine("    community INT64,");
        sb.AppendLine("    confidence STRING,");
        sb.AppendLine("    metadata MAP(STRING, STRING)");
        sb.AppendLine(");");
        sb.AppendLine();

        // DDL — Relationship table
        sb.AppendLine("// Create relationship table");
        sb.AppendLine("CREATE REL TABLE GraphEdge (");
        sb.AppendLine("    FROM GraphNode TO GraphNode,");
        sb.AppendLine("    relationship STRING,");
        sb.AppendLine("    metadata MAP(STRING, STRING),");
        sb.AppendLine("    weight DOUBLE,");
        sb.AppendLine("    confidence STRING,");
        sb.AppendLine("    MANY_MANY");
        sb.AppendLine(");");
        sb.AppendLine();

        // DML — Nodes
        sb.AppendLine("// Create nodes");
        foreach (var node in graph.GetNodes())
        {
            AppendCreateNode(sb, node);
        }
        sb.AppendLine();

        // DML — Edges (using MATCH to link existing nodes)
        sb.AppendLine("// Create edges");
        var nodeIds = new HashSet<string>(graph.GetNodes().Select(n => n.Id));
        foreach (var edge in graph.GetEdges())
        {
            if (edge.Source != null && edge.Target != null &&
                nodeIds.Contains(edge.Source.Id) && nodeIds.Contains(edge.Target.Id))
            {
                AppendCreateEdge(sb, edge);
            }
        }
        
        return sb.ToString();
    }

    private static void AppendCreateNode(StringBuilder sb, GraphNode node)
    {
        sb.Append($"CREATE (:GraphNode {{id: \"{EscapeLadybugString(node.Id)}\"");
        sb.Append($", label: \"{EscapeLadybugString(node.Label)}\"");
        sb.Append($", nodeType: \"{EscapeLadybugString(node.Type)}\"");

        if (!string.IsNullOrEmpty(node.FilePath))
        {
            sb.Append($", filePath: \"{EscapeLadybugString(node.FilePath)}\"");
        }

        if (!string.IsNullOrEmpty(node.RelativePath))
        {
            sb.Append($", relativePath: \"{EscapeLadybugString(node.RelativePath)}\"");
        }

        if (!string.IsNullOrEmpty(node.Language))
        {
            sb.Append($", language: \"{EscapeLadybugString(node.Language)}\"");
        }

        if (node.Community.HasValue)
        {
            sb.Append($", community: {node.Community.Value}");
        }

        sb.Append($", confidence: \"{EscapeLadybugString(node.Confidence.ToString())}\"");

        // Metadata as MAP(STRING, STRING)
        if (node.Metadata is { Count: > 0 })
        {
            sb.Append($", metadata: {FormatMetadataMap(node.Metadata)}");
        }

        sb.AppendLine("});");
    }

    private static void AppendCreateEdge(StringBuilder sb, GraphEdge edge)
    {
        var relType = EscapeLadybugString(edge.Relationship ?? "RELATED_TO");
        var confidence = EscapeLadybugString(edge.Confidence.ToString());
        var weight = edge.Weight.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        var sourceId = EscapeLadybugString(edge.Source!.Id);
        var targetId = EscapeLadybugString(edge.Target!.Id);

        sb.Append($"MATCH (s:GraphNode {{id: \"{sourceId}\"}}), (t:GraphNode {{id: \"{targetId}\"}}) ");
        sb.Append($"CREATE (s)-[:GraphEdge {{relationship: \"{relType}\", weight: {weight}, confidence: \"{confidence}\"");

        if (edge.Metadata is { Count: > 0 })
        {
            sb.Append($", metadata: {FormatMetadataMap(edge.Metadata)}");
        }

        sb.AppendLine("}]->(t);");
    }

    private static string FormatMetadataMap(IReadOnlyDictionary<string, string> metadata)
    {
        var keys = new StringBuilder();
        var values = new StringBuilder();
        var first = true;

        foreach (var kvp in metadata.OrderBy(kvp => kvp.Key))
        {
            if (!first)
            {
                keys.Append(", ");
                values.Append(", ");
            }

            keys.Append($"\"{EscapeLadybugString(kvp.Key)}\"");
            values.Append($"\"{EscapeLadybugString(kvp.Value)}\"");
            first = false;
        }

        return $"map([{keys}], [{values}])";
    }

    /// <summary>
    /// Escapes a string for use in Ladybug Cypher literal.
    /// Handles backslashes, double quotes, and common control characters.
    /// </summary>
    private static string EscapeLadybugString(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
