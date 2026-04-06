namespace Graphify.Pipeline;

/// <summary>
/// Configuration options for community detection clustering.
/// </summary>
public sealed record ClusterOptions
{
    /// <summary>
    /// Resolution parameter for modularity optimization.
    /// Higher values produce more, smaller communities.
    /// Default: 1.0 (standard modularity).
    /// </summary>
    public double Resolution { get; init; } = 1.0;

    /// <summary>
    /// Maximum number of iterations per phase.
    /// Default: 100.
    /// </summary>
    public int MaxIterations { get; init; } = 100;

    /// <summary>
    /// Minimum community size. Communities smaller than this will be merged.
    /// Default: 2 nodes.
    /// </summary>
    public int MinCommunitySize { get; init; } = 2;

    /// <summary>
    /// Maximum fraction of graph that a single community can contain before splitting.
    /// Default: 0.25 (25% of nodes).
    /// </summary>
    public double MaxCommunityFraction { get; init; } = 0.25;

    /// <summary>
    /// Minimum size for a community to be considered for splitting.
    /// Default: 10 nodes.
    /// </summary>
    public int MinSplitSize { get; init; } = 10;
}
