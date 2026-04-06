namespace Graphify.Models;

/// <summary>
/// Represents an edge extracted from source code.
/// Contains string IDs that will later be resolved to GraphNode objects.
/// </summary>
public record ExtractedEdge
{
    public required string Source { get; init; }
    public required string Target { get; init; }
    public required string Relation { get; init; }
    public required Confidence Confidence { get; init; }
    public required string SourceFile { get; init; }
    public string? SourceLocation { get; init; }
    public double Weight { get; init; } = 1.0;
}
