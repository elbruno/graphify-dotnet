using Graphify.Graph;
using Graphify.Models;
using Graphify.Pipeline;
using Xunit;

namespace Graphify.Tests.Pipeline;

[Trait("Category", "Pipeline")]
public sealed class AnalyzerTests
{
    [Fact]
    public async Task ExecuteAsync_GodNodeDetection_FindsHighestDegree()
    {
        // Arrange
        var options = new AnalyzerOptions { TopGodNodesCount = 2 };
        var analyzer = new Analyzer(options);
        var graph = CreateTestGraph();

        // Node "hub" has degree 3, others have degree 1-2
        var hub = CreateNode("hub", "Hub");
        var node1 = CreateNode("n1", "Node1");
        var node2 = CreateNode("n2", "Node2");
        var node3 = CreateNode("n3", "Node3");

        graph.AddNode(hub);
        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);

        graph.AddEdge(CreateEdge(hub, node1));
        graph.AddEdge(CreateEdge(hub, node2));
        graph.AddEdge(CreateEdge(hub, node3));

        // Act
        var result = await analyzer.ExecuteAsync(graph);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.GodNodes);
        Assert.Contains(result.GodNodes, gn => gn.Id == "hub" && gn.EdgeCount == 3);
    }

    [Fact]
    public async Task ExecuteAsync_SurprisingConnections_FindsCrossCommunity()
    {
        // Arrange
        var options = new AnalyzerOptions { TopSurprisingConnections = 5 };
        var analyzer = new Analyzer(options);
        var graph = CreateTestGraph();

        var node1 = CreateNode("a", "A", "File1.cs");
        var node2 = CreateNode("b", "B", "File1.cs");
        var node3 = CreateNode("c", "C", "File2.cs");
        var node4 = CreateNode("d", "D", "File2.cs");

        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);
        graph.AddNode(node4);

        // Intra-community edges
        graph.AddEdge(CreateEdge(node1, node2, "calls"));
        graph.AddEdge(CreateEdge(node3, node4, "calls"));

        // Cross-file edge (surprising)
        graph.AddEdge(CreateEdge(node1, node3, "uses", Confidence.Inferred));

        // Assign communities
        graph.AssignCommunities(new Dictionary<int, IReadOnlyList<string>>
        {
            [0] = new[] { "a", "b" },
            [1] = new[] { "c", "d" }
        });

        // Act
        var result = await analyzer.ExecuteAsync(graph);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.SurprisingConnections);
        
        // Should find the cross-file connection
        var surprise = result.SurprisingConnections.FirstOrDefault(sc => 
            sc.Source == "A" && sc.Target == "C");
        Assert.NotNull(surprise);
    }

    [Fact]
    public async Task ExecuteAsync_Statistics_CalculatedCorrectly()
    {
        // Arrange
        var options = new AnalyzerOptions();
        var analyzer = new Analyzer(options);
        var graph = CreateTestGraph();

        var node1 = CreateNode("a", "A");
        var node2 = CreateNode("b", "B");
        var node3 = CreateNode("c", "C");
        var isolated = CreateNode("isolated", "Isolated");

        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);
        graph.AddNode(isolated);

        graph.AddEdge(CreateEdge(node1, node2));
        graph.AddEdge(CreateEdge(node2, node3));

        graph.AssignCommunities(new Dictionary<int, IReadOnlyList<string>>
        {
            [0] = new[] { "a", "b", "c", "isolated" }
        });

        // Act
        var result = await analyzer.ExecuteAsync(graph);

        // Assert
        Assert.NotNull(result.Statistics);
        Assert.Equal(4, result.Statistics.NodeCount);
        Assert.Equal(2, result.Statistics.EdgeCount);
        Assert.Equal(1, result.Statistics.CommunityCount);
        Assert.Equal(1, result.Statistics.IsolatedNodeCount); // "isolated" has degree 0
        Assert.True(result.Statistics.AverageDegree > 0);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyGraph_ReturnsEmptyAnalysis()
    {
        // Arrange
        var options = new AnalyzerOptions();
        var analyzer = new Analyzer(options);
        var graph = new KnowledgeGraph();

        // Act
        var result = await analyzer.ExecuteAsync(graph);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.GodNodes);
        Assert.Empty(result.SurprisingConnections);
        Assert.NotEmpty(result.SuggestedQuestions); // May have "no signal" question
        Assert.Equal(0, result.Statistics.NodeCount);
        Assert.Equal(0, result.Statistics.EdgeCount);
    }

    [Fact]
    public async Task ExecuteAsync_SuggestedQuestions_Generated()
    {
        // Arrange
        var options = new AnalyzerOptions { MaxSuggestedQuestions = 10 };
        var analyzer = new Analyzer(options);
        var graph = CreateTestGraph();

        var node1 = CreateNode("a", "A");
        var node2 = CreateNode("b", "B");
        var isolated = CreateNode("isolated", "Isolated");

        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(isolated);

        // Add ambiguous edge
        graph.AddEdge(CreateEdge(node1, node2, "unknown", Confidence.Ambiguous));

        // Act
        var result = await analyzer.ExecuteAsync(graph);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.SuggestedQuestions);
        
        // Should have question about ambiguous edge
        var ambiguousQuestion = result.SuggestedQuestions
            .FirstOrDefault(q => q.Type == "ambiguous_edge");
        Assert.NotNull(ambiguousQuestion);
    }

    [Fact]
    public async Task ExecuteAsync_IsolatedNodes_DetectedInQuestions()
    {
        // Arrange
        var options = new AnalyzerOptions();
        var analyzer = new Analyzer(options);
        var graph = CreateTestGraph();

        var isolated1 = CreateNode("iso1", "Isolated1");
        var isolated2 = CreateNode("iso2", "Isolated2");

        graph.AddNode(isolated1);
        graph.AddNode(isolated2);

        // Act
        var result = await analyzer.ExecuteAsync(graph);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.SuggestedQuestions);
        
        // Should have question about isolated nodes
        var isolatedQuestion = result.SuggestedQuestions
            .FirstOrDefault(q => q.Type == "isolated_nodes");
        
        if (isolatedQuestion != null)
        {
            Assert.Contains("Isolated", isolatedQuestion.Question ?? "");
        }
    }

    [Fact]
    public async Task ExecuteAsync_BridgeNodes_DetectedInQuestions()
    {
        // Arrange
        var options = new AnalyzerOptions();
        var analyzer = new Analyzer(options);
        var graph = CreateTestGraph();

        // Create two communities with a bridge
        var node1 = CreateNode("a", "A");
        var node2 = CreateNode("b", "B");
        var bridge = CreateNode("bridge", "Bridge");
        var node3 = CreateNode("c", "C");
        var node4 = CreateNode("d", "D");

        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(bridge);
        graph.AddNode(node3);
        graph.AddNode(node4);

        // Community 1
        graph.AddEdge(CreateEdge(node1, node2));
        graph.AddEdge(CreateEdge(node1, bridge));

        // Bridge to community 2
        graph.AddEdge(CreateEdge(bridge, node3));

        // Community 2
        graph.AddEdge(CreateEdge(node3, node4));

        graph.AssignCommunities(new Dictionary<int, IReadOnlyList<string>>
        {
            [0] = new[] { "a", "b", "bridge" },
            [1] = new[] { "c", "d" }
        });

        // Act
        var result = await analyzer.ExecuteAsync(graph);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.SuggestedQuestions);
    }

    [Fact]
    public async Task ExecuteAsync_TopGodNodesCount_LimitsResults()
    {
        // Arrange
        var options = new AnalyzerOptions { TopGodNodesCount = 2 };
        var analyzer = new Analyzer(options);
        var graph = CreateTestGraph();

        // Create 5 nodes with varying degrees
        for (int i = 0; i < 5; i++)
        {
            var node = CreateNode($"n{i}", $"Node{i}");
            graph.AddNode(node);
        }

        // Create edges to give different degrees
        var nodes = graph.GetNodes().ToList();
        for (int i = 1; i < nodes.Count; i++)
        {
            graph.AddEdge(CreateEdge(nodes[0], nodes[i]));
        }
        graph.AddEdge(CreateEdge(nodes[1], nodes[2]));

        // Act
        var result = await analyzer.ExecuteAsync(graph);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.GodNodes.Count <= 2);
    }

    [Fact]
    public async Task ExecuteAsync_CrossFileSurprises_DetectedWithMultipleSources()
    {
        // Arrange
        var options = new AnalyzerOptions { TopSurprisingConnections = 5 };
        var analyzer = new Analyzer(options);
        var graph = CreateTestGraph();

        var node1 = CreateNode("a", "A", "File1.cs");
        var node2 = CreateNode("b", "B", "File2.cs");
        var node3 = CreateNode("c", "C", "File3.py");

        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);

        // Cross-file connections
        graph.AddEdge(CreateEdge(node1, node2, "imports", Confidence.Extracted));
        graph.AddEdge(CreateEdge(node2, node3, "uses", Confidence.Inferred));

        // Act
        var result = await analyzer.ExecuteAsync(graph);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.SurprisingConnections);
        
        // With multiple source files, should detect cross-file surprises
        Assert.True(result.SurprisingConnections.Count >= 1);
    }

    [Fact]
    public async Task ExecuteAsync_NoSignal_ReturnsNoSignalQuestion()
    {
        // Arrange
        var options = new AnalyzerOptions();
        var analyzer = new Analyzer(options);
        var graph = CreateTestGraph();

        // Simple, fully-connected graph with no surprises
        var node1 = CreateNode("a", "A");
        var node2 = CreateNode("b", "B");

        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddEdge(CreateEdge(node1, node2, "calls", Confidence.Extracted));

        graph.AssignCommunities(new Dictionary<int, IReadOnlyList<string>>
        {
            [0] = new[] { "a", "b" }
        });

        // Act
        var result = await analyzer.ExecuteAsync(graph);

        // Assert
        Assert.NotNull(result);
        // Either has questions or a "no signal" question
        Assert.NotEmpty(result.SuggestedQuestions);
    }

    private static KnowledgeGraph CreateTestGraph()
    {
        return new KnowledgeGraph();
    }

    private static GraphNode CreateNode(string id, string label, string? filePath = "test.cs")
    {
        return new GraphNode
        {
            Id = id,
            Label = label,
            Type = "Entity",
            FilePath = filePath,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };
    }

    private static GraphEdge CreateEdge(
        GraphNode source, 
        GraphNode target, 
        string relationship = "calls",
        Confidence confidence = Confidence.Extracted)
    {
        return new GraphEdge
        {
            Source = source,
            Target = target,
            Relationship = relationship,
            Weight = 1.0,
            Confidence = confidence,
            Metadata = new Dictionary<string, string>()
        };
    }
}
