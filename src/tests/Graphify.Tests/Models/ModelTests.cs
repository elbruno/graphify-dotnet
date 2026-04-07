using Graphify.Models;
using Xunit;

namespace Graphify.Tests.ModelTests;

/// <summary>
/// Tests for all model records, enums, and classes: construction, equality, defaults,
/// and enum coverage for ExtractedNode, ExtractedEdge, ExtractionResult, GraphNode,
/// GraphEdge, DetectedFile, AnalysisResult, GraphReport, and Confidence.
/// </summary>
[Trait("Category", "Models")]
public sealed class ModelTests
{
    // --- ExtractedNode ---

    [Fact]
    public void ExtractedNode_Construction_SetsRequiredProperties()
    {
        var node = new ExtractedNode
        {
            Id = "n1",
            Label = "MyClass",
            FileType = FileType.Code,
            SourceFile = "src/file.cs"
        };

        Assert.Equal("n1", node.Id);
        Assert.Equal("MyClass", node.Label);
        Assert.Equal(FileType.Code, node.FileType);
        Assert.Equal("src/file.cs", node.SourceFile);
        Assert.Null(node.SourceLocation);
        Assert.Null(node.Metadata);
    }

    [Fact]
    public void ExtractedNode_WithMetadata_PreservesValues()
    {
        var metadata = new Dictionary<string, object> { ["key"] = "value" };
        var node = new ExtractedNode
        {
            Id = "n1", Label = "L", FileType = FileType.Code,
            SourceFile = "f.cs", Metadata = metadata
        };

        Assert.NotNull(node.Metadata);
        Assert.Equal("value", node.Metadata!["key"]);
    }

    [Fact]
    public void ExtractedNode_RecordEquality()
    {
        var a = new ExtractedNode { Id = "n1", Label = "L", FileType = FileType.Code, SourceFile = "f.cs" };
        var b = new ExtractedNode { Id = "n1", Label = "L", FileType = FileType.Code, SourceFile = "f.cs" };

        Assert.Equal(a, b);
    }

    // --- ExtractedEdge ---

    [Fact]
    public void ExtractedEdge_Construction_SetsRequiredProperties()
    {
        var edge = new ExtractedEdge
        {
            Source = "s1", Target = "t1", Relation = "calls",
            Confidence = Confidence.Extracted, SourceFile = "f.cs"
        };

        Assert.Equal("s1", edge.Source);
        Assert.Equal("t1", edge.Target);
        Assert.Equal("calls", edge.Relation);
        Assert.Equal(Confidence.Extracted, edge.Confidence);
        Assert.Equal(1.0, edge.Weight);
    }

    [Fact]
    public void ExtractedEdge_DefaultWeight_IsOne()
    {
        var edge = new ExtractedEdge
        {
            Source = "a", Target = "b", Relation = "r",
            Confidence = Confidence.Inferred, SourceFile = "f"
        };

        Assert.Equal(1.0, edge.Weight);
    }

    [Fact]
    public void ExtractedEdge_CustomWeight()
    {
        var edge = new ExtractedEdge
        {
            Source = "a", Target = "b", Relation = "r",
            Confidence = Confidence.Ambiguous, SourceFile = "f", Weight = 0.5
        };

        Assert.Equal(0.5, edge.Weight);
    }

    // --- ExtractionResult ---

    [Fact]
    public void ExtractionResult_Construction_SetsDefaults()
    {
        var result = new ExtractionResult
        {
            Nodes = Array.Empty<ExtractedNode>(),
            Edges = Array.Empty<ExtractedEdge>(),
            SourceFilePath = "test.cs"
        };

        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
        Assert.Equal(ExtractionMethod.Ast, result.Method);
        Assert.Null(result.RawText);
        Assert.Null(result.ConfidenceScores);
    }

    [Fact]
    public void ExtractionResult_WithSemanticMethod()
    {
        var result = new ExtractionResult
        {
            Nodes = Array.Empty<ExtractedNode>(),
            Edges = Array.Empty<ExtractedEdge>(),
            SourceFilePath = "test.cs",
            Method = ExtractionMethod.Semantic
        };

        Assert.Equal(ExtractionMethod.Semantic, result.Method);
    }

    // --- GraphNode ---

