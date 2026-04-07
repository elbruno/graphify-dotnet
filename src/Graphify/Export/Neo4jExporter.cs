using System.Text;
using Graphify.Graph;

namespace Graphify.Export;

/// <summary>
/// Exports knowledge graph as Neo4j Cypher CREATE statements.
/// Generates a .cypher file that can be executed in Neo4j Browser or cypher-shell.
/// </summary>
public sealed class Neo4jExporter : IGraphExporter
{
    public string Format => "neo4j";

    public async Task ExportAsync(KnowledgeGraph graph, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var cypher = GenerateCypher(graph);
        await File.WriteAllTextAsync(outputPath, cypher, cancellationToken);
    }

    private static string GenerateCypher(KnowledgeGraph graph)
    {
        var sb = new StringBuilder();

        // Header comment
        sb.AppendLine("// Knowledge Graph Export to Neo4j");
        sb.AppendLine($"// Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"// Nodes: {graph.NodeCount}, Edges: {graph.EdgeCount}");
        sb.AppendLine();
        sb.AppendLine("// Clear existing data (optional - uncomment if needed)");
        sb.AppendLine("// MATCH (n) DETACH DELETE n;");
        sb.AppendLine();
        sb.AppendLine("// Create nodes");
        sb.AppendLine();

        var nodes = graph.GetNodes().ToList();
        var nodeIdMap = new Dictionary<string, string>();

        // Generate CREATE statements for nodes
        foreach (var node in nodes)
        {
            var varName = GenerateVariableName(node.Id);
            nodeIdMap[node.Id] = varName;

            var label = EscapeCypher(node.Label ?? node.Id);
            var nodeType = SanitizeNodeType(node.Type);
            var community = node.Community?.ToString() ?? "null";

            sb.Append($"CREATE ({varName}:{nodeType} {{");
            sb.Append($"id: \"{EscapeCypher(node.Id)}\", ");
            sb.Append($"label: \"{label}\"");

            if (node.Community.HasValue)
            {
                sb.Append($", community: {community}");
            }

            // Add metadata properties
            if (node.Metadata != null)
            {
                foreach (var (key, value) in node.Metadata.OrderBy(kvp => kvp.Key))
                {
                    if (!string.IsNullOrWhiteSpace(value) && key != "label")
                    {
                        var safeKey = SanitizePropertyName(key);
                        var safeValue = EscapeCypher(value);
                        sb.Append($", {safeKey}: \"{safeValue}\"");
                    }
                }
            }

            sb.AppendLine("});");
        }

        sb.AppendLine();
        sb.AppendLine("// Create relationships");
        sb.AppendLine();

        // Generate CREATE statements for edges
        var edgeCount = 0;
        var edgesBatch = new List<string>();

        foreach (var edge in graph.GetEdges())
        {
            if (!nodeIdMap.TryGetValue(edge.Source.Id, out var sourceVar) ||
                !nodeIdMap.TryGetValue(edge.Target.Id, out var targetVar))
            {
                continue;
            }

            var relationshipType = SanitizeRelationshipType(edge.Relationship ?? "RELATED_TO");
            var weight = edge.Weight;
            var confidence = edge.Confidence.ToString().ToUpperInvariant();

            var edgeStmt = $"CREATE ({sourceVar})-[:{relationshipType} {{weight: {weight:F2}, confidence: \"{confidence}\"}}]->({targetVar});";
            edgesBatch.Add(edgeStmt);
            edgeCount++;

            // Write in batches of 100 for better readability
            if (edgeCount % 100 == 0)
            {
                foreach (var stmt in edgesBatch)
                {
                    sb.AppendLine(stmt);
                }
                sb.AppendLine();
                edgesBatch.Clear();
            }
        }

        // Write remaining edges
        foreach (var stmt in edgesBatch)
        {
            sb.AppendLine(stmt);
        }

        sb.AppendLine();
        sb.AppendLine("// Create indexes for better query performance");
        sb.AppendLine();

        // Get unique node types for index creation
        var nodeTypes = nodes
            .Select(n => SanitizeNodeType(n.Type))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        foreach (var nodeType in nodeTypes)
        {
            sb.AppendLine($"CREATE INDEX IF NOT EXISTS FOR (n:{nodeType}) ON (n.id);");
            sb.AppendLine($"CREATE INDEX IF NOT EXISTS FOR (n:{nodeType}) ON (n.label);");
        }

        if (nodes.Any(n => n.Community.HasValue))
        {
            sb.AppendLine();
            sb.AppendLine("// Index for community-based queries");
            foreach (var nodeType in nodeTypes)
            {
                sb.AppendLine($"CREATE INDEX IF NOT EXISTS FOR (n:{nodeType}) ON (n.community);");
            }
        }

        sb.AppendLine();
        sb.AppendLine("// Query examples:");
        sb.AppendLine("// - Find all nodes: MATCH (n) RETURN n LIMIT 25;");
        sb.AppendLine("// - Find nodes by type: MATCH (n:Class) RETURN n LIMIT 25;");
        sb.AppendLine("// - Find nodes in a community: MATCH (n) WHERE n.community = 1 RETURN n;");
        sb.AppendLine("// - Find highly connected nodes: MATCH (n) RETURN n, size((n)--()) as degree ORDER BY degree DESC LIMIT 10;");
        sb.AppendLine("// - Find paths: MATCH p=shortestPath((a)-[*]-(b)) WHERE a.id='Node1' AND b.id='Node2' RETURN p;");

        return sb.ToString();
    }

    private static string GenerateVariableName(string nodeId)
    {
        // Create a safe variable name from node ID
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

        // Limit length and ensure it's unique enough
        var result = varName.ToString();
        if (result.Length > 50)
        {
            result = result.Substring(0, 47) + Math.Abs(nodeId.GetHashCode()).ToString().Substring(0, 3);
        }

        return result;
    }

    private static string SanitizeNodeType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return "Node";
        }

