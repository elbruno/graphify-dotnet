namespace Graphify.Models;

/// <summary>
/// Result of extracting nodes and edges from source code.
/// Output from both AST and semantic extractors.
/// </summary>
public record ExtractionResult
{
    public required IReadOnlyList<ExtractedNode> Nodes { get; init; }
    public required IReadOnlyList<ExtractedEdge> Edges { get; init; }
    public string? RawText { get; init; }
    public required string SourceFilePath { get; init; }
    /// <summary>
    /// Relative path to the source file, relative to the project root.
    /// </summary>
    public string? RelativeSourceFilePath { get; init; }
    public ExtractionMethod Method { get; init; } = ExtractionMethod.Ast;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Dictionary<string, double>? ConfidenceScores { get; init; }
}

/// <summary>
/// Method used to extract the graph.
/// </summary>
public enum ExtractionMethod
{
    Ast,
    Semantic,
    Hybrid
}
