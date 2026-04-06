using Graphify.Models;
using Graphify.Validation;
using Xunit;

namespace Graphify.Tests.Validation;

public class ExtractionValidatorTests
{
    private readonly ExtractionValidator _validator = new();

    [Fact]
    public void Validate_ValidExtractionResult_ReturnsSuccess()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>
            {
                new() { Id = "node1", Label = "Node 1", FileType = FileType.Code, SourceFile = "test.cs" }
            },
            Edges = new List<ExtractedEdge>
            {
                new() { Source = "node1", Target = "node1", Relation = "self", Confidence = Confidence.Extracted, SourceFile = "test.cs" }
            },
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_EmptyResult_ReturnsSuccess()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>(),
            Edges = new List<ExtractedEdge>(),
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_NullExtractionResult_ReturnsFailure()
    {
        // Act
        var result = _validator.Validate(null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("cannot be null", result.Errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_NodeWithEmptyId_ReturnsFailure()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>
            {
                new() { Id = "", Label = "Node 1", FileType = FileType.Code, SourceFile = "test.cs" }
            },
            Edges = new List<ExtractedEdge>(),
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("empty or null Id", result.Errors[0]);
    }

    [Fact]
    public void Validate_NodeWithNullId_ReturnsFailure()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>
            {
                new() { Id = null!, Label = "Node 1", FileType = FileType.Code, SourceFile = "test.cs" }
            },
            Edges = new List<ExtractedEdge>(),
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("empty or null Id", result.Errors[0]);
    }

    [Fact]
    public void Validate_NodeWithEmptyLabel_ReturnsFailure()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>
            {
                new() { Id = "node1", Label = "", FileType = FileType.Code, SourceFile = "test.cs" }
            },
            Edges = new List<ExtractedEdge>(),
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("empty or null Label", result.Errors[0]);
    }

    [Fact]
    public void Validate_NodeWithEmptySourceFile_ReturnsFailure()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>
            {
                new() { Id = "node1", Label = "Node 1", FileType = FileType.Code, SourceFile = "" }
            },
            Edges = new List<ExtractedEdge>(),
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("empty or null SourceFile", result.Errors[0]);
    }

    [Fact]
    public void Validate_EdgeWithMissingSource_ReturnsFailure()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>
            {
                new() { Id = "node1", Label = "Node 1", FileType = FileType.Code, SourceFile = "test.cs" }
            },
            Edges = new List<ExtractedEdge>
            {
                new() { Source = "nonexistent", Target = "node1", Relation = "uses", Confidence = Confidence.Extracted, SourceFile = "test.cs" }
            },
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("does not match any node id", result.Errors[0]);
    }

    [Fact]
    public void Validate_EdgeWithMissingTarget_ReturnsFailure()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>
            {
                new() { Id = "node1", Label = "Node 1", FileType = FileType.Code, SourceFile = "test.cs" }
            },
            Edges = new List<ExtractedEdge>
            {
                new() { Source = "node1", Target = "nonexistent", Relation = "uses", Confidence = Confidence.Extracted, SourceFile = "test.cs" }
            },
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("does not match any node id", result.Errors[0]);
    }

    [Fact]
    public void Validate_EdgeWithEmptySource_ReturnsFailure()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>
            {
                new() { Id = "node1", Label = "Node 1", FileType = FileType.Code, SourceFile = "test.cs" }
            },
            Edges = new List<ExtractedEdge>
            {
                new() { Source = "", Target = "node1", Relation = "uses", Confidence = Confidence.Extracted, SourceFile = "test.cs" }
            },
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("empty or null Source", result.Errors[0]);
    }

    [Fact]
    public void Validate_EdgeWithEmptyTarget_ReturnsFailure()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>
            {
                new() { Id = "node1", Label = "Node 1", FileType = FileType.Code, SourceFile = "test.cs" }
            },
            Edges = new List<ExtractedEdge>
            {
                new() { Source = "node1", Target = "", Relation = "uses", Confidence = Confidence.Extracted, SourceFile = "test.cs" }
            },
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("empty or null Target", result.Errors[0]);
    }

    [Fact]
    public void Validate_EdgeWithEmptyRelation_ReturnsFailure()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>
            {
                new() { Id = "node1", Label = "Node 1", FileType = FileType.Code, SourceFile = "test.cs" }
            },
            Edges = new List<ExtractedEdge>
            {
                new() { Source = "node1", Target = "node1", Relation = "", Confidence = Confidence.Extracted, SourceFile = "test.cs" }
            },
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("empty or null Relation", result.Errors[0]);
    }

    [Fact]
    public void Validate_EdgeWithEmptySourceFile_ReturnsFailure()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>
            {
                new() { Id = "node1", Label = "Node 1", FileType = FileType.Code, SourceFile = "test.cs" }
            },
            Edges = new List<ExtractedEdge>
            {
                new() { Source = "node1", Target = "node1", Relation = "uses", Confidence = Confidence.Extracted, SourceFile = "" }
            },
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("empty or null SourceFile", result.Errors[0]);
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>
            {
                new() { Id = "", Label = "", FileType = FileType.Code, SourceFile = "" }
            },
            Edges = new List<ExtractedEdge>
            {
                new() { Source = "", Target = "", Relation = "", Confidence = Confidence.Extracted, SourceFile = "" }
            },
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count > 1);
    }

    [Fact]
    public void Validate_NullNodesCollection_ReturnsFailure()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = null!,
            Edges = new List<ExtractedEdge>(),
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Nodes list cannot be null", result.Errors[0]);
    }

    [Fact]
    public void Validate_NullEdgesCollection_ReturnsFailure()
    {
        // Arrange
        var extraction = new ExtractionResult
        {
            Nodes = new List<ExtractedNode>(),
            Edges = null!,
            SourceFilePath = "test.cs"
        };

        // Act
        var result = _validator.Validate(extraction);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Edges list cannot be null", result.Errors[0]);
    }
}
