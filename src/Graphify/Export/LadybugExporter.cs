using System.Text;
using Graphify.Graph;
using Graphify.Models;

namespace Graphify.Export;

/// <summary>
/// Exports knowledge graph as a Ladybug (Kuzu) compatible Cypher script.
/// Generates a single .cypher file containing DDL (CREATE NODE TABLE / CREATE REL TABLE)
/// and DML (CREATE) statements that can be executed with the Ladybug CLI or embedded engine.
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
        sb.AppendLine("// Ladybug (Kuzu) Knowledge Graph Export");
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
        sb.AppendLine("    weight DOUBLE,");
        sb.AppendLine("    confidence STRING,");
        sb.AppendLine("    MANY_MANY");
        sb.AppendLine(");");
        sb.AppendLine();

        // Build node ID -> variable name map
        var nodes = graph.GetNodes().ToList();
        var nodeVarMap = new Dictionary<string, string>();

        // Nodes
        sb.AppendLine("// Create nodes");
        sb.AppendLine();

        foreach (var node in nodes)
        {
            var varName = GenerateVariableName(node.Id);
            nodeVarMap[node.Id] = varName;
            AppendCreateNode(sb, varName, node);
        }

        // Edges
        sb.AppendLine();
        sb.AppendLine("// Create relationships");
        sb.AppendLine();

        foreach (var edge in graph.GetEdges())
        {
            if (!nodeVarMap.TryGetValue(edge.Source.Id, out var sourceVar) ||
                !nodeVarMap.TryGetValue(edge.Target.Id, out var targetVar))
            {
                continue;
            }

            AppendCreateEdge(sb, sourceVar, targetVar, edge);
        }

        // Query examples
        sb.AppendLine();
        sb.AppendLine("// Query examples:");
        sb.AppendLine("// - Find all nodes: MATCH (n:GraphNode) RETURN n.id, n.label LIMIT 25;");
        sb.AppendLine("// - Find nodes by type: MATCH (n:GraphNode) WHERE n.nodeType = 'Class' RETURN n.id, n.label;");
        sb.AppendLine("// - Find nodes in a community: MATCH (n:GraphNode) WHERE n.community = 1 RETURN n.id, n.label;");
        sb.AppendLine("// - Find highly connected nodes: MATCH (n:GraphNode)-[e:GraphEdge]->() RETURN n.id, n.label, COUNT(e) AS degree ORDER BY degree DESC LIMIT 10;");
        sb.AppendLine("// - Find shortest path: MATCH p = shortestPath((a:GraphNode {id: 'NodeA'})-[:GraphEdge*]-(b:GraphNode {id: 'NodeB'})) RETURN p;");
        sb.AppendLine("// - Access metadata: MATCH (n:GraphNode) RETURN n.id, n.metadata['source_file'] AS file;");

        return sb.ToString();
    }

    private static void AppendCreateNode(StringBuilder sb, string varName, GraphNode node)
    {
        sb.Append($"CREATE ({varName}:GraphNode {{");
        sb.Append($"id: \"{EscapeLadybugString(node.Id)}\",");
        sb.Append($" label: \"{EscapeLadybugString(node.Label)}\",");
        sb.Append($" nodeType: \"{EscapeLadybugString(node.Type)}\"");

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
            var keys = new StringBuilder();
            var values = new StringBuilder();
            var first = true;

            foreach (var (key, value) in node.Metadata.OrderBy(kvp => kvp.Key))
            {
                if (!first)
                {
                    keys.Append(", ");
                    values.Append(", ");
                }

                keys.Append($"\"{EscapeLadybugString(key)}\"");
                values.Append($"\"{EscapeLadybugString(value)}\"");
                first = false;
            }

            sb.Append($", metadata: map([{keys}], [{values}])");
        }

        sb.AppendLine("});");
    }

    private static void AppendCreateEdge(StringBuilder sb, string sourceVar, string targetVar, GraphEdge edge)
    {
        var relType = EscapeLadybugString(edge.Relationship ?? "RELATED_TO");
        var confidence = EscapeLadybugString(edge.Confidence.ToString());
        var weight = edge.Weight.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

        sb.AppendLine($"CREATE ({sourceVar})-[:GraphEdge {{relationship: \"{relType}\", weight: {weight}, confidence: \"{confidence}\"}}]->({targetVar});");
    }

    private static string GenerateVariableName(string nodeId)
    {
        var varName = new StringBuilder();
        varName.Append('n');

        foreach (var c in nodeId)
        {
            if (char.IsLetterOrDigit(c))
            {
                varName.Append(c);
            }
            else
            {
                varName.Append('_');
            }
        }

        var result = varName.ToString();
        if (result.Length > 50)
        {
            result = result.Substring(0, 47) + Math.Abs(nodeId.GetHashCode()).ToString().Substring(0, 3);
        }

        return result;
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
