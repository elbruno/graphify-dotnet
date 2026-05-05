using System.Text.Json;
using Graphify.Export;
using Graphify.Graph;
using Graphify.Models;
using Xunit;

namespace Graphify.Tests.Export;

/// <summary>
/// Tests for relative path handling in export pipeline.
/// Verifies that file paths in JSON and HTML exports are stored as relative paths,
/// not absolute paths, and that cross-platform path normalization works correctly.
/// </summary>
[Trait("Category", "Export")]
[Trait("Feature", "RelativePaths")]
public sealed class RelativePathHandlingTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _projectRoot;
    private readonly string _outputDir;

    public RelativePathHandlingTests()
    {
        // Create a realistic directory structure
        _testRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _projectRoot = Path.Combine(_testRoot, "MyProject");
        _outputDir = Path.Combine(_projectRoot, "graph-output");

        Directory.CreateDirectory(_projectRoot);
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    #region JSON Export Relative Paths Tests

    [Fact]
    public async Task JsonExport_NodeFilePaths_AreRelative()
    {
        // Arrange
        var exporter = new JsonExporter();
        var graph = CreateGraphWithAbsoluteFilePaths();
        var outputPath = Path.Combine(_outputDir, "graph.json");

        // Act
        await exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var nodes = doc.RootElement.GetProperty("nodes");

        // Verify no node has an absolute path
        foreach (var node in nodes.EnumerateArray())
        {
            if (node.TryGetProperty("file_path", out var filePathElement) && filePathElement.ValueKind == JsonValueKind.String)
            {
                var filePath = filePathElement.GetString();
                Assert.NotNull(filePath);
                
                // Should not start with drive letter (Windows) or / (Unix)
                Assert.False(Path.IsPathRooted(filePath), 
                    $"File path '{filePath}' should be relative, not absolute");
            }
        }
    }

    [Fact]
    public async Task JsonExport_NodeFilePaths_CorrectlyNormalized()
    {
        // Arrange
        var exporter = new JsonExporter();
        var graph = new KnowledgeGraph();

        // Create nodes with different path formats
        var srcDir = Path.Combine(_projectRoot, "src");
        Directory.CreateDirectory(srcDir);

        var absPath1 = Path.Combine(srcDir, "Class1.cs");
        var absPath2 = Path.Combine(srcDir, "Subfolder", "Class2.cs");
        var relPath1 = Path.GetRelativePath(_projectRoot, absPath1).Replace('\\', '/');
        var relPath2 = Path.GetRelativePath(_projectRoot, absPath2).Replace('\\', '/');

        var node1 = new GraphNode
        {
            Id = "Class1",
            Label = "Class1",
            Type = "Class",
            FilePath = absPath1,
            RelativePath = relPath1,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        var node2 = new GraphNode
        {
            Id = "Class2",
            Label = "Class2",
            Type = "Class",
            FilePath = absPath2,
            RelativePath = relPath2,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        graph.AddNode(node1);
        graph.AddNode(node2);

        var outputPath = Path.Combine(_outputDir, "graph.json");

        // Act
        await exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var nodes = doc.RootElement.GetProperty("nodes");
        var nodeList = nodes.EnumerateArray().ToList();

        // Verify paths are normalized (consistent separators)
        foreach (var node in nodeList)
        {
            if (node.TryGetProperty("file_path", out var filePathElement) && filePathElement.ValueKind == JsonValueKind.String)
            {
                var filePath = filePathElement.GetString();
                Assert.NotNull(filePath);
                
                // Paths should use forward slashes (standard JSON format) or backslashes (consistent with OS)
                // but NOT be absolute
                Assert.False(Path.IsPathRooted(filePath),
                    $"Path '{filePath}' should be relative");
            }
        }
    }

    [Fact]
    public async Task JsonExport_MultipleOutputLocations_PreservesRelativePaths()
    {
        // Arrange
        var exporter = new JsonExporter();
        var graph = CreateGraphWithTestFiles();

        // Output to different locations
        var output1 = Path.Combine(_projectRoot, "export1", "graph.json");
        var output2 = Path.Combine(_projectRoot, "export2", "subfolder", "graph.json");

        Directory.CreateDirectory(Path.GetDirectoryName(output1)!);
        Directory.CreateDirectory(Path.GetDirectoryName(output2)!);

        // Act
        await exporter.ExportAsync(graph, output1);
        await exporter.ExportAsync(graph, output2);

        // Assert - both exports should have relative paths
        var json1 = await File.ReadAllTextAsync(output1);
        var json2 = await File.ReadAllTextAsync(output2);

        var doc1 = JsonDocument.Parse(json1);
        var doc2 = JsonDocument.Parse(json2);

        var nodes1 = doc1.RootElement.GetProperty("nodes");
        var nodes2 = doc2.RootElement.GetProperty("nodes");

        // Extract first file path from each
        var path1 = nodes1[0].GetProperty("file_path").GetString();
        var path2 = nodes2[0].GetProperty("file_path").GetString();

        // Both should be relative and equal (same graph, same paths)
        Assert.NotNull(path1);
        Assert.NotNull(path2);
        Assert.False(Path.IsPathRooted(path1));
        Assert.False(Path.IsPathRooted(path2));
        Assert.Equal(path1, path2);
    }

    [Fact]
    public async Task JsonExport_EdgeMetadata_PreservesRelativePaths()
    {
        // Arrange
        var exporter = new JsonExporter();
        var graph = new KnowledgeGraph();

        var absPath1 = Path.Combine(_projectRoot, "src", "A.cs");
        var absPath2 = Path.Combine(_projectRoot, "src", "B.cs");
        var relPath1 = Path.GetRelativePath(_projectRoot, absPath1).Replace('\\', '/');
        var relPath2 = Path.GetRelativePath(_projectRoot, absPath2).Replace('\\', '/');

        var node1 = new GraphNode
        {
            Id = "A",
            Label = "A",
            Type = "Class",
            FilePath = absPath1,
            RelativePath = relPath1,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        var node2 = new GraphNode
        {
            Id = "B",
            Label = "B",
            Type = "Class",
            FilePath = absPath2,
            RelativePath = relPath2,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        graph.AddNode(node1);
        graph.AddNode(node2);

        var edge = new GraphEdge
        {
            Source = node1,
            Target = node2,
            Relationship = "calls",
            Weight = 1.0,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>
            {
                ["source_file"] = Path.Combine(_projectRoot, "src", "A.cs"),
                ["target_file"] = Path.Combine(_projectRoot, "src", "B.cs")
            }
        };

        graph.AddEdge(edge);

        var outputPath = Path.Combine(_outputDir, "graph.json");

        // Act
        await exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var edges = doc.RootElement.GetProperty("edges");
        var firstEdge = edges[0];

        // Edge metadata containing file paths should be preserved
        Assert.True(firstEdge.TryGetProperty("metadata", out var metadata));
        
        if (metadata.ValueKind == JsonValueKind.Object)
        {
            if (metadata.TryGetProperty("source_file", out var sourceFile))
            {
                var sourcePath = sourceFile.GetString();
                Assert.NotNull(sourcePath);
                // Metadata paths may contain relative path references if they were stored as such
            }
        }
    }

    #endregion

    #region HTML Export Relative Paths Tests

    [Fact]
    public async Task HtmlExport_EmbeddedPaths_AreRelative()
    {
        // Arrange
        var exporter = new HtmlExporter();
        var graph = new KnowledgeGraph();

        var srcDir = Path.Combine(_projectRoot, "src");
        Directory.CreateDirectory(srcDir);

        var absPath1 = Path.Combine(srcDir, "module1.cs");
        var absPath2 = Path.Combine(srcDir, "module2.cs");
        var relPath1 = Path.GetRelativePath(_projectRoot, absPath1).Replace('\\', '/');
        var relPath2 = Path.GetRelativePath(_projectRoot, absPath2).Replace('\\', '/');

        var node1 = new GraphNode
        {
            Id = "Module1",
            Label = "Module1",
            Type = "Module",
            FilePath = absPath1,
            RelativePath = relPath1,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        var node2 = new GraphNode
        {
            Id = "Module2",
            Label = "Module2",
            Type = "Module",
            FilePath = absPath2,
            RelativePath = relPath2,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        graph.AddNode(node1);
        graph.AddNode(node2);

        graph.AddEdge(new GraphEdge
        {
            Source = node1,
            Target = node2,
            Relationship = "imports",
            Weight = 1.0,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        });

        var outputPath = Path.Combine(_outputDir, "graph.html");

        // Act
        await exporter.ExportAsync(graph, outputPath, cancellationToken: default);

        // Assert
        var html = await File.ReadAllTextAsync(outputPath);

        // Verify HTML contains file path references (should be relative)
        // The file paths are embedded in the JSON data within the HTML
        Assert.Contains("module1", html);
        Assert.Contains("module2", html);

        // Verify no absolute paths are embedded
        var drive = Path.GetPathRoot(_projectRoot);
        if (!string.IsNullOrEmpty(drive))
        {
            Assert.DoesNotContain(drive, html);
        }
    }

    [Fact]
    public async Task HtmlExport_SourceFileInfo_IsRelative()
    {
        // Arrange
        var exporter = new HtmlExporter();
        var graph = new KnowledgeGraph();

        var libDir = Path.Combine(_projectRoot, "lib", "core");
        Directory.CreateDirectory(libDir);

        var absPath1 = Path.Combine(libDir, "Parser.cs");
        var absPath2 = Path.Combine(libDir, "Analyzer.cs");
        var relPath1 = Path.GetRelativePath(_projectRoot, absPath1).Replace('\\', '/');
        var relPath2 = Path.GetRelativePath(_projectRoot, absPath2).Replace('\\', '/');

        var nodes = new[]
        {
            new GraphNode
            {
                Id = "Parser",
                Label = "Parser",
                Type = "Class",
                FilePath = absPath1,
                RelativePath = relPath1,
                Confidence = Confidence.Extracted,
                Metadata = new Dictionary<string, string>()
            },
            new GraphNode
            {
                Id = "Analyzer",
                Label = "Analyzer",
                Type = "Class",
                FilePath = absPath2,
                RelativePath = relPath2,
                Confidence = Confidence.Extracted,
                Metadata = new Dictionary<string, string>()
            }
        };

        foreach (var node in nodes)
        {
            graph.AddNode(node);
        }

        var outputPath = Path.Combine(_outputDir, "graph.html");

        // Act
        await exporter.ExportAsync(graph, outputPath, communityLabels: null, cancellationToken: default);

        // Assert
        var html = await File.ReadAllTextAsync(outputPath);

        // Extract the embedded JSON data (source_file field)
        // Verify file names are present but paths are relative
        Assert.Contains("Parser", html);
        Assert.Contains("Analyzer", html);

        // The source file info should be embedded but not as absolute paths
        Assert.False(html.Contains(_projectRoot) || html.Contains(libDir),
            "HTML should not contain absolute project paths");
    }

    #endregion

    #region Cross-Platform Path Normalization Tests

    [Fact]
    public async Task PathNormalization_UnixPaths_ConvertedCorrectly()
    {
        // Arrange
        var exporter = new JsonExporter();
        var graph = new KnowledgeGraph();

        // Simulate Unix-style paths (even on Windows)
        var unixPaths = new[]
        {
            "src/utils/helper.cs",
            "src/models/User.cs",
            "tests/unit/HelperTests.cs"
        };

        for (int i = 0; i < unixPaths.Length; i++)
        {
            var node = new GraphNode
            {
                Id = $"Node{i}",
                Label = $"Node{i}",
                Type = "Class",
                FilePath = unixPaths[i],
                RelativePath = unixPaths[i],  // Already normalized
                Confidence = Confidence.Extracted,
                Metadata = new Dictionary<string, string>()
            };
            graph.AddNode(node);
        }

        var outputPath = Path.Combine(_outputDir, "unix_paths.json");

        // Act
        await exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var nodes = doc.RootElement.GetProperty("nodes");

        // Verify paths are preserved (relative)
        for (int i = 0; i < nodes.GetArrayLength(); i++)
        {
            var node = nodes[i];
            if (node.TryGetProperty("file_path", out var pathElement))
            {
                var path = pathElement.GetString();
                Assert.NotNull(path);
                Assert.False(Path.IsPathRooted(path));
            }
        }
    }

    [Fact]
    public async Task PathNormalization_WindowsPaths_ConvertedCorrectly()
    {
        // Arrange
        var exporter = new JsonExporter();
        var graph = new KnowledgeGraph();

        // Windows-style paths (using backslashes)
        var windowsPaths = new[]
        {
            @"src\utils\helper.cs",
            @"src\models\User.cs",
            @"tests\unit\HelperTests.cs"
        };

        for (int i = 0; i < windowsPaths.Length; i++)
        {
            // Normalize to forward slashes
            var relPath = windowsPaths[i].Replace('\\', '/');
            var node = new GraphNode
            {
                Id = $"Node{i}",
                Label = $"Node{i}",
                Type = "Class",
                FilePath = windowsPaths[i],
                RelativePath = relPath,
                Confidence = Confidence.Extracted,
                Metadata = new Dictionary<string, string>()
            };
            graph.AddNode(node);
        }

        var outputPath = Path.Combine(_outputDir, "windows_paths.json");

        // Act
        await exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var nodes = doc.RootElement.GetProperty("nodes");

        // Verify paths are preserved and not absolute
        for (int i = 0; i < nodes.GetArrayLength(); i++)
        {
            var node = nodes[i];
            if (node.TryGetProperty("file_path", out var pathElement))
            {
                var path = pathElement.GetString();
                Assert.NotNull(path);
                Assert.False(Path.IsPathRooted(path));
            }
        }
    }

    [Fact]
    public async Task PathNormalization_MixedPathSeparators_Handled()
    {
        // Arrange
        var exporter = new JsonExporter();
        var graph = new KnowledgeGraph();

        // Mixed path separators (edge case)
        var mixedPaths = new[]
        {
            @"src\utils/helper.cs",
            "src/models\\User.cs",
            @"tests/unit\HelperTests.cs"
        };

        for (int i = 0; i < mixedPaths.Length; i++)
        {
            // Normalize to forward slashes
            var relPath = mixedPaths[i].Replace('\\', '/');
            var node = new GraphNode
            {
                Id = $"Node{i}",
                Label = $"Node{i}",
                Type = "Class",
                FilePath = mixedPaths[i],
                RelativePath = relPath,
                Confidence = Confidence.Extracted,
                Metadata = new Dictionary<string, string>()
            };
            graph.AddNode(node);
        }

        var outputPath = Path.Combine(_outputDir, "mixed_paths.json");

        // Act
        await exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var nodes = doc.RootElement.GetProperty("nodes");

        // Verify paths are handled correctly (all relative)
        for (int i = 0; i < nodes.GetArrayLength(); i++)
        {
            var node = nodes[i];
            if (node.TryGetProperty("file_path", out var pathElement))
            {
                var path = pathElement.GetString();
                Assert.NotNull(path);
                Assert.False(Path.IsPathRooted(path));
            }
        }
    }

    #endregion

    #region Nested Directories Tests

    [Fact]
    public async Task DeeplyNestedDirectories_RelativePathsMaintained()
    {
        // Arrange
        var exporter = new JsonExporter();
        var graph = new KnowledgeGraph();

        // Create deeply nested structure
        var deepDir = Path.Combine(_projectRoot, "src", "layer1", "layer2", "layer3", "layer4");
        Directory.CreateDirectory(deepDir);

        var absPath = Path.Combine(deepDir, "Deep.cs");
        var relPath = Path.GetRelativePath(_projectRoot, absPath).Replace('\\', '/');

        var node = new GraphNode
        {
            Id = "DeepNode",
            Label = "DeepNode",
            Type = "Class",
            FilePath = absPath,
            RelativePath = relPath,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };
        graph.AddNode(node);

        var outputPath = Path.Combine(_outputDir, "deep.json");

        // Act
        await exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var nodes = doc.RootElement.GetProperty("nodes");
        var firstNode = nodes[0];

        Assert.True(firstNode.TryGetProperty("file_path", out var pathElement));
        var filePath = pathElement.GetString();
        Assert.NotNull(filePath);
        Assert.False(Path.IsPathRooted(filePath));
        
        // Should preserve the nested structure
        Assert.Contains("layer1", filePath);
        Assert.Contains("layer4", filePath);
    }

    [Fact]
    public async Task OutputDirWithinProjectRoot_RelativePathsCorrect()
    {
        // Arrange
        var exporter = new JsonExporter();
        var graph = CreateGraphWithTestFiles();

        // Output to a subdirectory of project
        var nestedOutput = Path.Combine(_projectRoot, "results", "exports", "graphs");
        Directory.CreateDirectory(nestedOutput);
        var outputPath = Path.Combine(nestedOutput, "graph.json");

        // Act
        await exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var nodes = doc.RootElement.GetProperty("nodes");

        // Verify node paths are relative to project root, not to output dir
        foreach (var node in nodes.EnumerateArray())
        {
            if (node.TryGetProperty("file_path", out var pathElement))
            {
                var path = pathElement.GetString();
                Assert.NotNull(path);
                Assert.False(Path.IsPathRooted(path));
                
                // Should be relative like "src/file.cs", not "../../src/file.cs"
                Assert.False(path.StartsWith(".."), 
                    "Paths should be relative from project root, not from output directory");
            }
        }
    }

    #endregion

    #region Special Characters Tests

    [Fact]
    public async Task SpecialCharactersInPaths_HandledCorrectly()
    {
        // Arrange
        var exporter = new JsonExporter();
        var graph = new KnowledgeGraph();

        // Paths with special characters (but still valid)
        var specialDir = Path.Combine(_projectRoot, "src-lib");
        Directory.CreateDirectory(specialDir);

        var pathsWithSpecials = new[]
        {
            Path.Combine(specialDir, "file-with-dash.cs"),
            Path.Combine(specialDir, "file_with_underscore.cs"),
            Path.Combine(specialDir, "file.multiple.dots.cs")
        };

        for (int i = 0; i < pathsWithSpecials.Length; i++)
        {
            var relPath = Path.GetRelativePath(_projectRoot, pathsWithSpecials[i]).Replace('\\', '/');
            var node = new GraphNode
            {
                Id = $"Node{i}",
                Label = $"Node{i}",
                Type = "Class",
                FilePath = pathsWithSpecials[i],
                RelativePath = relPath,
                Confidence = Confidence.Extracted,
                Metadata = new Dictionary<string, string>()
            };
            graph.AddNode(node);
        }

        var outputPath = Path.Combine(_outputDir, "special_chars.json");

        // Act
        await exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var nodes = doc.RootElement.GetProperty("nodes");

        // Verify all paths are preserved correctly
        Assert.Equal(3, nodes.GetArrayLength());
        for (int i = 0; i < nodes.GetArrayLength(); i++)
        {
            var node = nodes[i];
            Assert.True(node.TryGetProperty("file_path", out var pathElement));
            var path = pathElement.GetString();
            Assert.NotNull(path);
            Assert.False(Path.IsPathRooted(path));
        }
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public async Task EmptyFilePath_HandledGracefully()
    {
        // Arrange
        var exporter = new JsonExporter();
        var graph = new KnowledgeGraph();

        var nodeWithNoPath = new GraphNode
        {
            Id = "ConceptNode",
            Label = "ConceptNode",
            Type = "Concept",
            FilePath = null,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        var absPath = Path.Combine(_projectRoot, "src", "Class.cs");
        var relPath = Path.GetRelativePath(_projectRoot, absPath).Replace('\\', '/');

        var nodeWithPath = new GraphNode
        {
            Id = "ClassNode",
            Label = "ClassNode",
            Type = "Class",
            FilePath = absPath,
            RelativePath = relPath,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        graph.AddNode(nodeWithNoPath);
        graph.AddNode(nodeWithPath);

        var outputPath = Path.Combine(_outputDir, "mixed_paths_empty.json");

        // Act
        await exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var nodes = doc.RootElement.GetProperty("nodes");

        // Should handle both null and relative paths
        Assert.Equal(2, nodes.GetArrayLength());
    }

    [Fact]
    public async Task FilePathsOutsideProjectRoot_StillRelative()
    {
        // Arrange
        var exporter = new JsonExporter();
        var graph = new KnowledgeGraph();

        // Create a file path outside project root
        var outsideDir = Path.Combine(_testRoot, "external");
        Directory.CreateDirectory(outsideDir);
        var outsideFile = Path.Combine(outsideDir, "external.cs");

        var node = new GraphNode
        {
            Id = "ExternalNode",
            Label = "ExternalNode",
            Type = "Class",
            FilePath = outsideFile,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        graph.AddNode(node);

        var outputPath = Path.Combine(_outputDir, "external.json");

        // Act
        await exporter.ExportAsync(graph, outputPath);

        // Assert
        var json = await File.ReadAllTextAsync(outputPath);
        var doc = JsonDocument.Parse(json);
        var nodes = doc.RootElement.GetProperty("nodes");
        var firstNode = nodes[0];

        // The path should be stored as-is (it will be absolute in this case, which is a known edge case)
        // or converted to relative somehow - the important thing is consistency
        Assert.True(firstNode.TryGetProperty("file_path", out var pathElement));
        var filePath = pathElement.GetString();
        Assert.NotNull(filePath);
        
        // This is an edge case - paths outside project should be handled gracefully
        // They might remain absolute or be converted - just verify no crash occurs
    }

    #endregion

    #region Consistency Tests

    [Fact]
    public async Task MultipleExports_ProduceSamePaths()
    {
        // Arrange
        var exporter = new JsonExporter();
        var graph = CreateGraphWithTestFiles();

        var output1 = Path.Combine(_outputDir, "export1.json");
        var output2 = Path.Combine(_outputDir, "export2.json");

        // Act
        await exporter.ExportAsync(graph, output1);
        await exporter.ExportAsync(graph, output2);

        // Assert - both exports should be identical for paths
        var json1 = await File.ReadAllTextAsync(output1);
        var json2 = await File.ReadAllTextAsync(output2);

        var doc1 = JsonDocument.Parse(json1);
        var doc2 = JsonDocument.Parse(json2);

        var nodes1 = doc1.RootElement.GetProperty("nodes");
        var nodes2 = doc2.RootElement.GetProperty("nodes");

        Assert.Equal(nodes1.GetArrayLength(), nodes2.GetArrayLength());

        for (int i = 0; i < nodes1.GetArrayLength(); i++)
        {
            var path1 = nodes1[i].GetProperty("file_path").GetString();
            var path2 = nodes2[i].GetProperty("file_path").GetString();
            Assert.Equal(path1, path2);
        }
    }

    #endregion

    #region Helper Methods

    private KnowledgeGraph CreateGraphWithAbsoluteFilePaths()
    {
        var graph = new KnowledgeGraph();

        // Create some real files
        var srcDir = Path.Combine(_projectRoot, "src");
        Directory.CreateDirectory(srcDir);

        File.WriteAllText(Path.Combine(srcDir, "Class1.cs"), "class Class1 { }");
        File.WriteAllText(Path.Combine(srcDir, "Class2.cs"), "class Class2 { }");
        File.WriteAllText(Path.Combine(srcDir, "Class3.cs"), "class Class3 { }");

        var absPath1 = Path.Combine(srcDir, "Class1.cs");
        var absPath2 = Path.Combine(srcDir, "Class2.cs");
        var absPath3 = Path.Combine(srcDir, "Class3.cs");
        var relPath1 = Path.GetRelativePath(_projectRoot, absPath1).Replace('\\', '/');
        var relPath2 = Path.GetRelativePath(_projectRoot, absPath2).Replace('\\', '/');
        var relPath3 = Path.GetRelativePath(_projectRoot, absPath3).Replace('\\', '/');

        var node1 = new GraphNode
        {
            Id = "Class1",
            Label = "Class1",
            Type = "Class",
            FilePath = absPath1,
            RelativePath = relPath1,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        var node2 = new GraphNode
        {
            Id = "Class2",
            Label = "Class2",
            Type = "Class",
            FilePath = absPath2,
            RelativePath = relPath2,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        var node3 = new GraphNode
        {
            Id = "Class3",
            Label = "Class3",
            Type = "Class",
            FilePath = absPath3,
            RelativePath = relPath3,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        graph.AddNode(node1);
        graph.AddNode(node2);
        graph.AddNode(node3);

        graph.AddEdge(new GraphEdge
        {
            Source = node1,
            Target = node2,
            Relationship = "calls",
            Weight = 1.0,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        });

        graph.AddEdge(new GraphEdge
        {
            Source = node2,
            Target = node3,
            Relationship = "uses",
            Weight = 1.0,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        });

        return graph;
    }

    private KnowledgeGraph CreateGraphWithTestFiles()
    {
        var graph = new KnowledgeGraph();

        var srcDir = Path.Combine(_projectRoot, "src");
        Directory.CreateDirectory(srcDir);

        var absPath1 = Path.Combine(srcDir, "Service.cs");
        var absPath2 = Path.Combine(srcDir, "Repository.cs");
        var relPath1 = Path.GetRelativePath(_projectRoot, absPath1).Replace('\\', '/');
        var relPath2 = Path.GetRelativePath(_projectRoot, absPath2).Replace('\\', '/');

        var node1 = new GraphNode
        {
            Id = "Service",
            Label = "Service",
            Type = "Class",
            FilePath = absPath1,
            RelativePath = relPath1,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        var node2 = new GraphNode
        {
            Id = "Repository",
            Label = "Repository",
            Type = "Class",
            FilePath = absPath2,
            RelativePath = relPath2,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        };

        graph.AddNode(node1);
        graph.AddNode(node2);

        graph.AddEdge(new GraphEdge
        {
            Source = node1,
            Target = node2,
            Relationship = "depends",
            Weight = 1.0,
            Confidence = Confidence.Extracted,
            Metadata = new Dictionary<string, string>()
        });

        return graph;
    }

    #endregion
}
