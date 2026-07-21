using Graphify.Graph;
using SurrealDb.Embedded.RocksDb;
using SurrealDb.Net;
using Microsoft.Extensions.DependencyInjection;
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
        var ns = _namespace ?? "graphify";
        var dbName = _database ?? "codebase";

        if (_testClient is not null)
        {
            await _testClient.Use(ns, dbName);
            await ExportToClientAsync(_testClient, graph, cancellationToken);
            return;
        }

        await using var db = CreateRocksDbClient(outputPath);
        await db.Use(ns, dbName);
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
        var ns = _namespace ?? "graphify";
        var dbName = _database ?? "codebase";

        // Connect without pre-selecting NS/DB — they may not exist yet on the
        // remote server and would cause a connection-time failure.
        var configuration = SurrealDbOptions
            .Create()
            .WithEndpoint(_endpoint!)
            .WithUsername(_username)
            .WithPassword(_password)
            .Build();

        await using var db = new SurrealDbClient(configuration);

        if (_username is not null)
        {
            await db.SignIn(new RootAuth
            {
                Username = _username,
                Password = _password ?? ""
            });
        }

        // Remote SurrealDB does not auto-create namespaces/databases; define
        // them (as root) before selecting them.
        await db.RawQuery($"DEFINE NAMESPACE IF NOT EXISTS {ns};", cancellationToken: cancellationToken);
        await db.RawQuery($"USE NS {ns}; DEFINE DATABASE IF NOT EXISTS {dbName};", cancellationToken: cancellationToken);
        await db.Use(ns, dbName);

        await ExportToClientAsync(db, graph, cancellationToken);
    }

    private static async Task ExportToClientAsync(ISurrealDbClient db,
        KnowledgeGraph graph, CancellationToken cancellationToken)
    {
        await DefineSchemaAsync(db);

        var nodes = graph.GetNodes().ToList();
        var edges = graph.GetEdges().ToList();

        // Use parameterized RawQuery (CREATE ... CONTENT) instead of the typed
        // Create<T> overload. Create<T>(table, data) deserializes the server
        // response back into T, and that response shape (single object vs array)
        // varies by SurrealDB version — the mismatch surfaces as a CBOR
        // "Expected major type Map (5)" error. RawQuery returns a generic
        // response whose result bytes are never deserialized into a typed
        // record, so it is immune to that version ambiguity.
        foreach (var node in nodes)
        {
            var escapedId = Uri.EscapeDataString(node.Id);
            var parameters = new Dictionary<string, object?>
            {
                ["id"] = escapedId,
                ["label"] = node.Label,
                ["kind"] = node.Type,
                ["filePath"] = node.FilePath,
                ["language"] = node.Language,
                ["confidence"] = node.Confidence.ToString().ToUpperInvariant(),
                ["community"] = node.Community,
                ["metadata"] = node.Metadata is { Count: > 0 }
                    ? node.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    : null
            };

            var response = await db.RawQuery(
                "CREATE type::thing('entity', $id) CONTENT { "
                    + "label: $label, kind: $kind, filePath: $filePath, "
                    + "language: $language, confidence: $confidence, "
                    + "community: $community, metadata: $metadata };",
                parameters,
                cancellationToken);
            response.EnsureAllOks();
        }

        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            var escapedSource = Uri.EscapeDataString(edge.Source.Id);
            var escapedTarget = Uri.EscapeDataString(edge.Target.Id);
            var parameters = new Dictionary<string, object?>
            {
                ["id"] = escapedSource + "->" + escapedTarget + "-" + i,
                ["source"] = (RecordId)("entity", escapedSource),
                ["target"] = (RecordId)("entity", escapedTarget),
                ["type"] = edge.Relationship,
                ["weight"] = edge.Weight,
                ["confidence"] = edge.Confidence.ToString().ToUpperInvariant(),
                ["metadata"] = edge.Metadata is { Count: > 0 }
                    ? edge.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    : null
            };

            var response = await db.RawQuery(
                "CREATE type::thing('relationship', $id) CONTENT { "
                    + "source: $source, target: $target, type: $type, "
                    + "weight: $weight, confidence: $confidence, "
                    + "metadata: $metadata };",
                parameters,
                cancellationToken);
            response.EnsureAllOks();
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
