using QuikGraph;

namespace Graphify.Models;

/// <summary>
/// Represents a directed edge in the knowledge graph.
/// Implements IEdge for QuikGraph compatibility.
/// </summary>
public sealed record GraphEdge : IEdge<GraphNode>
{
    /// <summary>
    /// Source node of this edge.
    /// </summary>
    public required GraphNode Source { get; init; }

    /// <summary>
    /// Target node of this edge.
    /// </summary>
    public required GraphNode Target { get; init; }

    /// <summary>
    /// Relationship type: imports, calls, uses, defines, contains, etc.
    /// </summary>
    public required string Relationship { get; init; }

    /// <summary>
    /// Edge weight (default 1.0 for unweighted edges).
    /// </summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>
    /// Confidence level for this relationship.
    /// </summary>
    public Confidence Confidence { get; init; } = Confidence.Extracted;

    /// <summary>
    /// Additional edge metadata (original source/target IDs, context, etc).
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    public override int GetHashCode() =>
        HashCode.Combine(Source.Id, Target.Id, Relationship);

    public bool Equals(GraphEdge? other) =>
        other is not null &&
        Source.Id == other.Source.Id &&
        Target.Id == other.Target.Id &&
        Relationship == other.Relationship;
}
