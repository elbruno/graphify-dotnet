using Graphify.Graph;
using Graphify.Models;

namespace Graphify.Integration.Tests.Helpers;

/// <summary>
/// Builds sample KnowledgeGraphs for integration tests.
/// </summary>
internal static class TestGraphFactory
{
    /// <summary>
    /// Creates a small graph with 3 classes and edges between them.
    /// </summary>
    public static KnowledgeGraph CreateSmallGraph()
    {
        var graph = new KnowledgeGraph();

        var nodeA = new GraphNode { Id = "app_program", Label = "Program", Type = "Entity", FilePath = "src/Program.cs", Confidence = Confidence.Extracted };
        var nodeB = new GraphNode { Id = "app_service", Label = "Service", Type = "Entity", FilePath = "src/Service.cs", Confidence = Confidence.Extracted };
        var nodeC = new GraphNode { Id = "app_repository", Label = "Repository", Type = "Entity", FilePath = "src/Repository.cs", Confidence = Confidence.Extracted };

        graph.AddNode(nodeA);
        graph.AddNode(nodeB);
        graph.AddNode(nodeC);

        graph.AddEdge(new GraphEdge { Source = nodeA, Target = nodeB, Relationship = "uses", Weight = 1.0, Confidence = Confidence.Extracted });
        graph.AddEdge(new GraphEdge { Source = nodeB, Target = nodeC, Relationship = "uses", Weight = 1.0, Confidence = Confidence.Extracted });
        graph.AddEdge(new GraphEdge { Source = nodeA, Target = nodeC, Relationship = "imports", Weight = 0.5, Confidence = Confidence.Inferred });

        return graph;
    }

    /// <summary>
    /// Creates a larger graph suitable for clustering and analysis tests.
    /// </summary>
    public static KnowledgeGraph CreateClusterableGraph()
    {
        var graph = new KnowledgeGraph();

        var nodes = new List<GraphNode>();
        for (int i = 0; i < 5; i++)
        {
            var node = new GraphNode { Id = $"ui_{i}", Label = $"UiComponent{i}", Type = "Entity", FilePath = $"src/ui/Component{i}.cs", Confidence = Confidence.Extracted };
            graph.AddNode(node);
            nodes.Add(node);
        }
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            graph.AddEdge(new GraphEdge { Source = nodes[i], Target = nodes[i + 1], Relationship = "uses", Weight = 1.0, Confidence = Confidence.Extracted });
        }

        var dataNodes = new List<GraphNode>();
        for (int i = 0; i < 5; i++)
        {
            var node = new GraphNode { Id = $"data_{i}", Label = $"DataService{i}", Type = "Entity", FilePath = $"src/data/Service{i}.cs", Confidence = Confidence.Extracted };
            graph.AddNode(node);
            dataNodes.Add(node);
        }
        for (int i = 0; i < dataNodes.Count - 1; i++)
        {
            graph.AddEdge(new GraphEdge { Source = dataNodes[i], Target = dataNodes[i + 1], Relationship = "uses", Weight = 1.0, Confidence = Confidence.Extracted });
        }

        graph.AddEdge(new GraphEdge { Source = nodes[0], Target = dataNodes[0], Relationship = "calls", Weight = 0.5, Confidence = Confidence.Inferred });

        return graph;
    }

    /// <summary>
    /// Creates mock ExtractionResults as if a .cs file was parsed.
    /// </summary>
    public static List<ExtractionResult> CreateMockExtractionResults(string sourceFile = "src/Sample.cs")
    {
        return
        [
            new ExtractionResult
            {
                SourceFilePath = sourceFile,
                Method = ExtractionMethod.Ast,
                Nodes =
                [
                    new ExtractedNode { Id = "sample_myclass", Label = "MyClass", FileType = FileType.Code, SourceFile = sourceFile, SourceLocation = "line:1" },
                    new ExtractedNode { Id = "sample_mymethod", Label = "MyMethod", FileType = FileType.Code, SourceFile = sourceFile, SourceLocation = "line:5" }
                ],
                Edges =
                [
                    new ExtractedEdge { Source = "sample_myclass", Target = "sample_mymethod", Relation = "contains", Confidence = Confidence.Extracted, SourceFile = sourceFile, Weight = 1.0 }
                ]
            }
        ];
    }

    /// <summary>
    /// Creates mock ExtractionResults for multiple files.
    /// </summary>
    public static List<ExtractionResult> CreateMultiFileExtractionResults()
    {
        return
        [
            new ExtractionResult
            {
                SourceFilePath = "src/Program.cs",
                Method = ExtractionMethod.Ast,
                Nodes =
                [
                    new ExtractedNode { Id = "program_main", Label = "Main", FileType = FileType.Code, SourceFile = "src/Program.cs" },
                    new ExtractedNode { Id = "program_app", Label = "App", FileType = FileType.Code, SourceFile = "src/Program.cs" }
                ],
                Edges =
                [
                    new ExtractedEdge { Source = "program_main", Target = "program_app", Relation = "contains", Confidence = Confidence.Extracted, SourceFile = "src/Program.cs", Weight = 1.0 }
                ]
            },
            new ExtractionResult
            {
                SourceFilePath = "src/Service.cs",
                Method = ExtractionMethod.Ast,
                Nodes =
                [
                    new ExtractedNode { Id = "service_handler", Label = "Handler", FileType = FileType.Code, SourceFile = "src/Service.cs" },
                    new ExtractedNode { Id = "program_app", Label = "App", FileType = FileType.Code, SourceFile = "src/Service.cs" }
                ],
                Edges =
                [
                    new ExtractedEdge { Source = "service_handler", Target = "program_app", Relation = "uses", Confidence = Confidence.Extracted, SourceFile = "src/Service.cs", Weight = 1.0 }
                ]
            }
        ];
    }
}
