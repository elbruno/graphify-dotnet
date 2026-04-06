namespace Graphify.Pipeline;

/// <summary>
/// Configuration options for the Analyzer pipeline stage.
/// </summary>
public sealed record AnalyzerOptions
{
    /// <summary>
    /// Number of highest-degree nodes to report as god nodes.
    /// </summary>
    public int TopGodNodesCount { get; init; } = 10;

    /// <summary>
    /// Minimum edge weight for surprising connections analysis.
    /// </summary>
    public double MinSurpriseWeight { get; init; } = 0.5;

    /// <summary>
    /// Maximum number of suggested questions to generate.
    /// </summary>
    public int MaxSuggestedQuestions { get; init; } = 10;

    /// <summary>
    /// Number of top surprising connections to report.
    /// </summary>
    public int TopSurprisingConnections { get; init; } = 5;
}
