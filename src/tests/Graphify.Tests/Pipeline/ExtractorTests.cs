using Graphify.Models;
using Graphify.Pipeline;
using Xunit;

namespace Graphify.Tests.Pipeline;

[Trait("Category", "Pipeline")]
public sealed class ExtractorTests : IDisposable
{
    private readonly string _testRoot;
    private readonly Extractor _extractor = new();

    public ExtractorTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_testRoot);
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

    [Fact]
    public async Task ExecuteAsync_CSharpClass_ExtractsClassAndMethods()
    {
        // Arrange
        var content = @"
using System;

namespace TestNamespace
{
    public class Calculator
    {
        public int Add(int a, int b)
        {
            return a + b;
        }

        private void Reset()
        {
        }
    }
}";
        var filePath = Path.Combine(_testRoot, "Calculator.cs");
        await File.WriteAllTextAsync(filePath, content);

        var detectedFile = new DetectedFile
        {
            FilePath = filePath,
            FileName = "Calculator.cs",
            Extension = ".cs",
            Language = "CSharp",
            Category = FileCategory.Code,
            SizeBytes = content.Length
        };

        // Act
        var result = await _extractor.ExecuteAsync(detectedFile);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(filePath, result.SourceFilePath);
        Assert.Equal(ExtractionMethod.Ast, result.Method);

        // Should extract: file node, namespace, class, methods
        Assert.NotEmpty(result.Nodes);
        Assert.Contains(result.Nodes, n => n.Label == "Calculator.cs");
        Assert.Contains(result.Nodes, n => n.Label == "TestNamespace");
        Assert.Contains(result.Nodes, n => n.Label == "Calculator");
        Assert.Contains(result.Nodes, n => n.Label == "Add()");
        Assert.Contains(result.Nodes, n => n.Label == "Reset()");

        // Should have edges: file contains namespace, file contains class, file contains methods
        Assert.NotEmpty(result.Edges);
        Assert.Contains(result.Edges, e => e.Relation == "contains");
    }

    [Fact]
    public async Task ExecuteAsync_CSharpUsings_ExtractsImports()
    {
        // Arrange
        var content = @"
using System;
using System.Collections.Generic;
using System.Linq;

public class Test { }";
        var filePath = Path.Combine(_testRoot, "Test.cs");
        await File.WriteAllTextAsync(filePath, content);

        var detectedFile = new DetectedFile
        {
            FilePath = filePath,
            FileName = "Test.cs",
            Extension = ".cs",
            Language = "CSharp",
            Category = FileCategory.Code,
            SizeBytes = content.Length
        };

        // Act
        var result = await _extractor.ExecuteAsync(detectedFile);

        // Assert
        Assert.NotEmpty(result.Edges);
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();
        Assert.NotEmpty(imports);
        Assert.Contains(imports, e => e.Target.Contains("system"));
        Assert.Contains(imports, e => e.Target.Contains("generic") || e.Target.Contains("collections"));
        Assert.Contains(imports, e => e.Target.Contains("linq"));
    }

    [Fact]
    public async Task ExecuteAsync_PythonFunctionAndClass_ExtractsCorrectly()
    {
        // Arrange
        var content = @"
import os
import sys
from pathlib import Path

def calculate_sum(a, b):
    return a + b

class DataProcessor:
    def process(self, data):
        return data * 2

def main():
    pass
";
        var filePath = Path.Combine(_testRoot, "processor.py");
        await File.WriteAllTextAsync(filePath, content);

        var detectedFile = new DetectedFile
        {
            FilePath = filePath,
            FileName = "processor.py",
            Extension = ".py",
            Language = "Python",
            Category = FileCategory.Code,
            SizeBytes = content.Length
        };

        // Act
        var result = await _extractor.ExecuteAsync(detectedFile);

        // Assert
        Assert.NotEmpty(result.Nodes);
        Assert.Contains(result.Nodes, n => n.Label == "processor.py");
        Assert.Contains(result.Nodes, n => n.Label == "DataProcessor");
        Assert.Contains(result.Nodes, n => n.Label == "calculate_sum()");
        Assert.Contains(result.Nodes, n => n.Label == "process()");
        Assert.Contains(result.Nodes, n => n.Label == "main()");

        // Check imports
        var imports = result.Edges.Where(e => e.Relation == "imports" || e.Relation == "imports_from").ToList();
        Assert.NotEmpty(imports);
    }

    [Fact]
    public async Task ExecuteAsync_JavaScript_ExtractsCorrectly()
    {
        // Arrange
        var content = @"
import { useState } from 'react';
import axios from 'axios';

function App() {
    return <div>Hello</div>;
}

const calculateTotal = (items) => {
    return items.reduce((sum, item) => sum + item.price, 0);
};

export default App;
";
        var filePath = Path.Combine(_testRoot, "App.js");
        await File.WriteAllTextAsync(filePath, content);

        var detectedFile = new DetectedFile
        {
            FilePath = filePath,
            FileName = "App.js",
            Extension = ".js",
            Language = "JavaScript",
            Category = FileCategory.Code,
            SizeBytes = content.Length
        };

        // Act
        var result = await _extractor.ExecuteAsync(detectedFile);

        // Assert
        Assert.NotEmpty(result.Nodes);
        Assert.Contains(result.Nodes, n => n.Label == "App.js");
        Assert.Contains(result.Nodes, n => n.Label == "App()");
        Assert.Contains(result.Nodes, n => n.Label == "calculateTotal()");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyFile_ReturnsMinimalResult()
    {
        // Arrange
        var filePath = Path.Combine(_testRoot, "empty.cs");
        await File.WriteAllTextAsync(filePath, "");

        var detectedFile = new DetectedFile
        {
            FilePath = filePath,
            FileName = "empty.cs",
            Extension = ".cs",
            Language = "CSharp",
            Category = FileCategory.Code,
            SizeBytes = 0
        };

        // Act
        var result = await _extractor.ExecuteAsync(detectedFile);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(filePath, result.SourceFilePath);
        // At minimum should have file node
        Assert.NotEmpty(result.Nodes);
        Assert.Contains(result.Nodes, n => n.Label == "empty.cs");
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedLanguage_ReturnsEmptyResult()
    {
        // Arrange
        var content = "some random text";
        var filePath = Path.Combine(_testRoot, "readme.txt");
        await File.WriteAllTextAsync(filePath, content);

        var detectedFile = new DetectedFile
        {
            FilePath = filePath,
            FileName = "readme.txt",
            Extension = ".txt",
            Language = "Unknown",
            Category = FileCategory.Document,
            SizeBytes = content.Length
        };

        // Act
        var result = await _extractor.ExecuteAsync(detectedFile);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
        Assert.Equal(filePath, result.SourceFilePath);
        Assert.Equal(ExtractionMethod.Ast, result.Method);
    }

    [Fact]
    public async Task ExecuteAsync_SourceLocations_ArePopulated()
    {
        // Arrange
        var content = @"
namespace Test
{
    public class MyClass
    {
        public void MyMethod() { }
    }
}";
        var filePath = Path.Combine(_testRoot, "Source.cs");
        await File.WriteAllTextAsync(filePath, content);

        var detectedFile = new DetectedFile
        {
            FilePath = filePath,
            FileName = "Source.cs",
            Extension = ".cs",
            Language = "CSharp",
            Category = FileCategory.Code,
            SizeBytes = content.Length
        };

        // Act
        var result = await _extractor.ExecuteAsync(detectedFile);

        // Assert
        Assert.All(result.Nodes, node =>
        {
            Assert.NotNull(node.SourceLocation);
            Assert.StartsWith("L", node.SourceLocation);
        });

        Assert.All(result.Edges, edge =>
        {
            Assert.NotNull(edge.SourceLocation);
            Assert.StartsWith("L", edge.SourceLocation);
        });
    }

    [Fact]
    public async Task ExecuteAsync_MultipleClasses_AllExtracted()
    {
        // Arrange
        var content = @"
public class First { }
public class Second { }
internal class Third { }
public interface IFourth { }
public record Fifth(string Name);
";
        var filePath = Path.Combine(_testRoot, "Multiple.cs");
        await File.WriteAllTextAsync(filePath, content);

        var detectedFile = new DetectedFile
        {
            FilePath = filePath,
            FileName = "Multiple.cs",
            Extension = ".cs",
            Language = "CSharp",
            Category = FileCategory.Code,
            SizeBytes = content.Length
        };

        // Act
        var result = await _extractor.ExecuteAsync(detectedFile);

        // Assert
        Assert.Contains(result.Nodes, n => n.Label == "First");
        Assert.Contains(result.Nodes, n => n.Label == "Second");
        Assert.Contains(result.Nodes, n => n.Label == "Third");
        Assert.Contains(result.Nodes, n => n.Label == "IFourth");
        Assert.Contains(result.Nodes, n => n.Label == "Fifth");
    }
}