    [Fact]
    public void GraphNode_Construction_SetsRequiredProperties()
    {
        var node = new GraphNode { Id = "g1", Label = "ClassA", Type = "Class" };

        Assert.Equal("g1", node.Id);
        Assert.Equal("ClassA", node.Label);
        Assert.Equal("Class", node.Type);
        Assert.Null(node.FilePath);
        Assert.Null(node.Language);
        Assert.Equal(Confidence.Extracted, node.Confidence);
        Assert.Null(node.Community);
        Assert.Null(node.Metadata);
    }

    [Fact]
    public void GraphNode_Equality_ByIdOnly()
    {
        var a = new GraphNode { Id = "x", Label = "A", Type = "Class" };
        var b = new GraphNode { Id = "x", Label = "B", Type = "Function" };

        Assert.Equal(a, b); // Equality is by Id only
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GraphNode_DifferentIds_NotEqual()
    {
        var a = new GraphNode { Id = "x", Label = "A", Type = "Class" };
        var b = new GraphNode { Id = "y", Label = "A", Type = "Class" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GraphNode_WithExpression_CreatesModifiedCopy()
    {
        var original = new GraphNode { Id = "x", Label = "A", Type = "Class" };
        var modified = original with { Community = 5 };

        Assert.Equal(5, modified.Community);
        Assert.Null(original.Community);
        Assert.Equal("x", modified.Id);
    }

    // --- GraphEdge ---

    [Fact]
    public void GraphEdge_Construction_SetsRequiredProperties()
    {
        var source = new GraphNode { Id = "s", Label = "S", Type = "Class" };
        var target = new GraphNode { Id = "t", Label = "T", Type = "Method" };
        var edge = new GraphEdge { Source = source, Target = target, Relationship = "calls" };

        Assert.Same(source, edge.Source);
        Assert.Same(target, edge.Target);
        Assert.Equal("calls", edge.Relationship);
        Assert.Equal(1.0, edge.Weight);
        Assert.Equal(Confidence.Extracted, edge.Confidence);
    }

    [Fact]
    public void GraphEdge_Equality_BySourceTargetRelationship()
    {
        var s = new GraphNode { Id = "s", Label = "S", Type = "T" };
        var t = new GraphNode { Id = "t", Label = "T", Type = "T" };
        var a = new GraphEdge { Source = s, Target = t, Relationship = "calls", Weight = 1.0 };
        var b = new GraphEdge { Source = s, Target = t, Relationship = "calls", Weight = 2.0 };

        Assert.Equal(a, b); // Weight doesn't affect equality
    }

    [Fact]
    public void GraphEdge_DifferentRelationship_NotEqual()
    {
        var s = new GraphNode { Id = "s", Label = "S", Type = "T" };
        var t = new GraphNode { Id = "t", Label = "T", Type = "T" };
        var a = new GraphEdge { Source = s, Target = t, Relationship = "calls" };
        var b = new GraphEdge { Source = s, Target = t, Relationship = "imports" };

        Assert.NotEqual(a, b);
    }

    // --- DetectedFile ---

    [Fact]
    public void DetectedFile_Construction_AllProperties()
    {
        var file = new DetectedFile(
            "/src/file.cs", "file.cs", ".cs", "CSharp",
            FileCategory.Code, 1024, "src/file.cs");

        Assert.Equal("/src/file.cs", file.FilePath);
        Assert.Equal("file.cs", file.FileName);
        Assert.Equal(".cs", file.Extension);
        Assert.Equal("CSharp", file.Language);
        Assert.Equal(FileCategory.Code, file.Category);
        Assert.Equal(1024, file.SizeBytes);
        Assert.Equal("src/file.cs", file.RelativePath);
    }

    [Fact]
    public void DetectedFile_RecordEquality()
    {
        var a = new DetectedFile("p", "f", ".cs", "CSharp", FileCategory.Code, 100, "r");
        var b = new DetectedFile("p", "f", ".cs", "CSharp", FileCategory.Code, 100, "r");

        Assert.Equal(a, b);
    }

    // --- AnalysisResult ---

    [Fact]
    public void AnalysisResult_Construction()
    {
        var result = new AnalysisResult
        {
            GodNodes = new[] { new GodNode { Id = "g", Label = "G", EdgeCount = 10 } },
            SurprisingConnections = Array.Empty<SurprisingConnection>(),
            SuggestedQuestions = Array.Empty<SuggestedQuestion>(),
            Statistics = new GraphStatistics
            {
                NodeCount = 5, EdgeCount = 3, CommunityCount = 1,
                AverageDegree = 1.2, IsolatedNodeCount = 0
            }
        };

        Assert.Single(result.GodNodes);
        Assert.Equal(5, result.Statistics.NodeCount);
    }

    [Fact]
    public void GraphStatistics_AllProperties()
    {
        var stats = new GraphStatistics
        {
            NodeCount = 100, EdgeCount = 250, CommunityCount = 5,
            AverageDegree = 5.0, IsolatedNodeCount = 3
        };

        Assert.Equal(100, stats.NodeCount);
        Assert.Equal(250, stats.EdgeCount);
        Assert.Equal(5, stats.CommunityCount);
        Assert.Equal(5.0, stats.AverageDegree);
        Assert.Equal(3, stats.IsolatedNodeCount);
    }

    // --- GraphReport ---

    [Fact]
    public void GraphReport_Construction()
    {
        var report = new GraphReport
        {
            Title = "Test Report",
            Summary = "A test summary",
            Communities = new[] { new Community { Id = 0, Label = "Core", Members = new[] { "A", "B" } } },
            GodNodes = Array.Empty<GodNode>(),
            SurprisingEdges = Array.Empty<SurprisingConnection>(),
            GeneratedAt = DateTimeOffset.UtcNow
        };

        Assert.Equal("Test Report", report.Title);
        Assert.Single(report.Communities);
        Assert.Equal("Core", report.Communities[0].Label);
    }

    [Fact]
    public void Community_WithCohesionScore()
    {
        var community = new Community
        {
            Id = 1, Label = "Services", Members = new[] { "A" },
            CohesionScore = 0.85
        };

        Assert.Equal(0.85, community.CohesionScore);
    }

    // --- Enums ---

    [Theory]
    [InlineData(Confidence.Extracted)]
    [InlineData(Confidence.Inferred)]
    [InlineData(Confidence.Ambiguous)]
    public void Confidence_HasExpectedValues(Confidence value)
    {
        Assert.True(Enum.IsDefined(value));
    }

    [Fact]
    public void Confidence_HasExactlyThreeValues()
    {
        Assert.Equal(3, Enum.GetValues<Confidence>().Length);
    }

    [Theory]
    [InlineData(FileType.Code)]
    [InlineData(FileType.Document)]
    [InlineData(FileType.Paper)]
    [InlineData(FileType.Image)]
    public void FileType_HasExpectedValues(FileType value)
    {
        Assert.True(Enum.IsDefined(value));
    }

    [Fact]
    public void FileType_HasExactlyFourValues()
    {
        Assert.Equal(4, Enum.GetValues<FileType>().Length);
    }

    [Theory]
    [InlineData(FileCategory.Code)]
    [InlineData(FileCategory.Documentation)]
    [InlineData(FileCategory.Media)]
    public void FileCategory_HasExpectedValues(FileCategory value)
    {
        Assert.True(Enum.IsDefined(value));
    }

    [Fact]
    public void FileCategory_HasExactlyThreeValues()
    {
        Assert.Equal(3, Enum.GetValues<FileCategory>().Length);
    }

    [Theory]
    [InlineData(ExtractionMethod.Ast)]
    [InlineData(ExtractionMethod.Semantic)]
    [InlineData(ExtractionMethod.Hybrid)]
    public void ExtractionMethod_HasExpectedValues(ExtractionMethod value)
    {
        Assert.True(Enum.IsDefined(value));
    }

    // --- SurprisingConnection ---

    [Fact]
    public void SurprisingConnection_Construction()
    {
        var conn = new SurprisingConnection
        {
            Source = "A", Target = "B",
            SourceFiles = new[] { "a.cs", "b.cs" },
            Relationship = "calls",
            Confidence = Confidence.Inferred,
            Why = "Cross-file"
        };

        Assert.Equal("A", conn.Source);
        Assert.Equal("Cross-file", conn.Why);
        Assert.Equal(2, conn.SourceFiles.Count);
    }

    // --- SuggestedQuestion ---

    [Fact]
    public void SuggestedQuestion_Construction()
    {
        var q = new SuggestedQuestion
        {
            Type = "god_node",
            Question = "Why is X so connected?",
            Why = "High degree"
        };

        Assert.Equal("god_node", q.Type);
        Assert.NotNull(q.Question);
    }

    // --- GodNode ---

    [Fact]
    public void GodNode_Construction()
    {
        var gn = new GodNode { Id = "hub", Label = "HubClass", EdgeCount = 42 };

        Assert.Equal("hub", gn.Id);
        Assert.Equal("HubClass", gn.Label);
        Assert.Equal(42, gn.EdgeCount);
    }
}
