namespace Graphify.Models;

/// <summary>
/// Result of graph analysis, containing structural insights and questions.
/// </summary>
public sealed record AnalysisResult
{
    /// <summary>
    /// Most highly connected nodes (god nodes).
    /// </summary>
    public required IReadOnlyList<GodNode> GodNodes { get; init; }

    /// <summary>
    /// Non-obvious cross-file or cross-community connections.
    /// </summary>
    public required IReadOnlyList<SurprisingConnection> SurprisingConnections { get; init; }

    /// <summary>
    /// Generated questions the graph can help answer.
    /// </summary>
    public required IReadOnlyList<SuggestedQuestion> SuggestedQuestions { get; init; }

    /// <summary>
    /// Graph statistics summary.
    /// </summary>
    public required GraphStatistics Statistics { get; init; }
}

public sealed record GodNode
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required int EdgeCount { get; init; }
}

public sealed record SurprisingConnection
{
    public required string Source { get; init; }
    public required string Target { get; init; }
    public required IReadOnlyList<string> SourceFiles { get; init; }
    public required string Relationship { get; init; }
    public required Confidence Confidence { get; init; }
    public string? Why { get; init; }
}

public sealed record SuggestedQuestion
{
    public required string Type { get; init; }
    public string? Question { get; init; }
    public required string Why { get; init; }
}

public sealed record GraphStatistics
{
    public required int NodeCount { get; init; }
    public required int EdgeCount { get; init; }
    public required int CommunityCount { get; init; }
    public required double AverageDegree { get; init; }
    public required int IsolatedNodeCount { get; init; }
}
