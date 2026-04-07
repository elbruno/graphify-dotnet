using Graphify.Graph;
using Graphify.Models;
using Graphify.Pipeline;
using Xunit;

namespace Graphify.Tests.Pipeline;

/// <summary>
/// Tests for ReportGenerator: markdown report generation including summary, god nodes,
/// surprising connections, communities, suggested questions, and knowledge gaps.
/// </summary>
[Trait("Category", "Pipeline")]
public sealed class ReportGeneratorTests
{
    private readonly ReportGenerator _generator = new();

    [Fact]
    public void Generate_FromAnalyzedGraph_ProducesReport()
    {
        var (graph, analysis) = CreateSampleAnalysis();

        var report = _generator.Generate(
            graph, analysis,
            new Dictionary<int, string> { [0] = "Core" },
            new Dictionary<int, double> { [0] = 0.75 },
            "TestProject");

        Assert.NotEmpty(report);
        Assert.Contains("TestProject", report);
    }

    [Fact]
    public void Generate_IncludesNodeAndEdgeCount()
    {
        var (graph, analysis) = CreateSampleAnalysis();

        var report = _generator.Generate(
            graph, analysis,
            new Dictionary<int, string> { [0] = "Core" },
            new Dictionary<int, double> { [0] = 0.75 },
            "Stats");

        Assert.Contains("3 nodes", report);
        Assert.Contains("2 edges", report);
    }

    [Fact]
    public void Generate_IncludesGodNodes()
    {
        var (graph, analysis) = CreateSampleAnalysis();

        var report = _generator.Generate(
            graph, analysis,
            new Dictionary<int, string> { [0] = "Core" },
            new Dictionary<int, double> { [0] = 0.75 },
            "GodTest");

        Assert.Contains("God Nodes", report);
        Assert.Contains("Hub", report);
    }

    [Fact]
    public void Generate_IncludesCommunityCount()
    {
        var (graph, analysis) = CreateSampleAnalysis();

        var report = _generator.Generate(
            graph, analysis,
            new Dictionary<int, string> { [0] = "Core" },
            new Dictionary<int, double> { [0] = 0.5 },
            "CommTest");

        Assert.Contains("Communities", report);
        Assert.Contains("Core", report);
    }

    [Fact]
    public void Generate_IncludesTopConnectedNodes()
    {
        var (graph, analysis) = CreateSampleAnalysis();

        var report = _generator.Generate(
            graph, analysis,
            new Dictionary<int, string> { [0] = "Core" },
            new Dictionary<int, double> { [0] = 0.5 },
            "Hubs");

        Assert.Contains("Hub", report);
        Assert.Contains("edges", report);
    }

    [Fact]
    public void Generate_EmptyGraph_ProducesValidReport()
    {
        var graph = new KnowledgeGraph();
        var analysis = new AnalysisResult
        {
            GodNodes = Array.Empty<GodNode>(),
            SurprisingConnections = Array.Empty<SurprisingConnection>(),
            SuggestedQuestions = Array.Empty<SuggestedQuestion>(),
            Statistics = new GraphStatistics
            {
                NodeCount = 0, EdgeCount = 0, CommunityCount = 0,
                AverageDegree = 0, IsolatedNodeCount = 0
            }
        };

        var report = _generator.Generate(
            graph, analysis,
            new Dictionary<int, string>(),
            new Dictionary<int, double>(),
            "Empty");

        Assert.Contains("Empty", report);
        Assert.Contains("0 nodes", report);
    }

    [Fact]
    public void Generate_IncludesSurprisingConnections()
    {
        var (graph, analysis) = CreateSampleAnalysis();

        var report = _generator.Generate(
            graph, analysis,
            new Dictionary<int, string> { [0] = "Core" },
            new Dictionary<int, double> { [0] = 0.5 },
            "Surprises");

        Assert.Contains("Surprising Connections", report);
    }

    [Fact]
    public void Generate_IncludesSuggestedQuestions()
    {
        var (graph, analysis) = CreateSampleAnalysis();

        var report = _generator.Generate(
            graph, analysis,
            new Dictionary<int, string> { [0] = "Core" },
            new Dictionary<int, double> { [0] = 0.5 },
            "Questions");

        Assert.Contains("Suggested Questions", report);
    }

    [Fact]
    public void Generate_NullGraph_ThrowsArgumentNullException()
    {
        var analysis = new AnalysisResult
        {
            GodNodes = Array.Empty<GodNode>(),
            SurprisingConnections = Array.Empty<SurprisingConnection>(),
            SuggestedQuestions = Array.Empty<SuggestedQuestion>(),
            Statistics = new GraphStatistics
            {
                NodeCount = 0, EdgeCount = 0, CommunityCount = 0,
                AverageDegree = 0, IsolatedNodeCount = 0
            }
        };

        Assert.Throws<ArgumentNullException>(() =>
            _generator.Generate(null!, analysis,
                new Dictionary<int, string>(), new Dictionary<int, double>(), "X"));
    }

    [Fact]
    public void Generate_NullAnalysis_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _generator.Generate(new KnowledgeGraph(), null!,
                new Dictionary<int, string>(), new Dictionary<int, double>(), "X"));
    }

    [Fact]
    public void Generate_IncludesKnowledgeGapsForIsolatedNodes()
    {
        var graph = new KnowledgeGraph();
        var isolated = new GraphNode { Id = "Lone", Label = "LoneNode", Type = "Class", Community = 0 };
        graph.AddNode(isolated);

        var analysis = new AnalysisResult
        {
            GodNodes = Array.Empty<GodNode>(),
            SurprisingConnections = Array.Empty<SurprisingConnection>(),
            SuggestedQuestions = Array.Empty<SuggestedQuestion>(),
            Statistics = new GraphStatistics
            {
                NodeCount = 1, EdgeCount = 0, CommunityCount = 1,
                AverageDegree = 0, IsolatedNodeCount = 1
            }
        };

        var report = _generator.Generate(
            graph, analysis,
            new Dictionary<int, string> { [0] = "Lone" },
            new Dictionary<int, double> { [0] = 0 },
            "Gaps");

        Assert.Contains("Knowledge Gaps", report);
        Assert.Contains("isolated node", report);
    }

    private static (KnowledgeGraph, AnalysisResult) CreateSampleAnalysis()
    {
        var graph = new KnowledgeGraph();
        var hub = new GraphNode { Id = "Hub", Label = "Hub", Type = "Class", Community = 0 };
        var n1 = new GraphNode { Id = "A", Label = "Alpha", Type = "Method", Community = 0 };
        var n2 = new GraphNode { Id = "B", Label = "Beta", Type = "Function", Community = 0 };
        graph.AddNode(hub);
        graph.AddNode(n1);
        graph.AddNode(n2);
        graph.AddEdge(new GraphEdge { Source = hub, Target = n1, Relationship = "calls" });
        graph.AddEdge(new GraphEdge { Source = hub, Target = n2, Relationship = "uses" });

        var analysis = new AnalysisResult
        {
            GodNodes = new[] { new GodNode { Id = "Hub", Label = "Hub", EdgeCount = 2 } },
            SurprisingConnections = Array.Empty<SurprisingConnection>(),
            SuggestedQuestions = new[]
            {
                new SuggestedQuestion { Type = "god_node", Question = "Why is Hub so connected?", Why = "High degree" }
            },
            Statistics = new GraphStatistics
            {
                NodeCount = 3, EdgeCount = 2, CommunityCount = 1,
                AverageDegree = 1.33, IsolatedNodeCount = 0
            }
        };

        return (graph, analysis);
    }
}
