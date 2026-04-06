using Graphify.Graph;
using Graphify.Models;
using Graphify.Pipeline;
using Xunit;

namespace Graphify.Tests.Pipeline;

[Trait("Category", "Pipeline")]
public sealed class GraphBuilderTests
{
    [Fact]
    public async Task ExecuteAsync_SingleExtraction_CreatesGraph()
    {
        // Arrange
        var builder = new GraphBuilder();
        var extraction = new ExtractionResult
        {
            Nodes = new[]
            {
                new ExtractedNode
                {
                    Id = "test_class",
                    Label = "TestClass",
                    FileType = FileType.Code,
                    SourceFile = "Test.cs"
                },
                new ExtractedNode
                {
                    Id = "test_method",
                    Label = "TestMethod",
                    FileType = FileType.Code,
                    SourceFile = "Test.cs"
                }
            },
            Edges = new[]
            {
                new ExtractedEdge
                {
                    Source = "test_class",
                    Target = "test_method",
                    Relation = "contains",
                    Confidence = Confidence.Extracted,
                    SourceFile = "Test.cs",
                    Weight = 1.0
                }
            },
            SourceFilePath = "Test.cs",
            Method = ExtractionMethod.Ast
        };

        // Act
        var graph = await builder.ExecuteAsync(new[] { extraction });

        // Assert
        Assert.NotNull(graph);
        Assert.Equal(2, graph.NodeCount);
        Assert.Equal(1, graph.EdgeCount);

        var classNode = graph.GetNode("test_class");
        Assert.NotNull(classNode);
        Assert.Equal("TestClass", classNode.Label);

        var methodNode = graph.GetNode("test_method");
        Assert.NotNull(methodNode);
        Assert.Equal("TestMethod", methodNode.Label);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleExtractions_MergesGraph()
    {
        // Arrange
        var builder = new GraphBuilder();
        var extraction1 = new ExtractionResult
        {
            Nodes = new[]
            {
                new ExtractedNode
                {
                    Id = "shared_node",
                    Label = "SharedClass",
                    FileType = FileType.Code,
                    SourceFile = "File1.cs"
                }
            },
            Edges = Array.Empty<ExtractedEdge>(),
            SourceFilePath = "File1.cs",
            Method = ExtractionMethod.Ast
        };

        var extraction2 = new ExtractionResult
        {
            Nodes = new[]
            {
                new ExtractedNode
                {
                    Id = "shared_node",
                    Label = "SharedClass",
                    FileType = FileType.Code,
                    SourceFile = "File2.cs"
                },
                new ExtractedNode
                {
                    Id = "unique_node",
                    Label = "UniqueClass",
                    FileType = FileType.Code,
                    SourceFile = "File2.cs"
                }
            },
            Edges = Array.Empty<ExtractedEdge>(),
            SourceFilePath = "File2.cs",
            Method = ExtractionMethod.Ast
        };

        // Act
        var graph = await builder.ExecuteAsync(new[] { extraction1, extraction2 });

        // Assert
        Assert.NotNull(graph);
        Assert.Equal(2, graph.NodeCount); // shared_node deduplicated + unique_node
        
        var sharedNode = graph.GetNode("shared_node");
        Assert.NotNull(sharedNode);
        Assert.Equal("SharedClass", sharedNode.Label);
        
        var uniqueNode = graph.GetNode("unique_node");
        Assert.NotNull(uniqueNode);
        Assert.Equal("UniqueClass", uniqueNode.Label);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateNodes_AreMerged()
    {
        // Arrange
        var builder = new GraphBuilder();
        var extraction1 = new ExtractionResult
        {
            Nodes = new[]
            {
                new ExtractedNode
                {
                    Id = "node1",
                    Label = "FirstLabel",
                    FileType = FileType.Code,
                    SourceFile = "File1.cs",
                    Metadata = new Dictionary<string, object> { ["key1"] = "value1" }
                }
            },
            Edges = Array.Empty<ExtractedEdge>(),
            SourceFilePath = "File1.cs",
            Method = ExtractionMethod.Ast
        };

        var extraction2 = new ExtractionResult
        {
            Nodes = new[]
            {
                new ExtractedNode
                {
                    Id = "node1",
                    Label = "SecondLabel",
                    FileType = FileType.Code,
                    SourceFile = "File2.cs",
                    Metadata = new Dictionary<string, object> { ["key2"] = "value2" }
                }
            },
            Edges = Array.Empty<ExtractedEdge>(),
            SourceFilePath = "File2.cs",
            Method = ExtractionMethod.Ast
        };

        // Act
        var graph = await builder.ExecuteAsync(new[] { extraction1, extraction2 });

        // Assert
        Assert.Equal(1, graph.NodeCount); // Deduplicated
        var node = graph.GetNode("node1");
        Assert.NotNull(node);
        // Last extraction wins by default
        Assert.Equal("SecondLabel", node.Label);
        Assert.NotNull(node.Metadata);
        Assert.True(node.Metadata.ContainsKey("merge_count"));
        Assert.Equal("2", node.Metadata["merge_count"]);
    }

    [Fact]
    public async Task ExecuteAsync_EdgeWeightAccumulation_WorksCorrectly()
    {
        // Arrange
        var builder = new GraphBuilder();
        var extraction1 = new ExtractionResult
        {
            Nodes = new[]
            {
                new ExtractedNode { Id = "a", Label = "A", FileType = FileType.Code, SourceFile = "F.cs" },
                new ExtractedNode { Id = "b", Label = "B", FileType = FileType.Code, SourceFile = "F.cs" }
            },
            Edges = new[]
            {
                new ExtractedEdge
                {
                    Source = "a",
                    Target = "b",
                    Relation = "calls",
                    Confidence = Confidence.Extracted,
                    SourceFile = "F.cs",
                    Weight = 1.0
                }
            },
            SourceFilePath = "F.cs",
            Method = ExtractionMethod.Ast
        };

        var extraction2 = new ExtractionResult
        {
            Nodes = new[]
            {
                new ExtractedNode { Id = "a", Label = "A", FileType = FileType.Code, SourceFile = "F.cs" },
                new ExtractedNode { Id = "b", Label = "B", FileType = FileType.Code, SourceFile = "F.cs" }
            },
            Edges = new[]
            {
                new ExtractedEdge
                {
                    Source = "a",
                    Target = "b",
                    Relation = "calls",
                    Confidence = Confidence.Extracted,
                    SourceFile = "F.cs",
                    Weight = 2.0
                }
            },
            SourceFilePath = "F.cs",
            Method = ExtractionMethod.Ast
        };

        // Act
        var graph = await builder.ExecuteAsync(new[] { extraction1, extraction2 });

        // Assert
        Assert.Equal(1, graph.EdgeCount);
        var edge = graph.GetEdges().First();
        Assert.Equal(3.0, edge.Weight); // 1.0 + 2.0
        Assert.Equal("2", edge.Metadata["merge_count"]);
    }

    [Fact]
    public async Task ExecuteAsync_CreateFileNodes_AddsFileNodes()
    {
        // Arrange
        var options = new GraphBuilderOptions { CreateFileNodes = true };
        var builder = new GraphBuilder(options);
        var extraction = new ExtractionResult
        {
            Nodes = new[]
            {
                new ExtractedNode
                {
                    Id = "class1",
                    Label = "Class1",
                    FileType = FileType.Code,
                    SourceFile = "Test.cs"
                }
            },
            Edges = Array.Empty<ExtractedEdge>(),
            SourceFilePath = "Test.cs",
            Method = ExtractionMethod.Ast
        };

        // Act
        var graph = await builder.ExecuteAsync(new[] { extraction });

        // Assert
        var fileNode = graph.GetNode("file:Test.cs");
        Assert.NotNull(fileNode);
        Assert.Equal("Test.cs", fileNode.Label);
        Assert.Equal("File", fileNode.Type);

        // Should have contains edge from file to class
        var containsEdges = graph.GetEdges()
            .Where(e => e.Relationship == "contains" && e.Source.Id == "file:Test.cs")
            .ToList();
        Assert.NotEmpty(containsEdges);
        Assert.Contains(containsEdges, e => e.Target.Id == "class1");
    }

    [Fact]
    public async Task ExecuteAsync_SkipMissingNodes_DoesNotCreateDanglingEdges()
    {
        // Arrange
        var builder = new GraphBuilder();
        var extraction = new ExtractionResult
        {
            Nodes = new[]
            {
                new ExtractedNode
                {
                    Id = "existing_node",
                    Label = "ExistingNode",
                    FileType = FileType.Code,
                    SourceFile = "Test.cs"
                }
            },
            Edges = new[]
            {
                new ExtractedEdge
                {
                    Source = "existing_node",
                    Target = "missing_node",
                    Relation = "imports",
                    Confidence = Confidence.Extracted,
                    SourceFile = "Test.cs",
                    Weight = 1.0
                }
            },
            SourceFilePath = "Test.cs",
            Method = ExtractionMethod.Ast
        };

        // Act
        var graph = await builder.ExecuteAsync(new[] { extraction });

        // Assert
        Assert.Equal(1, graph.NodeCount); // Only existing_node
        Assert.Equal(0, graph.EdgeCount); // Edge skipped because target doesn't exist
    }

    [Fact]
    public async Task ExecuteAsync_MinEdgeWeight_FiltersLowWeightEdges()
    {
        // Arrange
        var options = new GraphBuilderOptions { MinEdgeWeight = 2.0 };
        var builder = new GraphBuilder(options);
        var extraction = new ExtractionResult
        {
            Nodes = new[]
            {
                new ExtractedNode { Id = "a", Label = "A", FileType = FileType.Code, SourceFile = "F.cs" },
                new ExtractedNode { Id = "b", Label = "B", FileType = FileType.Code, SourceFile = "F.cs" },
                new ExtractedNode { Id = "c", Label = "C", FileType = FileType.Code, SourceFile = "F.cs" }
            },
            Edges = new[]
            {
                new ExtractedEdge
                {
                    Source = "a",
                    Target = "b",
                    Relation = "calls",
                    Confidence = Confidence.Extracted,
                    SourceFile = "F.cs",
                    Weight = 1.0 // Below threshold
                },
                new ExtractedEdge
                {
                    Source = "a",
                    Target = "c",
                    Relation = "calls",
                    Confidence = Confidence.Extracted,
                    SourceFile = "F.cs",
                    Weight = 3.0 // Above threshold
                }
            },
            SourceFilePath = "F.cs",
            Method = ExtractionMethod.Ast
        };

        // Act
        var graph = await builder.ExecuteAsync(new[] { extraction });

        // Assert
        Assert.Equal(3, graph.NodeCount);
        Assert.Equal(1, graph.EdgeCount); // Only high-weight edge
        var edge = graph.GetEdges().First();
        Assert.Equal("a", edge.Source.Id);
        Assert.Equal("c", edge.Target.Id);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyInput_ReturnsEmptyGraph()
    {
        // Arrange
        var builder = new GraphBuilder();
        var emptyList = Array.Empty<ExtractionResult>();

        // Act
        var graph = await builder.ExecuteAsync(emptyList);

        // Assert
        Assert.NotNull(graph);
        Assert.Equal(0, graph.NodeCount);
        Assert.Equal(0, graph.EdgeCount);
    }

    [Fact]
    public async Task ExecuteAsync_ConfidenceMerge_KeepsHighest()
    {
        // Arrange
        var builder = new GraphBuilder();
        var extraction = new ExtractionResult
        {
            Nodes = new[]
            {
                new ExtractedNode { Id = "a", Label = "A", FileType = FileType.Code, SourceFile = "F.cs" },
                new ExtractedNode { Id = "b", Label = "B", FileType = FileType.Code, SourceFile = "F.cs" }
            },
            Edges = new[]
            {
                new ExtractedEdge
                {
                    Source = "a",
                    Target = "b",
                    Relation = "calls",
                    Confidence = Confidence.Ambiguous,
                    SourceFile = "F.cs",
                    Weight = 1.0
                },
                new ExtractedEdge
                {
                    Source = "a",
                    Target = "b",
                    Relation = "calls",
                    Confidence = Confidence.Extracted,
                    SourceFile = "F.cs",
                    Weight = 1.0
                }
            },
            SourceFilePath = "F.cs",
            Method = ExtractionMethod.Ast
        };

        // Act
        var graph = await builder.ExecuteAsync(new[] { extraction });

        // Assert
        Assert.Equal(1, graph.EdgeCount);
        var edge = graph.GetEdges().First();
        // Extracted is higher confidence than Ambiguous
        Assert.Equal(Confidence.Extracted, edge.Confidence);
        Assert.Equal(2.0, edge.Weight); // Weights still accumulate
    }
}
