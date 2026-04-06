namespace Graphify.Export;

using Graphify.Graph;

/// <summary>
/// Handles graph export operations.
/// </summary>
public interface IGraphExporter
{
    /// <summary>
    /// Export format name (e.g., "json", "html", "cypher").
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Export a knowledge graph to the specified output path.
    /// </summary>
    /// <param name="graph">The knowledge graph to export.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ExportAsync(KnowledgeGraph graph, string outputPath, CancellationToken cancellationToken = default);
}
