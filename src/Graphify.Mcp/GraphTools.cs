using System.ComponentModel;
using System.Text.Json;
using Graphify.Graph;
using Graphify.Models;
using ModelContextProtocol.Server;

namespace Graphify.Mcp;

/// <summary>
/// MCP tools that expose knowledge graph operations.
/// </summary>
[McpServerToolType]
public class GraphTools
{
    private readonly KnowledgeGraph _graph;

    public GraphTools(KnowledgeGraph graph)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
    }

    [McpServerTool]
    [Description("Search for nodes and edges in the knowledge graph. Returns matching nodes with their connections.")]
    public string Query(
        [Description("Search term to match against node IDs, labels, or types")]
        string searchTerm,
        [Description("Maximum number of results to return (default: 10)")]
        int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return JsonSerializer.Serialize(new { error = "Search term cannot be empty" });
        }

        var searchLower = searchTerm.ToLowerInvariant();
        var matchingNodes = _graph.GetNodes()
            .Where(n => n.Id.ToLowerInvariant().Contains(searchLower) ||
                       n.Label.ToLowerInvariant().Contains(searchLower) ||
                       n.Type.ToLowerInvariant().Contains(searchLower))
            .Take(limit)
            .Select(n => new
            {
                id = n.Id,
                label = n.Label,
                type = n.Type,
                filePath = n.FilePath,
                language = n.Language,
                confidence = n.Confidence.ToString(),
                community = n.Community,
                degree = _graph.GetDegree(n.Id),
                connections = _graph.GetEdges(n.Id)
                    .Select(e => new
                    {
                        source = e.Source.Id,
                        target = e.Target.Id,
                        relationship = e.Relationship,
                        weight = e.Weight
                    })
                    .Take(5)
                    .ToList()
            })
            .ToList();

        return JsonSerializer.Serialize(new
        {
            query = searchTerm,
            resultCount = matchingNodes.Count,
            results = matchingNodes
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool]
    [Description("Find the shortest path between two nodes in the knowledge graph.")]
    public string Path(
        [Description("Starting node ID")]
        string sourceId,
        [Description("Target node ID")]
        string targetId)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
        {
            return JsonSerializer.Serialize(new { error = "Source and target IDs are required" });
        }

        var sourceNode = _graph.GetNode(sourceId);
        var targetNode = _graph.GetNode(targetId);

        if (sourceNode == null)
        {
            return JsonSerializer.Serialize(new { error = $"Source node '{sourceId}' not found" });
        }

        if (targetNode == null)
        {
            return JsonSerializer.Serialize(new { error = $"Target node '{targetId}' not found" });
        }

        try
        {
            // Simple BFS path finding
            var visited = new HashSet<string>();
            var queue = new Queue<(GraphNode Node, List<GraphNode> Path)>();
            queue.Enqueue((sourceNode, new List<GraphNode> { sourceNode }));
            visited.Add(sourceNode.Id);

            while (queue.Count > 0)
            {
                var (current, path) = queue.Dequeue();

                if (current.Id == targetNode.Id)
                {
                    return JsonSerializer.Serialize(new
                    {
                        found = true,
                        pathLength = path.Count - 1,
                        path = path.Select(n => new
                        {
                            id = n.Id,
                            label = n.Label,
                            type = n.Type
                        }).ToList()
                    }, new JsonSerializerOptions { WriteIndented = true });
                }

                foreach (var neighbor in _graph.GetNeighbors(current.Id))
                {
                    if (!visited.Contains(neighbor.Id))
                    {
                        visited.Add(neighbor.Id);
                        var newPath = new List<GraphNode>(path) { neighbor };
                        queue.Enqueue((neighbor, newPath));
                    }
                }
            }

            return JsonSerializer.Serialize(new
            {
                found = false,
                message = $"No path found between '{sourceId}' and '{targetId}'"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"Error finding path: {ex.Message}"
            });
        }
    }

    [McpServerTool]
    [Description("Explain a node's role and its connections in the knowledge graph.")]
    public string Explain(
        [Description("Node ID to explain")]
        string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return JsonSerializer.Serialize(new { error = "Node ID is required" });
        }

        var node = _graph.GetNode(nodeId);
        if (node == null)
        {
            return JsonSerializer.Serialize(new { error = $"Node '{nodeId}' not found" });
        }

        var inEdges = _graph.GetEdges(nodeId).Where(e => e.Target.Id == nodeId).ToList();
        var outEdges = _graph.GetEdges(nodeId).Where(e => e.Source.Id == nodeId).ToList();
        var degree = _graph.GetDegree(nodeId);

        var explanation = new
        {
            node = new
            {
                id = node.Id,
                label = node.Label,
                type = node.Type,
                filePath = node.FilePath,
                language = node.Language,
                confidence = node.Confidence.ToString(),
                community = node.Community
            },
            statistics = new
            {
                totalDegree = degree,
                incomingConnections = inEdges.Count,
                outgoingConnections = outEdges.Count
            },
            incomingEdges = inEdges.Select(e => new
            {
                from = e.Source.Id,
                fromLabel = e.Source.Label,
                relationship = e.Relationship,
                confidence = e.Confidence.ToString()
            }).ToList(),
            outgoingEdges = outEdges.Select(e => new
            {
                to = e.Target.Id,
                toLabel = e.Target.Label,
                relationship = e.Relationship,
                confidence = e.Confidence.ToString()
            }).ToList(),
            summary = GenerateNodeSummary(node, inEdges.Count, outEdges.Count, degree)
        };

        return JsonSerializer.Serialize(explanation, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool]
    [Description("List communities and their members in the knowledge graph.")]
    public string Communities(
        [Description("Optional: specific community ID to query (omit to list all)")]
        int? communityId = null)
    {
        var allNodes = _graph.GetNodes().ToList();
        var nodesWithCommunities = allNodes.Where(n => n.Community.HasValue).ToList();

        if (communityId.HasValue)
        {
            var communityNodes = _graph.GetNodesByCommunity(communityId.Value).ToList();
            
            if (!communityNodes.Any())
            {
                return JsonSerializer.Serialize(new { error = $"Community {communityId} not found or has no members" });
            }

            return JsonSerializer.Serialize(new
            {
                communityId = communityId.Value,
                memberCount = communityNodes.Count,
                members = communityNodes.Select(n => new
                {
                    id = n.Id,
                    label = n.Label,
                    type = n.Type,
                    filePath = n.FilePath,
                    degree = _graph.GetDegree(n.Id)
                }).OrderByDescending(n => n.degree).ToList()
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        var communities = nodesWithCommunities
            .GroupBy(n => n.Community!.Value)
            .Select(g => new
            {
                communityId = g.Key,
                memberCount = g.Count(),
                topMembers = g.OrderByDescending(n => _graph.GetDegree(n.Id))
                    .Take(5)
                    .Select(n => new
                    {
                        id = n.Id,
                        label = n.Label,
                        type = n.Type,
                        degree = _graph.GetDegree(n.Id)
                    })
                    .ToList()
            })
            .OrderByDescending(c => c.memberCount)
            .ToList();

        return JsonSerializer.Serialize(new
        {
            totalCommunities = communities.Count,
            nodesInCommunities = nodesWithCommunities.Count,
            nodesWithoutCommunity = allNodes.Count - nodesWithCommunities.Count,
            communities
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool]
    [Description("Run analysis on the knowledge graph and return structural insights.")]
    public string Analyze(
        [Description("Number of top nodes to include in analysis (default: 10)")]
        int topN = 10)
    {
        var allNodes = _graph.GetNodes().ToList();
        var allEdges = _graph.GetEdges().ToList();

        if (!allNodes.Any())
        {
            return JsonSerializer.Serialize(new { error = "Graph is empty" });
        }

        var topNodesByDegree = _graph.GetHighestDegreeNodes(topN).ToList();
        
        var nodesByCommunity = allNodes.Where(n => n.Community.HasValue)
            .GroupBy(n => n.Community!.Value)
            .Count();

        var isolatedNodes = allNodes.Where(n => _graph.GetDegree(n.Id) == 0).ToList();

        var nodesByType = allNodes.GroupBy(n => n.Type)
            .Select(g => new { type = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        var edgesByRelationship = allEdges.GroupBy(e => e.Relationship)
            .Select(g => new { relationship = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        var averageDegree = allNodes.Any() ? allNodes.Average(n => _graph.GetDegree(n.Id)) : 0;

        var analysis = new
        {
            statistics = new
            {
                nodeCount = allNodes.Count,
                edgeCount = allEdges.Count,
                communityCount = nodesByCommunity,
                averageDegree = Math.Round(averageDegree, 2),
                isolatedNodeCount = isolatedNodes.Count
            },
            topNodes = topNodesByDegree.Select(t => new
            {
                id = t.Node.Id,
                label = t.Node.Label,
                type = t.Node.Type,
                degree = t.Degree,
                community = t.Node.Community
            }).ToList(),
            nodeTypes = nodesByType,
            relationshipTypes = edgesByRelationship,
            insights = GenerateInsights(allNodes, allEdges, topNodesByDegree, isolatedNodes.Count, nodesByCommunity)
        };

        return JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string GenerateNodeSummary(GraphNode node, int inCount, int outCount, int totalDegree)
    {
        var role = node.Type switch
        {
            "class" => "class definition",
            "function" => "function or method",
            "module" => "module or namespace",
            "file" => "file",
            "concept" => "abstract concept",
            _ => $"{node.Type} entity"
        };

        var connectivityDesc = totalDegree switch
        {
            0 => "isolated (no connections)",
            1 => "minimally connected (1 connection)",
            < 5 => $"lightly connected ({totalDegree} connections)",
            < 20 => $"moderately connected ({totalDegree} connections)",
            _ => $"highly connected ({totalDegree} connections)"
        };

        return $"'{node.Label}' is a {role} that is {connectivityDesc}. " +
               $"It has {inCount} incoming and {outCount} outgoing relationships.";
    }

    private static List<string> GenerateInsights(
        List<GraphNode> allNodes,
        List<GraphEdge> allEdges,
        List<(GraphNode Node, int Degree)> topNodes,
        int isolatedCount,
        int communityCount)
    {
        var insights = new List<string>();

        if (topNodes.Any())
        {
            var topNode = topNodes.First();
            insights.Add($"The most connected node is '{topNode.Node.Label}' ({topNode.Node.Type}) with {topNode.Degree} connections.");
        }

        if (isolatedCount > 0)
        {
            var isolatedPct = (isolatedCount * 100.0) / allNodes.Count;
            insights.Add($"{isolatedCount} nodes ({isolatedPct:F1}%) are isolated with no connections.");
        }

        if (communityCount > 0)
        {
            insights.Add($"The graph is organized into {communityCount} communities, indicating modular structure.");
        }

        var avgDegree = allNodes.Any() ? allNodes.Average(n => n.Type == "class" ? 1 : 0) : 0;
        if (allEdges.Any())
        {
            var mostCommonRel = allEdges.GroupBy(e => e.Relationship)
                .OrderByDescending(g => g.Count())
                .First();
            insights.Add($"The most common relationship type is '{mostCommonRel.Key}' ({mostCommonRel.Count()} edges).");
        }

        return insights;
    }
}
