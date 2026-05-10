using Graphify.Graph;
using Graphify.Models;

namespace Graphify.Pipeline;

/// <summary>
/// Community detection pipeline stage using Louvain algorithm.
/// Takes a KnowledgeGraph and assigns community IDs to all nodes.
/// </summary>
public sealed class ClusterEngine : IPipelineStage<KnowledgeGraph, KnowledgeGraph>
{
    private readonly ClusterOptions _options;

    public ClusterEngine(ClusterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public Task<KnowledgeGraph> ExecuteAsync(KnowledgeGraph graph, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (graph.NodeCount == 0)
        {
            return Task.FromResult(graph);
        }

        var communities = DetectCommunities(graph);
        graph.AssignCommunities(communities);

        return Task.FromResult(graph);
    }

    /// <summary>
    /// Run Louvain community detection. Returns {community_id: [node_ids]}.
    /// </summary>
    private Dictionary<int, IReadOnlyList<string>> DetectCommunities(KnowledgeGraph graph)
    {
        if (graph.EdgeCount == 0)
        {
            // No edges - each node is its own community
            var isolatedCommunities = new Dictionary<int, IReadOnlyList<string>>();
            var isolatedNodes = graph.GetNodes().Select(n => n.Id).OrderBy(id => id).ToList();
            for (int i = 0; i < isolatedNodes.Count; i++)
            {
                isolatedCommunities[i] = new List<string> { isolatedNodes[i] };
            }
            return isolatedCommunities;
        }

        // Phase 1: Initialize - each node is its own community
        var nodes = graph.GetNodes().ToList();
        int nodeCount = nodes.Count;

        // Precompute weighted adjacency (neighbor -> aggregated edge weight) and per-node degree.
        // This collapses parallel edges and lets the Louvain main loop run in O(E) per iteration
        // instead of re-walking the whole graph for each gain evaluation.
        var adjacency = new Dictionary<string, Dictionary<string, double>>(nodeCount);
        var nodeDegree = new Dictionary<string, double>(nodeCount);

        foreach (var node in nodes)
        {
            var neighborWeights = new Dictionary<string, double>();
            double degree = 0.0;
            foreach (var edge in graph.GetEdges(node.Id))
            {
                var neighborId = edge.Source.Id == node.Id ? edge.Target.Id : edge.Source.Id;
                if (neighborWeights.TryGetValue(neighborId, out var existing))
                {
                    neighborWeights[neighborId] = existing + edge.Weight;
                }
                else
                {
                    neighborWeights[neighborId] = edge.Weight;
                }
                degree += edge.Weight;
            }
            adjacency[node.Id] = neighborWeights;
            nodeDegree[node.Id] = degree;
        }

        double totalEdgeWeight = nodeDegree.Values.Sum() / 2.0;
        double m2 = 2.0 * totalEdgeWeight;
        double m2Sq = m2 * m2;

        var nodeToCommunity = new Dictionary<string, int>(nodeCount);
        var communityTotalDegree = new Dictionary<int, double>(nodeCount);
        for (int i = 0; i < nodeCount; i++)
        {
            var id = nodes[i].Id;
            nodeToCommunity[id] = i;
            communityTotalDegree[i] = nodeDegree[id];
        }

        bool improved = true;
        int iteration = 0;
        var edgesToCommunity = new Dictionary<int, double>();

        while (improved && iteration < _options.MaxIterations)
        {
            improved = false;
            iteration++;

            foreach (var node in nodes)
            {
                var nodeId = node.Id;
                var currentCommunity = nodeToCommunity[nodeId];
                var nDegree = nodeDegree[nodeId];
                var neighbors = adjacency[nodeId];

                // Aggregate edge weight from this node into each neighboring community in one pass.
                edgesToCommunity.Clear();
                foreach (var (neighborId, weight) in neighbors)
                {
                    if (neighborId == nodeId) continue;
                    var neighborCommunity = nodeToCommunity[neighborId];
                    if (edgesToCommunity.TryGetValue(neighborCommunity, out var w))
                    {
                        edgesToCommunity[neighborCommunity] = w + weight;
                    }
                    else
                    {
                        edgesToCommunity[neighborCommunity] = weight;
                    }
                }

                double edgesToCurrent = edgesToCommunity.TryGetValue(currentCommunity, out var ec) ? ec : 0.0;
                double currentTotal = communityTotalDegree[currentCommunity];

                int bestCommunity = currentCommunity;
                double bestGain = 0.0;

                foreach (var (targetCommunity, edgesToTarget) in edgesToCommunity)
                {
                    if (targetCommunity == currentCommunity) continue;

                    double targetTotal = communityTotalDegree[targetCommunity];
                    double deltaQ = _options.Resolution * (
                        (edgesToTarget - edgesToCurrent) / m2 +
                        (currentTotal - targetTotal - nDegree) * nDegree / m2Sq
                    );

                    if (deltaQ > bestGain)
                    {
                        bestGain = deltaQ;
                        bestCommunity = targetCommunity;
                    }
                }

                if (bestCommunity != currentCommunity)
                {
                    communityTotalDegree[currentCommunity] -= nDegree;
                    communityTotalDegree[bestCommunity] += nDegree;
                    nodeToCommunity[nodeId] = bestCommunity;
                    improved = true;
                }
            }
        }

        // Group nodes by community
        var rawCommunities = new Dictionary<int, List<string>>();
        foreach (var (nodeId, communityId) in nodeToCommunity)
        {
            if (!rawCommunities.ContainsKey(communityId))
            {
                rawCommunities[communityId] = new List<string>();
            }
            rawCommunities[communityId].Add(nodeId);
        }

        // Split oversized communities
        var finalCommunities = new List<List<string>>();
        int maxSize = Math.Max(
            _options.MinSplitSize,
            (int)(graph.NodeCount * _options.MaxCommunityFraction)
        );

        foreach (var communityNodes in rawCommunities.Values)
        {
            if (communityNodes.Count > maxSize)
            {
                finalCommunities.AddRange(SplitCommunity(graph, communityNodes, maxSize));
            }
            else
            {
                finalCommunities.Add(communityNodes);
            }
        }

        // Sort by size descending and re-index
        finalCommunities.Sort((a, b) => b.Count.CompareTo(a.Count));
        
        var result = new Dictionary<int, IReadOnlyList<string>>();
        for (int i = 0; i < finalCommunities.Count; i++)
        {
            result[i] = finalCommunities[i].OrderBy(id => id).ToList();
        }

        return result;
    }

    /// <summary>
    /// Split oversized community by running Louvain on the subgraph induced by <paramref name="nodeIds"/>.
    /// </summary>
    private List<List<string>> SplitCommunity(KnowledgeGraph graph, List<string> nodeIds, int maxSize)
    {
        if (nodeIds.Count <= maxSize)
            return new List<List<string>> { nodeIds };

        var nodeSet = new HashSet<string>(nodeIds);

        // Build sub-adjacency: only edges that stay inside nodeSet.
        var subAdjacency = new Dictionary<string, Dictionary<string, double>>(nodeIds.Count);
        var subNodeDegree = new Dictionary<string, double>(nodeIds.Count);
        double subTotalWeightDoubled = 0.0;

        foreach (var nodeId in nodeIds)
        {
            var neighborWeights = new Dictionary<string, double>();
            double degree = 0.0;
            foreach (var edge in graph.GetEdges(nodeId))
            {
                var neighborId = edge.Source.Id == nodeId ? edge.Target.Id : edge.Source.Id;
                if (!nodeSet.Contains(neighborId)) continue;
                if (neighborWeights.TryGetValue(neighborId, out var existing))
                {
                    neighborWeights[neighborId] = existing + edge.Weight;
                }
                else
                {
                    neighborWeights[neighborId] = edge.Weight;
                }
                degree += edge.Weight;
            }
            subAdjacency[nodeId] = neighborWeights;
            subNodeDegree[nodeId] = degree;
            subTotalWeightDoubled += degree;
        }

        double subTotalWeight = subTotalWeightDoubled / 2.0;
        if (subTotalWeight == 0.0)
        {
            return nodeIds.Select(id => new List<string> { id }).ToList();
        }

        var subNodeToCommunity = new Dictionary<string, int>(nodeIds.Count);
        var subCommunityTotalDegree = new Dictionary<int, double>(nodeIds.Count);
        for (int i = 0; i < nodeIds.Count; i++)
        {
            var id = nodeIds[i];
            subNodeToCommunity[id] = i;
            subCommunityTotalDegree[i] = subNodeDegree[id];
        }

        double m2 = 2.0 * subTotalWeight;
        double m2Sq = m2 * m2;

        bool improved = true;
        int iteration = 0;
        int maxIter = Math.Min(50, _options.MaxIterations);
        var edgesToCommunity = new Dictionary<int, double>();

        while (improved && iteration < maxIter)
        {
            improved = false;
            iteration++;

            foreach (var nodeId in nodeIds)
            {
                var currentCommunity = subNodeToCommunity[nodeId];
                var nDegree = subNodeDegree[nodeId];
                var neighbors = subAdjacency[nodeId];

                edgesToCommunity.Clear();
                foreach (var (neighborId, weight) in neighbors)
                {
                    if (neighborId == nodeId) continue;
                    var neighborCommunity = subNodeToCommunity[neighborId];
                    if (edgesToCommunity.TryGetValue(neighborCommunity, out var w))
                    {
                        edgesToCommunity[neighborCommunity] = w + weight;
                    }
                    else
                    {
                        edgesToCommunity[neighborCommunity] = weight;
                    }
                }

                double edgesToCurrent = edgesToCommunity.TryGetValue(currentCommunity, out var ec) ? ec : 0.0;
                double currentTotal = subCommunityTotalDegree[currentCommunity];

                int bestCommunity = currentCommunity;
                double bestGain = 0.0;

                foreach (var (targetCommunity, edgesToTarget) in edgesToCommunity)
                {
                    if (targetCommunity == currentCommunity) continue;

                    double targetTotal = subCommunityTotalDegree[targetCommunity];
                    double deltaQ = _options.Resolution * (
                        (edgesToTarget - edgesToCurrent) / m2 +
                        (currentTotal - targetTotal - nDegree) * nDegree / m2Sq
                    );

                    if (deltaQ > bestGain)
                    {
                        bestGain = deltaQ;
                        bestCommunity = targetCommunity;
                    }
                }

                if (bestCommunity != currentCommunity)
                {
                    subCommunityTotalDegree[currentCommunity] -= nDegree;
                    subCommunityTotalDegree[bestCommunity] += nDegree;
                    subNodeToCommunity[nodeId] = bestCommunity;
                    improved = true;
                }
            }
        }

        var subCommunities = new Dictionary<int, List<string>>();
        foreach (var (nodeId, communityId) in subNodeToCommunity)
        {
            if (!subCommunities.ContainsKey(communityId))
            {
                subCommunities[communityId] = new List<string>();
            }
            subCommunities[communityId].Add(nodeId);
        }

        return subCommunities.Values.ToList();
    }

    /// <summary>
    /// Calculate modularity of the entire graph with current community assignments.
    /// </summary>
    public static double CalculateModularity(KnowledgeGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        if (graph.EdgeCount == 0)
            return 0.0;

        double totalEdgeWeight = graph.GetEdges().Sum(e => e.Weight);
        if (totalEdgeWeight == 0)
            return 0.0;

        double m2 = 2.0 * totalEdgeWeight;
        double modularity = 0.0;

        // Group nodes by community
        var communities = new Dictionary<int, List<GraphNode>>();
        foreach (var node in graph.GetNodes())
        {
            if (node.Community.HasValue)
            {
                if (!communities.ContainsKey(node.Community.Value))
                {
                    communities[node.Community.Value] = new List<GraphNode>();
                }
                communities[node.Community.Value].Add(node);
            }
        }

        foreach (var nodes in communities.Values)
        {
            double edgesInside = 0.0;
            double degreesSum = 0.0;

            var nodeSet = new HashSet<string>(nodes.Select(n => n.Id));

            foreach (var node in nodes)
            {
                double nodeDegree = graph.GetEdges(node.Id).Sum(e => e.Weight);
                degreesSum += nodeDegree;

                foreach (var edge in graph.GetEdges(node.Id))
                {
                    var otherId = edge.Source.Id == node.Id ? edge.Target.Id : edge.Source.Id;
                    if (nodeSet.Contains(otherId))
                    {
                        edgesInside += edge.Weight;
                    }
                }
            }

            edgesInside /= 2.0; // Each edge counted twice
            modularity += (edgesInside / totalEdgeWeight) - Math.Pow(degreesSum / m2, 2);
        }

        return modularity;
    }

    /// <summary>
    /// Calculate cohesion (intra-community edge density) for a specific community.
    /// Returns ratio of actual edges to maximum possible edges (0.0 to 1.0).
    /// </summary>
    public static double CalculateCohesion(KnowledgeGraph graph, int communityId)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var nodes = graph.GetNodesByCommunity(communityId).ToList();
        int n = nodes.Count;

        if (n <= 1)
            return 1.0;

        var nodeSet = new HashSet<string>(nodes.Select(node => node.Id));
        int actualEdges = 0;

        foreach (var node in nodes)
        {
            foreach (var edge in graph.GetEdges(node.Id))
            {
                var otherId = edge.Source.Id == node.Id ? edge.Target.Id : edge.Source.Id;
                if (nodeSet.Contains(otherId) && string.Compare(edge.Source.Id, edge.Target.Id, StringComparison.Ordinal) < 0)
                {
                    actualEdges++;
                }
            }
        }

        double possibleEdges = n * (n - 1) / 2.0;
        return possibleEdges > 0 ? Math.Round(actualEdges / possibleEdges, 2) : 0.0;
    }
}
