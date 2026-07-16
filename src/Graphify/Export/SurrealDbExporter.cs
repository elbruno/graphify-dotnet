using Graphify.Graph;
using SurrealDb.Embedded.RocksDb;
using SurrealDb.Net;
using SurrealDb.Net.Models;
using SurrealDb.Net.Models.Auth;

namespace Graphify.Export;

public sealed class SurrealDbExporter : IGraphExporter
{
    private readonly string? _endpoint;
    private readonly string? _username;
    private readonly string? _password;
    private readonly string? _namespace;
    private readonly string? _database;
    private readonly SurrealDbRocksDbClient? _testClient;

    public SurrealDbExporter(
        string? endpoint = null,
        string? username = null,
        string? password = null,
        string? ns = null,
        string? database = null)
    {
        _endpoint = endpoint;
        _username = username;
        _password = password;
        _namespace = ns;
        _database = database;
    }

    /// <summary>
    /// Testing constructor: accept a pre-opened client instead of creating one.
    /// The exporter will NOT dispose the client — caller owns the lifetime.
    /// </summary>
    /// <summary>
    /// Testing constructor: accept a pre-opened client (caller owns lifetime).
    /// Enables in-process verification without RocksDB lock contention.
    /// </summary>
    public SurrealDbExporter(SurrealDbRocksDbClient testClient)
    {
        _testClient = testClient;
    }

    public string Format => "surrealdb";

    public async Task ExportAsync(KnowledgeGraph graph, string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (_endpoint is not null)
        {
            await ExportRemoteAsync(graph, cancellationToken);
        }
        else
        {
            await ExportEmbeddedAsync(graph, outputPath, cancellationToken);
        }
    }

    internal async Task ExportEmbeddedAsync(KnowledgeGraph graph, string outputPath,
        CancellationToken cancellationToken)
    {
        if (_testClient is not null)
        {
            await _testClient.Use("graphify", "codebase");
            await ExportToClientAsync(_testClient, graph, cancellationToken);
            return;
        }

        await using var db = CreateRocksDbClient(outputPath);
        await db.Use("graphify", "codebase");
        await ExportToClientAsync(db, graph, cancellationToken);
    }

    private static SurrealDbRocksDbClient CreateRocksDbClient(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new SurrealDbRocksDbClient(outputPath);
    }

    private async Task ExportRemoteAsync(KnowledgeGraph graph,
        CancellationToken cancellationToken)
    {
        await using var db = new SurrealDbClient(_endpoint!);

        if (_username is not null)
        {
            await db.SignIn(new RootAuth
            {
                Username = _username,
                Password = _password ?? ""
            });
        }

        await db.Use(
            _namespace ?? "graphify",
            _database ?? "codebase");

        await ExportToClientAsync(db, graph, cancellationToken);
    }

    private static async Task ExportToClientAsync(ISurrealDbClient db,
        KnowledgeGraph graph, CancellationToken cancellationToken)
    {
        await DefineSchemaAsync(db);

        var nodes = graph.GetNodes().ToList();
        var edges = graph.GetEdges().ToList();

        foreach (var node in nodes)
        {
            var escapedId = Uri.EscapeDataString(node.Id);
            await db.Create("entity", new
            {
                Id = (RecordId)("entity", escapedId),
                label = node.Label,
                kind = node.Type,
                filePath = node.FilePath,
                language = node.Language,
                confidence = node.Confidence.ToString().ToUpperInvariant(),
                community = node.Community,
                metadata = node.Metadata is { Count: > 0 }
                    ? node.Metadata.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value)
                    : null
            });
        }

        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            var escapedSource = Uri.EscapeDataString(edge.Source.Id);
            var escapedTarget = Uri.EscapeDataString(edge.Target.Id);
            await db.Create("relationship", new
            {
                Id = (RecordId)("relationship", escapedSource + "->" + escapedTarget + "-" + i),
                source = (RecordId)("entity", escapedSource),
                target = (RecordId)("entity", escapedTarget),
                type = edge.Relationship,
                weight = edge.Weight,
                confidence = edge.Confidence.ToString().ToUpperInvariant(),
                metadata = edge.Metadata is { Count: > 0 }
                    ? edge.Metadata.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value)
                    : null
            });
        }
    }

    private static async Task DefineSchemaAsync(ISurrealDbClient db)
    {
        // Schema definition. Separate statements with semicolons for SurrealQL compatibility.
        await db.Query($"""
            DEFINE TABLE IF NOT EXISTS entity;
            DEFINE TABLE IF NOT EXISTS relationship;
            """);
    }

}
