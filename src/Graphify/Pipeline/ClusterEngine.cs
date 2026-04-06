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
        var nodeToCommunity = new Dictionary<string, int>();
        var nodes = graph.GetNodes().ToList();
        for (int i = 0; i < nodes.Count; i++)
        {
            nodeToCommunity[nodes[i].Id] = i;
        }

        double totalEdgeWeight = graph.GetEdges().Sum(e => e.Weight);
        bool improved = true;
        int iteration = 0;

        while (improved && iteration < _options.MaxIterations)
        {
            improved = false;
            iteration++;

            // For each node, try moving to neighbor communities
            foreach (var node in nodes)
            {
                var currentCommunity = nodeToCommunity[node.Id];
                var bestCommunity = currentCommunity;
                double bestGain = 0.0;

                // Get neighboring communities
                var neighborCommunities = new HashSet<int>();
                foreach (var edge in graph.GetEdges(node.Id))
                {
                    var neighborId = edge.Source.Id == node.Id ? edge.Target.Id : edge.Source.Id;
                    if (nodeToCommunity.TryGetValue(neighborId, out var neighborCommunity))
                    {
                        neighborCommunities.Add(neighborCommunity);
                    }
                }

                // Try moving to each neighbor community
                foreach (var targetCommunity in neighborCommunities)
                {
                    if (targetCommunity == currentCommunity)
                        continue;

                    double gain = CalculateModularityGain(
                        graph,
                        node.Id,
                        currentCommunity,
                        targetCommunity,
                        nodeToCommunity,
                        totalEdgeWeight
                    );

                    if (gain > bestGain)
                    {
                        bestGain = gain;
                        bestCommunity = targetCommunity;
                    }
                }

                if (bestCommunity != currentCommunity)
                {
                    nodeToCommunity[node.Id] = bestCommunity;
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
    /// Calculate modularity gain from moving a node to a new community.
    /// </summary>
    private double CalculateModularityGain(
        KnowledgeGraph graph,
        string nodeId,
        int currentCommunity,
        int targetCommunity,
        Dictionary<string, int> nodeToCommunity,
        double totalEdgeWeight)
    {
        if (totalEdgeWeight == 0)
            return 0.0;

        var node = graph.GetNode(nodeId);
        if (node == null)
            return 0.0;

        // Calculate node degree and edges to target community
        double nodeDegree = 0.0;
        double edgesToTarget = 0.0;
        double edgesToCurrent = 0.0;

        foreach (var edge in graph.GetEdges(nodeId))
        {
            double weight = edge.Weight;
            nodeDegree += weight;

            var neighborId = edge.Source.Id == nodeId ? edge.Target.Id : edge.Source.Id;
            if (nodeToCommunity.TryGetValue(neighborId, out var neighborCommunity))
            {
                if (neighborCommunity == targetCommunity)
                    edgesToTarget += weight;
                if (neighborCommunity == currentCommunity && neighborId != nodeId)
                    edgesToCurrent += weight;
            }
        }

        // Calculate community totals
        double targetCommunityTotal = 0.0;
        double currentCommunityTotal = 0.0;

        foreach (var otherNode in graph.GetNodes())
        {
            if (!nodeToCommunity.TryGetValue(otherNode.Id, out var otherCommunity))
                continue;

            double otherDegree = graph.GetEdges(otherNode.Id).Sum(e => e.Weight);

            if (otherCommunity == targetCommunity)
                targetCommunityTotal += otherDegree;
            if (otherCommunity == currentCommunity)
                currentCommunityTotal += otherDegree;
        }

        double m2 = 2.0 * totalEdgeWeight;

        // Modularity gain formula
        double deltaQ = _options.Resolution * (
            (edgesToTarget - edgesToCurrent) / m2 +
            (currentCommunityTotal - targetCommunityTotal - nodeDegree) * nodeDegree / (m2 * m2)
        );

        return deltaQ;
    }

    /// <summary>
    /// Split oversized community by running Louvain on the subgraph.
    /// </summary>
    private List<List<string>> SplitCommunity(KnowledgeGraph graph, List<string> nodeIds, int maxSize)
    {
        if (nodeIds.Count <= maxSize)
            return new List<List<string>> { nodeIds };

        // Build subgraph edges
        var subgraphEdges = new List<GraphEdge>();
        var nodeSet = new HashSet<string>(nodeIds);

        foreach (var nodeId in nodeIds)
        {
            foreach (var edge in graph.GetEdges(nodeId))
            {
                var otherId = edge.Source.Id == nodeId ? edge.Target.Id : edge.Source.Id;
                if (nodeSet.Contains(otherId) && string.Compare(edge.Source.Id, edge.Target.Id, StringComparison.Ordinal) < 0)
                {
                    subgraphEdges.Add(edge);
                }
            }
        }

        if (subgraphEdges.Count == 0)
        {
            // No internal edges - split into individual nodes
            return nodeIds.Select(id => new List<string> { id }).ToList();
        }

        // Run simplified Louvain on subgraph
        var subNodeToCommunity = new Dictionary<string, int>();
        for (int i = 0; i < nodeIds.Count; i++)
        {
            subNodeToCommunity[nodeIds[i]] = i;
        }

        double subTotalWeight = subgraphEdges.Sum(e => e.Weight);
        bool improved = true;
        int iteration = 0;

        while (improved && iteration < Math.Min(50, _options.MaxIterations))
        {
            improved = false;
            iteration++;

            foreach (var nodeId in nodeIds)
            {
                var currentCommunity = subNodeToCommunity[nodeId];
                var bestCommunity = currentCommunity;
                double bestGain = 0.0;

                var neighborCommunities = new HashSet<int>();
                foreach (var edge in graph.GetEdges(nodeId))
                {
                    var neighborId = edge.Source.Id == nodeId ? edge.Target.Id : edge.Source.Id;
                    if (subNodeToCommunity.TryGetValue(neighborId, out var neighborCommunity))
                    {
                        neighborCommunities.Add(neighborCommunity);
                    }
                }

                foreach (var targetCommunity in neighborCommunities)
                {
                    if (targetCommunity == currentCommunity)
                        continue;

                    double gain = CalculateSubgraphModularityGain(
                        graph,
                        nodeId,
                        currentCommunity,
                        targetCommunity,
                        subNodeToCommunity,
                        nodeSet,
                        subTotalWeight
                    );

                    if (gain > bestGain)
                    {
                        bestGain = gain;
                        bestCommunity = targetCommunity;
                    }
                }

                if (bestCommunity != currentCommunity)
                {
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
    /// Calculate modularity gain within a subgraph during splitting.
    /// </summary>
    private double CalculateSubgraphModularityGain(
        KnowledgeGraph graph,
        string nodeId,
        int currentCommunity,
        int targetCommunity,
        Dictionary<string, int> subNodeToCommunity,
        HashSet<string> nodeSet,
        double totalEdgeWeight)
    {
        if (totalEdgeWeight == 0)
            return 0.0;

        double nodeDegree = 0.0;
        double edgesToTarget = 0.0;
        double edgesToCurrent = 0.0;

        foreach (var edge in graph.GetEdges(nodeId))
        {
            var neighborId = edge.Source.Id == nodeId ? edge.Target.Id : edge.Source.Id;
            if (!nodeSet.Contains(neighborId))
                continue;

            double weight = edge.Weight;
            nodeDegree += weight;

            if (subNodeToCommunity.TryGetValue(neighborId, out var neighborCommunity))
            {
                if (neighborCommunity == targetCommunity)
                    edgesToTarget += weight;
                if (neighborCommunity == currentCommunity && neighborId != nodeId)
                    edgesToCurrent += weight;
            }
        }

        double targetCommunityTotal = 0.0;
        double currentCommunityTotal = 0.0;

        foreach (var otherId in nodeSet)
        {
            if (!subNodeToCommunity.TryGetValue(otherId, out var otherCommunity))
                continue;

            double otherDegree = 0.0;
            foreach (var edge in graph.GetEdges(otherId))
            {
                var neighborId = edge.Source.Id == otherId ? edge.Target.Id : edge.Source.Id;
                if (nodeSet.Contains(neighborId))
                {
                    otherDegree += edge.Weight;
                }
            }

            if (otherCommunity == targetCommunity)
                targetCommunityTotal += otherDegree;
            if (otherCommunity == currentCommunity)
                currentCommunityTotal += otherDegree;
        }

        double m2 = 2.0 * totalEdgeWeight;

        double deltaQ = _options.Resolution * (
            (edgesToTarget - edgesToCurrent) / m2 +
            (currentCommunityTotal - targetCommunityTotal - nodeDegree) * nodeDegree / (m2 * m2)
        );

        return deltaQ;
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
