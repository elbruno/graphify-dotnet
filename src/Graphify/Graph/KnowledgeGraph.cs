using QuikGraph;
using Graphify.Models;

namespace Graphify.Graph;

/// <summary>
/// Main API for the knowledge graph. Wraps QuikGraph's BidirectionalGraph
/// to provide domain-specific operations for code analysis.
/// </summary>
public sealed class KnowledgeGraph
{
    private readonly BidirectionalGraph<GraphNode, GraphEdge> _graph;
    private readonly Dictionary<string, GraphNode> _nodeIndex;

    public KnowledgeGraph()
    {
        _graph = new BidirectionalGraph<GraphNode, GraphEdge>(allowParallelEdges: true);
        _nodeIndex = new Dictionary<string, GraphNode>();
    }

    /// <summary>
    /// Total number of nodes in the graph.
    /// </summary>
    public int NodeCount => _graph.VertexCount;

    /// <summary>
    /// Total number of edges in the graph.
    /// </summary>
    public int EdgeCount => _graph.EdgeCount;

    /// <summary>
    /// Add a node to the graph. If a node with the same Id already exists,
    /// the new node replaces it (semantic nodes overwrite AST nodes).
    /// </summary>
    public void AddNode(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (_nodeIndex.TryGetValue(node.Id, out var existing))
        {
            _graph.RemoveVertex(existing);
        }

        _graph.AddVertex(node);
        _nodeIndex[node.Id] = node;
    }

    /// <summary>
    /// Add an edge to the graph. Both source and target nodes must already exist.
    /// </summary>
    public bool AddEdge(GraphEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);

        if (!_nodeIndex.ContainsKey(edge.Source.Id) || !_nodeIndex.ContainsKey(edge.Target.Id))
        {
            return false; // Skip edges to external/stdlib nodes
        }

        return _graph.AddEdge(edge);
    }

    /// <summary>
    /// Get a node by its ID. Returns null if not found.
    /// </summary>
    public GraphNode? GetNode(string id) =>
        _nodeIndex.TryGetValue(id, out var node) ? node : null;

    /// <summary>
    /// Get all outgoing neighbors of a node.
    /// </summary>
    public IEnumerable<GraphNode> GetNeighbors(string id)
    {
        if (_nodeIndex.TryGetValue(id, out var node))
        {
            return _graph.OutEdges(node).Select(e => e.Target);
        }
        return Enumerable.Empty<GraphNode>();
    }

    /// <summary>
    /// Get all edges in the graph.
    /// </summary>
    public IEnumerable<GraphEdge> GetEdges() => _graph.Edges;

    /// <summary>
    /// Get all edges connected to a node (both incoming and outgoing).
    /// </summary>
    public IEnumerable<GraphEdge> GetEdges(string id)
    {
        if (_nodeIndex.TryGetValue(id, out var node))
        {
            return _graph.InEdges(node).Concat(_graph.OutEdges(node)).Distinct();
        }
        return Enumerable.Empty<GraphEdge>();
    }

    /// <summary>
    /// Get the degree (total edge count) for a node.
    /// </summary>
    public int GetDegree(string id)
    {
        if (_nodeIndex.TryGetValue(id, out var node))
        {
            return _graph.InDegree(node) + _graph.OutDegree(node);
        }
        return 0;
    }

    /// <summary>
    /// Get the top N highest-degree nodes.
    /// </summary>
    public IEnumerable<(GraphNode Node, int Degree)> GetHighestDegreeNodes(int topN)
    {
        return _graph.Vertices
            .Select(node => (Node: node, Degree: _graph.InDegree(node) + _graph.OutDegree(node)))
            .OrderByDescending(x => x.Degree)
            .Take(topN);
    }

    /// <summary>
    /// Get all nodes in the graph.
    /// </summary>
    public IEnumerable<GraphNode> GetNodes() => _graph.Vertices;

    /// <summary>
    /// Get all nodes belonging to a specific community.
    /// </summary>
    public IEnumerable<GraphNode> GetNodesByCommunity(int communityId) =>
        _graph.Vertices.Where(n => n.Community == communityId);

    /// <summary>
    /// Assign community IDs to nodes. Updates nodes with their community assignment.
    /// </summary>
    public void AssignCommunities(IReadOnlyDictionary<int, IReadOnlyList<string>> communities)
    {
        ArgumentNullException.ThrowIfNull(communities);

        var updates = new List<(GraphNode Old, GraphNode New)>();

        foreach (var (communityId, nodeIds) in communities)
        {
            foreach (var nodeId in nodeIds)
            {
                if (_nodeIndex.TryGetValue(nodeId, out var node))
                {
                    var updated = node with { Community = communityId };
                    updates.Add((node, updated));
                }
            }
        }

        // Collect all edges first before modifying the graph
        var edgesToUpdate = new List<(GraphEdge OldEdge, GraphNode OldSource, GraphNode OldTarget, GraphNode NewSource, GraphNode? NewTarget)>();

        foreach (var (oldNode, newNode) in updates)
        {
            var outEdges = _graph.OutEdges(oldNode).ToList();
            var inEdges = _graph.InEdges(oldNode).ToList();
            
            foreach (var edge in outEdges)
            {
                var newTarget = updates.FirstOrDefault(u => u.Old.Equals(edge.Target)).New ?? edge.Target;
                edgesToUpdate.Add((edge, oldNode, edge.Target, newNode, newTarget));
            }
            foreach (var edge in inEdges)
            {
                var newSource = updates.FirstOrDefault(u => u.Old.Equals(edge.Source)).New ?? edge.Source;
                edgesToUpdate.Add((edge, edge.Source, oldNode, newSource, newNode));
            }
        }

        // Remove old nodes
        foreach (var (oldNode, newNode) in updates)
        {
            _graph.RemoveVertex(oldNode);
        }

        // Add new nodes
        foreach (var (oldNode, newNode) in updates)
        {
            _graph.AddVertex(newNode);
            _nodeIndex[newNode.Id] = newNode;
        }

        // Re-add edges with updated nodes
        foreach (var (oldEdge, oldSource, oldTarget, newSource, newTarget) in edgesToUpdate.Distinct())
        {
            var newEdge = oldEdge with 
            { 
                Source = oldEdge.Source.Equals(oldSource) ? newSource : oldEdge.Source,
                Target = oldEdge.Target.Equals(oldTarget) ? (newTarget ?? oldEdge.Target) : oldEdge.Target
            };
            _graph.AddEdge(newEdge);
        }
    }

    /// <summary>
    /// Merge another graph into this one. Nodes with the same ID are overwritten.
    /// </summary>
    public void MergeGraph(KnowledgeGraph other)
    {
        ArgumentNullException.ThrowIfNull(other);

        foreach (var node in other.GetNodes())
        {
            AddNode(node);
        }

        foreach (var edge in other.GetEdges())
        {
            AddEdge(edge);
        }
    }

    /// <summary>
    /// Access the underlying QuikGraph for advanced algorithms.
    /// Use sparingly - prefer KnowledgeGraph methods for common operations.
    /// </summary>
    public BidirectionalGraph<GraphNode, GraphEdge> UnderlyingGraph => _graph;
}

