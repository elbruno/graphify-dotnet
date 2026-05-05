namespace Graphify.Models;

/// <summary>
/// Represents a node in the knowledge graph.
/// Immutable record type for thread-safety and value semantics.
/// </summary>
public sealed record GraphNode
{
    /// <summary>
    /// Unique identifier for the node (e.g., "MyClass", "calculate_total()").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable label (may differ from Id for display purposes).
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Node type: function, class, module, concept, file, etc.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Source file path where this entity was extracted from (empty for concept nodes).
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Relative path to the source file, relative to the project root.
    /// Used for portability in exported graphs.
    /// </summary>
    public string? RelativePath { get; init; }

    /// <summary>
    /// Programming language (e.g., "csharp", "python", "typescript").
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Confidence level for this node's extraction.
    /// </summary>
    public Confidence Confidence { get; init; } = Confidence.Extracted;

    /// <summary>
    /// Community ID assigned by clustering algorithm (nullable before clustering).
    /// </summary>
    public int? Community { get; init; }

    /// <summary>
    /// Additional metadata as key-value pairs (source_location, semantic tags, etc).
    /// </summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    public override int GetHashCode() => Id.GetHashCode();

    public bool Equals(GraphNode? other) => other is not null && Id == other.Id;
}
