using System.Text.Json;
using System.Text.Json.Serialization;
using Graphify.Graph;

namespace Graphify.Export;

/// <summary>
/// Exports knowledge graphs to JSON format compatible with the Python graphify implementation.
/// </summary>
public sealed class JsonExporter : IGraphExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Format => "json";

    /// <summary>
    /// Export a knowledge graph to JSON format.
    /// </summary>
    /// <remarks>
    /// Output format matches Python's NetworkX node_link_data structure:
    /// {
    ///   "nodes": [{ "id": "...", "label": "...", "type": "...", "community": 0, "metadata": {} }],
    ///   "edges": [{ "source": "...", "target": "...", "relationship": "...", "weight": 1.0 }],
    ///   "metadata": { "nodeCount": N, "edgeCount": M, "communityCount": C, "generatedAt": "..." }
    /// }
    /// </remarks>
    public async Task ExportAsync(KnowledgeGraph graph, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        // Build the export structure
        var nodes = graph.GetNodes()
            .Select(n => new NodeDto
            {
                Id = n.Id,
                Label = n.Label,
                Type = n.Type,
                Community = n.Community,
                FilePath = n.FilePath,
                Language = n.Language,
                Confidence = n.Confidence.ToString().ToUpperInvariant(),
                Metadata = n.Metadata
            })
            .ToList();

        var edges = graph.GetEdges()
            .Select(e => new EdgeDto
            {
                Source = e.Source.Id,
                Target = e.Target.Id,
                Relationship = e.Relationship,
                Weight = e.Weight,
                Confidence = e.Confidence.ToString().ToUpperInvariant(),
                Metadata = e.Metadata
            })
            .ToList();

        // Count distinct communities
        var communityCount = nodes
            .Where(n => n.Community.HasValue)
            .Select(n => n.Community!.Value)
            .Distinct()
            .Count();

        var exportData = new GraphExportDto
        {
            Nodes = nodes,
            Edges = edges,
            Metadata = new ExportMetadataDto
            {
                NodeCount = nodes.Count,
                EdgeCount = edges.Count,
                CommunityCount = communityCount,
                GeneratedAt = DateTime.UtcNow
            }
        };

        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to file
        await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, exportData, JsonOptions, cancellationToken);
    }

    // DTOs for JSON serialization
    private sealed record GraphExportDto
    {
        [JsonPropertyName("nodes")]
        public required List<NodeDto> Nodes { get; init; }

        [JsonPropertyName("edges")]
        public required List<EdgeDto> Edges { get; init; }

        [JsonPropertyName("metadata")]
        public required ExportMetadataDto Metadata { get; init; }
    }

    private sealed record NodeDto
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("label")]
        public required string Label { get; init; }

        [JsonPropertyName("type")]
        public required string Type { get; init; }

        [JsonPropertyName("community")]
        public int? Community { get; init; }

        [JsonPropertyName("file_path")]
        public string? FilePath { get; init; }

        [JsonPropertyName("language")]
        public string? Language { get; init; }

        [JsonPropertyName("confidence")]
        public string? Confidence { get; init; }

        [JsonPropertyName("metadata")]
        public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    }

    private sealed record EdgeDto
    {
        [JsonPropertyName("source")]
        public required string Source { get; init; }

        [JsonPropertyName("target")]
        public required string Target { get; init; }

        [JsonPropertyName("relationship")]
        public required string Relationship { get; init; }

        [JsonPropertyName("weight")]
        public double Weight { get; init; } = 1.0;

        [JsonPropertyName("confidence")]
        public string? Confidence { get; init; }

        [JsonPropertyName("metadata")]
        public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    }

    private sealed record ExportMetadataDto
    {
        [JsonPropertyName("node_count")]
        public int NodeCount { get; init; }

        [JsonPropertyName("edge_count")]
        public int EdgeCount { get; init; }

        [JsonPropertyName("community_count")]
        public int CommunityCount { get; init; }

        [JsonPropertyName("generated_at")]
        public DateTime GeneratedAt { get; init; }
    }
}
