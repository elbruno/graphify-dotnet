using System.Globalization;
using System.Text.Json;
using Dahomey.Cbor.Attributes;
using SurrealDb.Net;
using SurrealDb.Net.Models;
using SurrealDb.Net.Models.Response;

namespace Graphify.Cli.Mcp;

/// <summary>
/// IGraphBackend implementation that queries a SurrealDB database directly via SurrealQL.
/// Used for the MCP serve command when --surreal-endpoint or --surreal-path is specified.
///
/// The graph is stored using SurrealDB graph edges (created with RELATE), so all graph
/// work — neighbourhood traversal, degree, and shortest-path — runs server-side via the
/// graph engine instead of downloading the whole graph and computing in memory.
/// </summary>
public sealed class SurrealDbGraphBackend : IGraphBackend, IAsyncDisposable
{
    private readonly ISurrealDbClient _db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SurrealDbGraphBackend(ISurrealDbClient db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is IAsyncDisposable disposable)
            await disposable.DisposeAsync();
    }

    public async Task<string> QueryAsync(string searchTerm, int limit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return JsonSerializer.Serialize(new ErrorResult { Error = "Search term cannot be empty" });

        try
        {
            var parameters = new Dictionary<string, object?>
            {
                ["term"] = searchTerm.ToLowerInvariant(),
                ["limit"] = limit
            };

            // Degree is computed server-side by counting graph edges, not by a
            // per-node relationship query (the previous N+1 pattern).
            var response = await _db.RawQuery(
                "SELECT id, label, kind, filePath, language, confidence, community, "
                    + "count(->relationship) + count(<-relationship) AS degree "
                    + "FROM entity "
                    + "WHERE string::contains(string::lowercase(id), $term) "
                    + "OR string::contains(string::lowercase(label), $term) "
                    + "OR string::contains(string::lowercase(kind), $term) "
                    + "LIMIT $limit",
                parameters,
                cancellationToken);

            if (response.FirstOk is not { } ok)
                return JsonSerializer.Serialize(new ErrorResult { Error = "Query failed" });

            var entities = ok.GetValues<EntityRow>().ToList();
            var results = new List<NodeResult>();

            foreach (var entity in entities)
            {
                var nodeId = ExtractNodeId(entity.Id);
                var connections = await FetchConnectionsAsync(entity.Id, 5, cancellationToken);

                results.Add(new NodeResult
                {
                    Id = nodeId,
                    Label = entity.label ?? nodeId,
                    Type = entity.kind ?? "unknown",
                    FilePath = entity.filePath,
                    Language = entity.language,
                    Confidence = entity.confidence,
                    Community = entity.community,
                    Degree = entity.degree,
                    Connections = connections
                });
            }

            return JsonSerializer.Serialize(new QueryResult
            {
                Query = searchTerm,
                ResultCount = results.Count,
                Results = results
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorResult { Error = $"Query failed: {ex.Message}" });
        }
    }

    public async Task<string> ExplainAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            return JsonSerializer.Serialize(new ErrorResult { Error = "Node ID is required" });

        try
        {
            var recordId = MakeEntityRecordId(nodeId);

            var nodeResponse = await _db.RawQuery(
                "SELECT id, label, kind, filePath, language, confidence, community FROM $node_id",
                new Dictionary<string, object?> { ["node_id"] = recordId },
                cancellationToken);

            if (nodeResponse.FirstOk is not { } nodeOk)
                return JsonSerializer.Serialize(new ErrorResult { Error = $"Node '{nodeId}' not found" });

            var entity = nodeOk.GetValues<EntityRow>().FirstOrDefault();
            if (entity == null)
                return JsonSerializer.Serialize(new ErrorResult { Error = $"Node '{nodeId}' not found" });

            // Outgoing edges: this node is the source (edge.in == node).
            var outRels = await SelectRelsAsync(
                "SELECT in, out, type, confidence FROM relationship WHERE in = $record_id",
                recordId, cancellationToken);

            // Incoming edges: this node is the target (edge.out == node).
            var inRels = await SelectRelsAsync(
                "SELECT in, out, type, confidence FROM relationship WHERE out = $record_id",
                recordId, cancellationToken);

            var outTargets = outRels.Select(r => r.Out).Where(x => x is not null).ToList();
            var inSources = inRels.Select(r => r.In).Where(x => x is not null).ToList();
            var labels = await ResolveLabelsAsync(outTargets.Concat(inSources), cancellationToken);

            var outEdges = outRels.Select(r => new EdgeResult
            {
                To = ExtractNodeId(r.Out),
                ToLabel = LabelFor(labels, r.Out),
                Relationship = r.type ?? "",
                Confidence = r.confidence
            }).ToList();

            var inEdges = inRels.Select(r => new EdgeResult
            {
                From = ExtractNodeId(r.In),
                FromLabel = LabelFor(labels, r.In),
                Relationship = r.type ?? "",
                Confidence = r.confidence
            }).ToList();

            return JsonSerializer.Serialize(new ExplainResult
            {
                Node = new ExplainNodeResult
                {
                    Id = ExtractNodeId(entity.Id),
                    Label = entity.label ?? nodeId,
                    Type = entity.kind ?? "unknown",
                    FilePath = entity.filePath,
                    Language = entity.language,
                    Confidence = entity.confidence,
                    Community = entity.community
                },
                Statistics = new ExplainStatistics
                {
                    TotalDegree = inEdges.Count + outEdges.Count,
                    IncomingConnections = inEdges.Count,
                    OutgoingConnections = outEdges.Count
                },
                IncomingEdges = inEdges,
                OutgoingEdges = outEdges
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorResult { Error = $"Explain failed: {ex.Message}" });
        }
    }

    public async Task<string> PathAsync(string sourceId, string targetId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
            return JsonSerializer.Serialize(new ErrorResult { Error = "Source and target IDs are required" });

        try
        {
            var sourceRecord = MakeEntityRecordId(sourceId);
            var targetRecord = MakeEntityRecordId(targetId);

            // Server-side shortest path using SurrealDB's graph algorithm (+shortest),
            // replacing the previous approach that downloaded the entire graph and ran
            // BFS in memory. The path is returned as the list of entity records along
            // the shortest route from source to target.
            var pathResponse = await _db.RawQuery(
                "RETURN type::field($source->{..+shortest=$target}->relationship->entity)",
                new Dictionary<string, object?> { ["source"] = sourceRecord, ["target"] = targetRecord },
                cancellationToken);

            var pathIds = new List<RecordId>();
            if (pathResponse.FirstOk is { } pathOk)
                pathIds = pathOk.GetValues<RecordId>().Where(x => x is not null).ToList();

            if (pathIds.Count == 0)
                return JsonSerializer.Serialize(new PathResult { Found = false });

            // SurrealDB's +shortest algorithm excludes the originating record from
            // the result by default, so prepend the source to produce a complete
            // source→target path (matching MemoryGraphBackend's BFS, which seeds
            // the path with the source node).
            pathIds.Insert(0, sourceRecord);

            var labels = await ResolveLabelsAsync(pathIds, cancellationToken);

            var path = pathIds.Select(id => new PathNodeResult
            {
                Id = ExtractNodeId(id),
                Label = LabelFor(labels, id),
                Type = labels.TryGetValue(RecordKey(id), out var e) ? (e.kind ?? "unknown") : "unknown"
            }).ToList();

            return JsonSerializer.Serialize(new PathResult
            {
                Found = true,
                PathLength = path.Count - 1,
                Path = path
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorResult { Error = $"Path failed: {ex.Message}" });
        }
    }

    public async Task<string> CommunitiesAsync(int? communityId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (communityId.HasValue)
            {
                var response = await _db.RawQuery(
                    "SELECT id, label, kind, filePath, "
                        + "count(->relationship) + count(<-relationship) AS degree "
                        + "FROM entity WHERE community = $communityId",
                    new Dictionary<string, object?> { ["communityId"] = communityId.Value },
                    cancellationToken);

                if (response.FirstOk is not { } ok)
                    return JsonSerializer.Serialize(new ErrorResult { Error = $"Community {communityId} not found" });

                var members = ok.GetValues<EntityRow>()
                    .Select(m => new CommunityMemberResult
                    {
                        Id = ExtractNodeId(m.Id),
                        Label = m.label ?? ExtractNodeId(m.Id),
                        Type = m.kind ?? "unknown",
                        FilePath = m.filePath,
                        Degree = m.degree
                    })
                    .OrderByDescending(m => m.Degree)
                    .ToList();

                if (members.Count == 0)
                    return JsonSerializer.Serialize(new ErrorResult { Error = $"Community {communityId} not found or has no members" });

                return JsonSerializer.Serialize(new CommunityDetailResult
                {
                    CommunityId = communityId.Value,
                    MemberCount = members.Count,
                    Members = members
                }, JsonOptions);
            }

            // List all communities with counts (server-side GROUP BY).
            var listResponse = await _db.RawQuery(
                "SELECT community, count() AS count FROM entity WHERE community != NONE GROUP BY community ORDER BY count DESC",
                cancellationToken: cancellationToken);

            var countResponse = await _db.RawQuery(
                "SELECT count() AS total FROM entity; SELECT count() AS total FROM entity WHERE community != NONE",
                cancellationToken: cancellationToken);

            int totalNodes = 0;
            int nodesInCommunities = 0;
            if (countResponse.Count > 0 && countResponse[0] is SurrealDbOkResult r0)
                totalNodes = r0.GetValues<TotalRow>().FirstOrDefault()?.total ?? 0;
            if (countResponse.Count > 1 && countResponse[1] is SurrealDbOkResult r1)
                nodesInCommunities = r1.GetValues<TotalRow>().FirstOrDefault()?.total ?? 0;

            var communities = new List<CommunitySummaryResult>();
            if (listResponse.FirstOk is { } listOk)
            {
                foreach (var group in listOk.GetValues<CommunityGroupRow>().ToList())
                {
                    var topMembers = await TopCommunityMembersAsync(group.community, 5, cancellationToken);
                    communities.Add(new CommunitySummaryResult
                    {
                        CommunityId = group.community,
                        MemberCount = group.count,
                        TopMembers = topMembers
                    });
                }
            }

            return JsonSerializer.Serialize(new CommunitiesListResult
            {
                TotalCommunities = communities.Count,
                NodesInCommunities = nodesInCommunities,
                NodesWithoutCommunity = totalNodes - nodesInCommunities,
                Communities = communities
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorResult { Error = $"Communities failed: {ex.Message}" });
        }
    }

    public async Task<string> AnalyzeAsync(int topN, CancellationToken cancellationToken = default)
    {
        try
        {
            // Aggregations run server-side in a single batched query.
            var response = await _db.RawQuery(
                """
                SELECT count() AS count FROM entity;
                SELECT count() AS count FROM relationship;
                SELECT community, count() AS count FROM entity WHERE community != NONE GROUP BY community;
                SELECT kind, count() AS count FROM entity GROUP BY kind ORDER BY count DESC;
                SELECT type, count() AS count FROM relationship GROUP BY type ORDER BY count DESC;
                """,
                cancellationToken: cancellationToken);

            if (response.HasErrors)
                return JsonSerializer.Serialize(new ErrorResult { Error = "Analyze query failed" });

            var nodeCount = response.GetValues<CountRow>(0).FirstOrDefault()?.count ?? 0;
            var edgeCount = response.GetValues<CountRow>(1).FirstOrDefault()?.count ?? 0;
            var communityGroups = response.GetValues<CommunityGroupRow>(2).ToList();
            var typeDistributions = response.GetValues<KindCountRow>(3).ToList();
            var relTypeDistributions = response.GetValues<TypeCountRow>(4).ToList();

            // Top nodes by degree — computed server-side via edge counts (no N+1).
            var topResponse = await _db.RawQuery(
                "SELECT id, label, kind, community, "
                    + "count(->relationship) + count(<-relationship) AS degree "
                    + "FROM entity ORDER BY degree DESC LIMIT $topN",
                new Dictionary<string, object?> { ["topN"] = topN },
                cancellationToken);

            var topNodes = new List<AnalyzeNodeResult>();
            if (topResponse.FirstOk is { } topOk)
            {
                topNodes = topOk.GetValues<EntityRow>().Select(e => new AnalyzeNodeResult
                {
                    Id = ExtractNodeId(e.Id),
                    Label = e.label ?? "",
                    Type = e.kind ?? "unknown",
                    Degree = e.degree,
                    Community = e.community
                }).ToList();
            }

            // Isolated nodes (no edges in either direction) — single server-side query.
            var isolatedResponse = await _db.RawQuery(
                "SELECT count() AS count FROM entity "
                    + "WHERE count(->relationship) = 0 AND count(<-relationship) = 0 GROUP ALL",
                cancellationToken: cancellationToken);

            int isolatedCount = 0;
            if (isolatedResponse.FirstOk is { } isoOk)
                isolatedCount = isoOk.GetValues<CountRow>().FirstOrDefault()?.count ?? 0;

            double averageDegree = nodeCount > 0 ? (double)edgeCount * 2 / nodeCount : 0;

            return JsonSerializer.Serialize(new AnalyzeResult
            {
                Statistics = new AnalyzeStatistics
                {
                    NodeCount = nodeCount,
                    EdgeCount = edgeCount,
                    CommunityCount = communityGroups.Count,
                    AverageDegree = Math.Round(averageDegree, 2),
                    IsolatedNodeCount = isolatedCount
                },
                TopNodes = topNodes,
                NodeTypes = typeDistributions.Select(t => new TypeCountResult
                {
                    Type = t.kind ?? "unknown",
                    Count = t.count
                }).ToList(),
                RelationshipTypes = relTypeDistributions.Select(t => new TypeCountResult
                {
                    Type = t.type ?? "unknown",
                    Count = t.count
                }).ToList()
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new ErrorResult { Error = $"Analyze failed: {ex.Message}" });
        }
    }

    // ── Private helpers ────────────────────────────────────────────────

    private async Task<List<RelRow>> SelectRelsAsync(string query, RecordId recordId, CancellationToken ct)
    {
        var response = await _db.RawQuery(
            query,
            new Dictionary<string, object?> { ["record_id"] = recordId },
            ct);

        return response.FirstOk is { } ok ? ok.GetValues<RelRow>().ToList() : [];
    }

    /// <summary>
    /// Fetches connections for a node from both edge directions in a single query.
    /// For outgoing edges the node is <c>in</c> (source); for incoming it is <c>out</c> (target).
    /// </summary>
    private async Task<List<ConnectionResult>> FetchConnectionsAsync(RecordId? entityId, int limit, CancellationToken ct)
    {
        if (entityId is null)
            return [];

        var response = await _db.RawQuery(
            "SELECT in, out, type, weight FROM relationship WHERE in = $record_id "
                + "UNION SELECT in, out, type, weight FROM relationship WHERE out = $record_id "
                + $"LIMIT {limit.ToString(CultureInfo.InvariantCulture)}",
            new Dictionary<string, object?> { ["record_id"] = entityId },
            ct);

        if (response.FirstOk is not { } ok)
            return [];

        return ok.GetValues<RelRow>().Select(r => new ConnectionResult
        {
            Source = ExtractNodeId(r.In),
            Target = ExtractNodeId(r.Out),
            Relationship = r.type ?? "",
            Weight = r.weight
        }).ToList();
    }

    private async Task<List<CommunityMemberResult>> TopCommunityMembersAsync(int community, int limit, CancellationToken ct)
    {
        var response = await _db.RawQuery(
            "SELECT id, label, kind, filePath, "
                + "count(->relationship) + count(<-relationship) AS degree "
                + "FROM entity WHERE community = $communityId ORDER BY degree DESC LIMIT $limit",
            new Dictionary<string, object?> { ["communityId"] = community, ["limit"] = limit },
            ct);

        if (response.FirstOk is not { } ok)
            return [];

        return ok.GetValues<EntityRow>().Select(m => new CommunityMemberResult
        {
            Id = ExtractNodeId(m.Id),
            Label = m.label ?? ExtractNodeId(m.Id),
            Type = m.kind ?? "unknown",
            FilePath = m.filePath,
            Degree = m.degree
        }).ToList();
    }

    /// <summary>
    /// Resolves label/kind for a set of record ids in a single batched query.
    /// </summary>
    private async Task<Dictionary<string, EntityRow>> ResolveLabelsAsync(IEnumerable<RecordId?> ids, CancellationToken ct)
    {
        var recordIds = ids.Where(x => x is not null).Cast<RecordId>().ToList();
        if (recordIds.Count == 0)
            return new Dictionary<string, EntityRow>();

        var response = await _db.RawQuery(
            "SELECT id, label, kind FROM $ids",
            new Dictionary<string, object?> { ["ids"] = recordIds },
            ct);

        var map = new Dictionary<string, EntityRow>();
        if (response.FirstOk is { } ok)
        {
            foreach (var e in ok.GetValues<EntityRow>())
            {
                var key = RecordKey(e.Id);
                if (!string.IsNullOrEmpty(key))
                    map[key] = e;
            }
        }
        return map;
    }

    private static string ExtractNodeId(RecordId? recordId)
    {
        if (recordId is null)
            return "";

        try
        {
            var escaped = recordId.DeserializeId<string>();
            return Uri.UnescapeDataString(escaped);
        }
        catch
        {
            return recordId.ToString() ?? "";
        }
    }

    private static string RecordKey(RecordId? recordId) => ExtractNodeId(recordId);

    private static string LabelFor(Dictionary<string, EntityRow> labels, RecordId? id)
    {
        var key = RecordKey(id);
        return labels.TryGetValue(key, out var e) ? (e.label ?? key) : key;
    }

    private static RecordId MakeEntityRecordId(string nodeId)
    {
        var escaped = Uri.EscapeDataString(nodeId);
        return (RecordId)("entity", escaped);
    }

    // ── Internal SurrealDB row types for CBOR deserialization ─────────

    internal sealed class EntityRow : Record
    {
        public string? label { get; set; }
        public string? kind { get; set; }
        public string? filePath { get; set; }
        public string? language { get; set; }
        public string? confidence { get; set; }
        public int? community { get; set; }
        public int degree { get; set; }
    }

    internal sealed class RelRow : Record
    {
        // 'in'/'out' are the SurrealDB graph-edge endpoint fields. They are C#
        // keywords, so map them from the CBOR field names via attributes.
        [CborProperty("in")]
        public RecordId? In { get; set; }

        [CborProperty("out")]
        public RecordId? Out { get; set; }

        public string? type { get; set; }
        public double weight { get; set; }
        public string? confidence { get; set; }
    }

    internal sealed class CountRow : Record
    {
        public int count { get; set; }
    }

    internal sealed class TotalRow : Record
    {
        public int total { get; set; }
    }

    internal sealed class CommunityGroupRow : Record
    {
        public int community { get; set; }
        public int count { get; set; }
    }

    internal sealed class KindCountRow : Record
    {
        public string? kind { get; set; }
        public int count { get; set; }
    }

    internal sealed class TypeCountRow : Record
    {
        public string? type { get; set; }
        public int count { get; set; }
    }
}
