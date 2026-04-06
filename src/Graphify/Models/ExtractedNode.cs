namespace Graphify.Models;

/// <summary>
/// Represents a node extracted from source code.
/// </summary>
public record ExtractedNode
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required FileType FileType { get; init; }
    public required string SourceFile { get; init; }
    public string? SourceLocation { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
