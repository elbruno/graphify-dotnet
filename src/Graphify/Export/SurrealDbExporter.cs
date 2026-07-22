using Graphify.Graph;
using SurrealDb.Embedded.RocksDb;
using SurrealDb.Net;
using Microsoft.Extensions.DependencyInjection;
using SurrealDb.Net.Models;
using SurrealDb.Net.Models.Auth;
using SurrealDb.Net.Models.Response;

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

        var items = nodes.Select(node => new Dictionary<string, object?>
        {
            ["id"] = (RecordId)("entity", Uri.EscapeDataString(node.Id)),
            ["label"] = node.Label,
            ["kind"] = node.Type,
            ["filePath"] = node.FilePath,
            ["language"] = node.Language,
            ["confidence"] = node.Confidence.ToString().ToUpperInvariant(),
            ["community"] = node.Community,
            ["metadata"] = node.Metadata is { Count: > 0 }
                ? node.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                : null
        }).ToList();

        var rels = edges.Select(edge => new Dictionary<string, object?>
        {
            ["in"] = (RecordId)("entity", Uri.EscapeDataString(edge.Source.Id)),
            ["out"] = (RecordId)("entity", Uri.EscapeDataString(edge.Target.Id)),
            ["type"] = edge.Relationship,
            ["weight"] = edge.Weight,
            ["confidence"] = edge.Confidence.ToString().ToUpperInvariant(),
            ["metadata"] = edge.Metadata is { Count: > 0 }
                ? edge.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                : null
        }).ToList();

        // Clear the existing graph FIRST in its own request so the DELETEs are fully
        // committed before we try to INSERT with the same deterministic IDs. SurrealDB
        // transactions keep tombstoned records visible within the transaction scope, so
        // DELETE + INSERT in the same transaction would fail with "already exists".
        //
        // The risk of a crash between the DELETE and the INSERT (leaving an empty DB) is
        // acceptable: both `run` and `watch` re-export the complete graph, so the next
        // invocation restores it. Atomicity of the INSERT side is maintained by wrapping
        // the inserts in their own BEGIN/COMMIT.
        var deleteResponse = await db.RawQuery(
            "DELETE relationship; DELETE entity;",
            cancellationToken: cancellationToken);
        EnsureSuccess(deleteResponse, "DELETE relationship; DELETE entity;");

        if (items.Count == 0 && rels.Count == 0)
        {
            return; // Nothing to insert, graph is already empty
        }

        // Atomic insert: all or nothing. A concurrent reader sees either the empty post-
        // DELETE state or the complete new graph, never a half-inserted snapshot.
        var statements = new List<string> { "BEGIN;" };
        var parameters = new Dictionary<string, object?>();

        if (items.Count > 0)
        {
            statements.Add("INSERT INTO entity $items;");
            parameters["items"] = items;
        }

        if (rels.Count > 0)
        {
            statements.Add("INSERT RELATION INTO relationship $rels;");
            parameters["rels"] = rels;
        }

        statements.Add("COMMIT;");

        var query = string.Join(" ", statements);
        var response = await db.RawQuery(query, parameters, cancellationToken);
        EnsureSuccess(response, query);
    }

    /// <summary>
    /// Throws with the actual SurrealDB error text instead of the SDK's opaque
    /// "SurrealDbResponse is unsuccessful" message, so failures are diagnosable.
    /// </summary>
    private static void EnsureSuccess(SurrealDbResponse response, string query)
    {
        if (!response.HasErrors)
        {
            return;
        }

        var details = string.Join(" | ", response.Errors.Select(DescribeError));
        throw new InvalidOperationException(
            $"SurrealDB query failed: {details}{Environment.NewLine}Query: {query}");
    }

    private static string DescribeError(ISurrealDbErrorResult error) => error switch
    {
        SurrealDbErrorResult e => string.IsNullOrWhiteSpace(e.Details)
            ? $"{e.Status} ({e.Kind})"
            : e.Details,
        SurrealDbProtocolErrorResult p => string.Join(" - ",
            new[] { $"HTTP {(int)p.Code}", p.Description, p.Details, p.Information }
                .Where(s => !string.IsNullOrWhiteSpace(s))),
        _ => "unknown error result"
    };

    private static async Task DefineSchemaAsync(ISurrealDbClient db)
    {
        // Schema definition. Separate statements with semicolons for SurrealQL compatibility.
        // Indexes support the backend's server-side queries: community filtering/grouping
        // and relationship-type aggregation.
        await db.Query($"""
            DEFINE TABLE IF NOT EXISTS entity;
            DEFINE TABLE IF NOT EXISTS relationship;
            DEFINE INDEX IF NOT EXISTS idx_entity_community ON entity FIELDS community;
            DEFINE INDEX IF NOT EXISTS idx_entity_kind ON entity FIELDS kind;
            DEFINE INDEX IF NOT EXISTS idx_relationship_type ON relationship FIELDS type;
            """);
    }

}