        // Neo4j labels must start with a letter and contain only letters, numbers, and underscores
        var sanitized = new StringBuilder();
        var firstChar = true;

        foreach (var c in type)
        {
            if (char.IsLetter(c))
            {
                sanitized.Append(c);
                firstChar = false;
            }
            else if (!firstChar && char.IsDigit(c))
            {
                sanitized.Append(c);
            }
            else if (!firstChar && c == '_')
            {
                sanitized.Append(c);
            }
            else if (!firstChar)
            {
                sanitized.Append('_');
            }
        }

        var result = sanitized.ToString();
        return string.IsNullOrEmpty(result) ? "Node" : result;
    }

    private static string SanitizeRelationshipType(string relationship)
    {
        if (string.IsNullOrWhiteSpace(relationship))
        {
            return "RELATED_TO";
        }

        // Neo4j relationship types: uppercase with underscores
        var sanitized = new StringBuilder();

        foreach (var c in relationship)
        {
            if (char.IsLetterOrDigit(c))
            {
                sanitized.Append(char.ToUpperInvariant(c));
            }
            else
            {
                sanitized.Append('_');
            }
        }

        // Remove consecutive underscores
        var result = sanitized.ToString();
        while (result.Contains("__"))
        {
            result = result.Replace("__", "_");
        }

        result = result.Trim('_');

        return string.IsNullOrEmpty(result) ? "RELATED_TO" : result;
    }

    private static string SanitizePropertyName(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return "property";
        }

        // Neo4j property names: camelCase or snake_case
        var sanitized = new StringBuilder();
        var firstChar = true;

        foreach (var c in propertyName)
        {
            if (char.IsLetter(c))
            {
                sanitized.Append(firstChar ? char.ToLowerInvariant(c) : c);
                firstChar = false;
            }
            else if (!firstChar && char.IsDigit(c))
            {
                sanitized.Append(c);
            }
            else if (!firstChar && c == '_')
            {
                sanitized.Append(c);
            }
            else if (!firstChar)
            {
                sanitized.Append('_');
            }
        }

        var result = sanitized.ToString();
        return string.IsNullOrEmpty(result) ? "property" : result;
    }

    private static string EscapeCypher(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
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
