using System.Text.Json;
using System.Text.Json.Serialization;
using Graphify.Graph;

namespace Graphify.Export;

/// <summary>
/// Exports knowledge graphs to SurrealDB format (embedded file-based database).
/// Interim implementation uses JSON serialization compatible with SurrealDB schema.
/// FUTURE: Replace with direct surrealdb.net client calls once stable API is confirmed.
/// </summary>
public sealed class SurrealDbExporter : IGraphExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Format => "surrealdb";

    public async Task ExportAsync(KnowledgeGraph graph, string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        // Ensure output directory exists
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Build export structure
        var nodes = graph.GetNodes()
            .Select(n => new SurrealDbNodeRecord
            {
                Id = $"entities:{Uri.EscapeDataString(n.Id)}",
                Label = n.Label,
                Kind = n.Type,
                FilePath = n.FilePath,
                Language = n.Language,
                Confidence = n.Confidence.ToString().ToUpperInvariant(),
                Community = n.Community,
                Metadata = n.Metadata
            })
            .ToList();

        var edges = graph.GetEdges()
            .Select(e => new SurrealDbEdgeRecord
            {
                Source = $"entities:{Uri.EscapeDataString(e.Source.Id)}",
                Target = $"entities:{Uri.EscapeDataString(e.Target.Id)}",
                Type = e.Relationship,
                Weight = e.Weight,
                Confidence = e.Confidence.ToString().ToUpperInvariant(),
                Metadata = e.Metadata
            })
            .ToList();

        var exportData = new SurrealDbExportDto
        {
            Entities = nodes,
            Relationships = edges,
            Metadata = new ExportMetadataDto
            {
                EntityCount = nodes.Count,
                RelationshipCount = edges.Count,
                GeneratedAt = DateTime.UtcNow
            }
        };

        // Write to file (JSON format for interim implementation)
        await using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, exportData, JsonOptions, cancellationToken);
    }

    private sealed record SurrealDbExportDto
    {
        [JsonPropertyName("entities")]
        public required List<SurrealDbNodeRecord> Entities { get; init; }

        [JsonPropertyName("relationships")]
        public required List<SurrealDbEdgeRecord> Relationships { get; init; }

        [JsonPropertyName("metadata")]
        public required ExportMetadataDto Metadata { get; init; }
    }

    private sealed record SurrealDbNodeRecord
    {
        [JsonPropertyName("id")]
        public required string Id { get; init; }

        [JsonPropertyName("label")]
        public required string Label { get; init; }

        [JsonPropertyName("kind")]
        public string? Kind { get; init; }

        [JsonPropertyName("filePath")]
        public string? FilePath { get; init; }

        [JsonPropertyName("language")]
        public string? Language { get; init; }

        [JsonPropertyName("confidence")]
        public string? Confidence { get; init; }

        [JsonPropertyName("community")]
        public int? Community { get; init; }

        [JsonPropertyName("metadata")]
        public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    }

    private sealed record SurrealDbEdgeRecord
    {
        [JsonPropertyName("source")]
        public required string Source { get; init; }

        [JsonPropertyName("target")]
        public required string Target { get; init; }

        [JsonPropertyName("type")]
        public required string Type { get; init; }

        [JsonPropertyName("weight")]
        public double Weight { get; init; } = 1.0;

        [JsonPropertyName("confidence")]
        public string? Confidence { get; init; }

        [JsonPropertyName("metadata")]
        public IReadOnlyDictionary<string, string>? Metadata { get; init; }
    }

    private sealed record ExportMetadataDto
    {
        [JsonPropertyName("entity_count")]
        public int EntityCount { get; init; }

        [JsonPropertyName("relationship_count")]
        public int RelationshipCount { get; init; }

        [JsonPropertyName("generated_at")]
        public DateTime GeneratedAt { get; init; }
    }
}
