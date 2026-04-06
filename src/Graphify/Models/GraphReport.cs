namespace Graphify.Models;

/// <summary>
/// Complete graph report for export (JSON, HTML, vis.js, etc).
/// </summary>
public sealed record GraphReport
{
    /// <summary>
    /// Report title (e.g., "Knowledge Graph: MyProject").
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// High-level summary text.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Detected communities with labels and members.
    /// </summary>
    public required IReadOnlyList<Community> Communities { get; init; }

    /// <summary>
    /// Most connected nodes (god nodes).
    /// </summary>
    public required IReadOnlyList<GodNode> GodNodes { get; init; }

    /// <summary>
    /// Non-obvious connections worth investigating.
    /// </summary>
    public required IReadOnlyList<SurprisingConnection> SurprisingEdges { get; init; }

    /// <summary>
    /// Timestamp when this report was generated.
    /// </summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Graph statistics.
    /// </summary>
    public GraphStatistics? Statistics { get; init; }
}

public sealed record Community
{
    public required int Id { get; init; }
    public required string Label { get; init; }
    public required IReadOnlyList<string> Members { get; init; }
    public double? CohesionScore { get; init; }
}
