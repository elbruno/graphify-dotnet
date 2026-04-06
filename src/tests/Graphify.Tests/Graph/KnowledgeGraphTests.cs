using Graphify.Graph;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Graph;

public class KnowledgeGraphTests
{
    [Fact]
    public void Constructor_InitializesEmptyGraph()
    {
        // Act
        var graph = new KnowledgeGraph();

        // Assert
        Assert.Equal(0, graph.NodeCount);
        Assert.Equal(0, graph.EdgeCount);
    }

    [Fact]
    public void AddNode_IncreasesNodeCount()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };

        // Act
        graph.AddNode(node);

        // Assert
        Assert.Equal(1, graph.NodeCount);
    }

    [Fact]
    public void AddNode_NullNode_ThrowsArgumentNullException()
    {
        // Arrange
        var graph = new KnowledgeGraph();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => graph.AddNode(null!));
    }

    [Fact]
    public void AddNode_DuplicateId_ReplacesNode()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node1 = new GraphNode { Id = "node1", Label = "Original", Type = "class" };
        var node2 = new GraphNode { Id = "node1", Label = "Updated", Type = "class" };

        // Act
        graph.AddNode(node1);
        graph.AddNode(node2);

        // Assert
        Assert.Equal(1, graph.NodeCount);
        var retrieved = graph.GetNode("node1");
        Assert.NotNull(retrieved);
        Assert.Equal("Updated", retrieved.Label);
    }

    [Fact]
    public void GetNode_ExistingNode_ReturnsNode()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };
        graph.AddNode(node);

        // Act
        var retrieved = graph.GetNode("node1");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("node1", retrieved.Id);
        Assert.Equal("Node 1", retrieved.Label);
    }

    [Fact]
    public void GetNode_NonExistentNode_ReturnsNull()
    {
        // Arrange
        var graph = new KnowledgeGraph();

        // Act
        var retrieved = graph.GetNode("nonexistent");

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public void AddEdge_ValidEdge_IncreasesEdgeCount()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var source = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };
        var target = new GraphNode { Id = "node2", Label = "Node 2", Type = "class" };
        graph.AddNode(source);
        graph.AddNode(target);

        var edge = new GraphEdge { Source = source, Target = target, Relationship = "uses" };

        // Act
        var result = graph.AddEdge(edge);

        // Assert
        Assert.True(result);
        Assert.Equal(1, graph.EdgeCount);
    }

    [Fact]
    public void AddEdge_MissingSourceNode_ReturnsFalse()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var source = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };
        var target = new GraphNode { Id = "node2", Label = "Node 2", Type = "class" };
        graph.AddNode(target); // Only add target

        var edge = new GraphEdge { Source = source, Target = target, Relationship = "uses" };

        // Act
        var result = graph.AddEdge(edge);

        // Assert
        Assert.False(result);
        Assert.Equal(0, graph.EdgeCount);
    }

    [Fact]
    public void AddEdge_MissingTargetNode_ReturnsFalse()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var source = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };
        var target = new GraphNode { Id = "node2", Label = "Node 2", Type = "class" };
        graph.AddNode(source); // Only add source

        var edge = new GraphEdge { Source = source, Target = target, Relationship = "uses" };

        // Act
        var result = graph.AddEdge(edge);

        // Assert
        Assert.False(result);
        Assert.Equal(0, graph.EdgeCount);
    }

    [Fact]
    public void AddEdge_NullEdge_ThrowsArgumentNullException()
    {
        // Arrange
        var graph = new KnowledgeGraph();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => graph.AddEdge(null!));
    }

    [Fact]
    public void GetEdges_ReturnsAllEdges()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node1 = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };
        var node2 = new GraphNode { Id = "node2", Label = "Node 2", Type = "class" };
        graph.AddNode(node1);
        graph.AddNode(node2);

        var edge1 = new GraphEdge { Source = node1, Target = node2, Relationship = "uses" };
        var edge2 = new GraphEdge { Source = node2, Target = node1, Relationship = "implements" };
        graph.AddEdge(edge1);
        graph.AddEdge(edge2);

        // Act
        var edges = graph.GetEdges().ToList();

        // Assert
        Assert.Equal(2, edges.Count);
    }

    [Fact]
    public void GetEdges_WithNodeId_ReturnsConnectedEdges()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node1 = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };
        var node2 = new GraphNode { Id = "node2", Label = "Node 2", Type = "class" };
        var node3 = new GraphNode { Id = "node3", Label = "Node 3", Type = "class" };
        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);

        graph.AddEdge(new GraphEdge { Source = node1, Target = node2, Relationship = "uses" });
        graph.AddEdge(new GraphEdge { Source = node2, Target = node3, Relationship = "calls" });
        graph.AddEdge(new GraphEdge { Source = node3, Target = node1, Relationship = "imports" });

        // Act
        var edges = graph.GetEdges("node1").ToList();

        // Assert
        Assert.Equal(2, edges.Count); // One incoming, one outgoing
    }

    [Fact]
    public void GetNeighbors_ReturnsOutgoingNodes()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node1 = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };
        var node2 = new GraphNode { Id = "node2", Label = "Node 2", Type = "class" };
        var node3 = new GraphNode { Id = "node3", Label = "Node 3", Type = "class" };
        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);

        graph.AddEdge(new GraphEdge { Source = node1, Target = node2, Relationship = "uses" });
        graph.AddEdge(new GraphEdge { Source = node1, Target = node3, Relationship = "calls" });

        // Act
        var neighbors = graph.GetNeighbors("node1").ToList();

        // Assert
        Assert.Equal(2, neighbors.Count);
        Assert.Contains(neighbors, n => n.Id == "node2");
        Assert.Contains(neighbors, n => n.Id == "node3");
    }

    [Fact]
    public void GetNeighbors_NonExistentNode_ReturnsEmpty()
    {
        // Arrange
        var graph = new KnowledgeGraph();

        // Act
        var neighbors = graph.GetNeighbors("nonexistent").ToList();

        // Assert
        Assert.Empty(neighbors);
    }

    [Fact]
    public void GetDegree_ReturnsCorrectDegree()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node1 = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };
        var node2 = new GraphNode { Id = "node2", Label = "Node 2", Type = "class" };
        var node3 = new GraphNode { Id = "node3", Label = "Node 3", Type = "class" };
        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);

        graph.AddEdge(new GraphEdge { Source = node1, Target = node2, Relationship = "uses" });
        graph.AddEdge(new GraphEdge { Source = node3, Target = node1, Relationship = "imports" });

        // Act
        var degree = graph.GetDegree("node1");

        // Assert
        Assert.Equal(2, degree); // 1 incoming + 1 outgoing
    }

    [Fact]
    public void GetDegree_NonExistentNode_ReturnsZero()
    {
        // Arrange
        var graph = new KnowledgeGraph();

        // Act
        var degree = graph.GetDegree("nonexistent");

        // Assert
        Assert.Equal(0, degree);
    }

    [Fact]
    public void GetHighestDegreeNodes_ReturnsTopNodes()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node1 = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };
        var node2 = new GraphNode { Id = "node2", Label = "Node 2", Type = "class" };
        var node3 = new GraphNode { Id = "node3", Label = "Node 3", Type = "class" };
        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);

        // node2 has highest degree
        graph.AddEdge(new GraphEdge { Source = node1, Target = node2, Relationship = "uses" });
        graph.AddEdge(new GraphEdge { Source = node2, Target = node3, Relationship = "calls" });
        graph.AddEdge(new GraphEdge { Source = node3, Target = node2, Relationship = "imports" });

        // Act
        var topNodes = graph.GetHighestDegreeNodes(2).ToList();

        // Assert
        Assert.Equal(2, topNodes.Count);
        Assert.Equal("node2", topNodes[0].Node.Id);
        Assert.Equal(3, topNodes[0].Degree);
    }

    [Fact]
    public void GetNodes_ReturnsAllNodes()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        graph.AddNode(new GraphNode { Id = "node1", Label = "Node 1", Type = "class" });
        graph.AddNode(new GraphNode { Id = "node2", Label = "Node 2", Type = "class" });
        graph.AddNode(new GraphNode { Id = "node3", Label = "Node 3", Type = "class" });

        // Act
        var nodes = graph.GetNodes().ToList();

        // Assert
        Assert.Equal(3, nodes.Count);
    }

    [Fact]
    public void AssignCommunities_UpdatesNodeCommunities()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node1 = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };
        var node2 = new GraphNode { Id = "node2", Label = "Node 2", Type = "class" };
        graph.AddNode(node1);
        graph.AddNode(node2);

        var communities = new Dictionary<int, IReadOnlyList<string>>
        {
            { 0, new List<string> { "node1" } },
            { 1, new List<string> { "node2" } }
        };

        // Act
        graph.AssignCommunities(communities);

        // Assert
        var retrieved1 = graph.GetNode("node1");
        var retrieved2 = graph.GetNode("node2");
        Assert.Equal(0, retrieved1!.Community);
        Assert.Equal(1, retrieved2!.Community);
    }

    [Fact]
    public void GetNodesByCommunity_ReturnsNodesInCommunity()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node1 = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };
        var node2 = new GraphNode { Id = "node2", Label = "Node 2", Type = "class" };
        var node3 = new GraphNode { Id = "node3", Label = "Node 3", Type = "class" };
        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);

        var communities = new Dictionary<int, IReadOnlyList<string>>
        {
            { 0, new List<string> { "node1", "node2" } },
            { 1, new List<string> { "node3" } }
        };
        graph.AssignCommunities(communities);

        // Act
        var community0Nodes = graph.GetNodesByCommunity(0).ToList();

        // Assert
        Assert.Equal(2, community0Nodes.Count);
        Assert.All(community0Nodes, node => Assert.Equal(0, node.Community));
    }

    [Fact]
    public void MergeGraph_AddsNodesAndEdges()
    {
        // Arrange
        var graph1 = new KnowledgeGraph();
        var graph2 = new KnowledgeGraph();

        var node1 = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };
        var node2 = new GraphNode { Id = "node2", Label = "Node 2", Type = "class" };
        graph1.AddNode(node1);
        graph2.AddNode(node2);

        // Act
        graph1.MergeGraph(graph2);

        // Assert
        Assert.Equal(2, graph1.NodeCount);
        Assert.NotNull(graph1.GetNode("node1"));
        Assert.NotNull(graph1.GetNode("node2"));
    }

    [Fact]
    public void MergeGraph_OverwritesDuplicateNodes()
    {
        // Arrange
        var graph1 = new KnowledgeGraph();
        var graph2 = new KnowledgeGraph();

        var node1 = new GraphNode { Id = "node1", Label = "Original", Type = "class" };
        var node2 = new GraphNode { Id = "node1", Label = "Updated", Type = "class" };
        graph1.AddNode(node1);
        graph2.AddNode(node2);

        // Act
        graph1.MergeGraph(graph2);

        // Assert
        Assert.Equal(1, graph1.NodeCount);
        var retrieved = graph1.GetNode("node1");
        Assert.Equal("Updated", retrieved!.Label);
    }

    [Fact]
    public void MergeGraph_NullGraph_ThrowsArgumentNullException()
    {
        // Arrange
        var graph = new KnowledgeGraph();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => graph.MergeGraph(null!));
    }

    [Fact]
    public void UnderlyingGraph_ProvidesAccessToQuikGraph()
    {
        // Arrange
        var graph = new KnowledgeGraph();
        var node = new GraphNode { Id = "node1", Label = "Node 1", Type = "class" };
        graph.AddNode(node);

        // Act
        var underlyingGraph = graph.UnderlyingGraph;

        // Assert
        Assert.NotNull(underlyingGraph);
        Assert.Equal(1, underlyingGraph.VertexCount);
    }
}
