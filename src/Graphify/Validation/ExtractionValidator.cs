namespace Graphify.Validation;

using Graphify.Models;

/// <summary>
/// Validates extraction results against the graphify schema.
/// </summary>
public class ExtractionValidator : IGraphValidator
{
    public ValidationResult Validate(ExtractionResult extraction)
    {
        if (extraction == null)
        {
            return ValidationResult.Failure("Extraction result cannot be null");
        }

        var errors = new List<string>();

        ValidateNodes(extraction.Nodes, errors);
        ValidateEdges(extraction.Edges, extraction.Nodes, errors);

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    private static void ValidateNodes(IReadOnlyList<ExtractedNode> nodes, List<string> errors)
    {
        if (nodes == null)
        {
            errors.Add("Nodes list cannot be null");
            return;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            
            if (node == null)
            {
                errors.Add($"Node {i} is null");
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.Id))
            {
                errors.Add($"Node {i} has empty or null Id");
            }

            if (string.IsNullOrWhiteSpace(node.Label))
            {
                errors.Add($"Node {i} (id={node.Id ?? "?"}) has empty or null Label");
            }

            if (string.IsNullOrWhiteSpace(node.SourceFile))
            {
                errors.Add($"Node {i} (id={node.Id ?? "?"}) has empty or null SourceFile");
            }
        }
    }

    private static void ValidateEdges(IReadOnlyList<ExtractedEdge> edges, IReadOnlyList<ExtractedNode> nodes, List<string> errors)
    {
        if (edges == null)
        {
            errors.Add("Edges list cannot be null");
            return;
        }

        var nodeIds = nodes?.Select(n => n?.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet<string?>() ?? new HashSet<string?>();

        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            
            if (edge == null)
            {
                errors.Add($"Edge {i} is null");
                continue;
            }

            if (string.IsNullOrWhiteSpace(edge.Source))
            {
                errors.Add($"Edge {i} has empty or null Source");
            }
            else if (nodeIds.Count > 0 && !nodeIds.Contains(edge.Source))
            {
                errors.Add($"Edge {i} source '{edge.Source}' does not match any node id");
            }

            if (string.IsNullOrWhiteSpace(edge.Target))
            {
                errors.Add($"Edge {i} has empty or null Target");
            }
            else if (nodeIds.Count > 0 && !nodeIds.Contains(edge.Target))
            {
                errors.Add($"Edge {i} target '{edge.Target}' does not match any node id");
            }

            if (string.IsNullOrWhiteSpace(edge.Relation))
            {
                errors.Add($"Edge {i} has empty or null Relation");
            }

            if (string.IsNullOrWhiteSpace(edge.SourceFile))
            {
                errors.Add($"Edge {i} has empty or null SourceFile");
            }
        }
    }
}
