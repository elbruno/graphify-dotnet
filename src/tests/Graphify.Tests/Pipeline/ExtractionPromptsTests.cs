using Graphify.Pipeline;
using Xunit;

namespace Graphify.Tests.Pipeline;

/// <summary>
/// Tests for ExtractionPrompts: verifies prompt templates include file content,
/// language hints, expected keywords, and structural requirements.
/// </summary>
[Trait("Category", "Pipeline")]
public sealed class ExtractionPromptsTests
{
    [Fact]
    public void CodeSemanticExtraction_IncludesFileContent()
    {
        var prompt = ExtractionPrompts.CodeSemanticExtraction("test.cs", "public class Foo {}");

        Assert.Contains("public class Foo {}", prompt);
    }

    [Fact]
    public void CodeSemanticExtraction_IncludesFileName()
    {
        var prompt = ExtractionPrompts.CodeSemanticExtraction("MyService.cs", "// code");

        Assert.Contains("MyService.cs", prompt);
    }

    [Fact]
    public void CodeSemanticExtraction_IncludesMaxNodes()
    {
        var prompt = ExtractionPrompts.CodeSemanticExtraction("test.cs", "code", maxNodes: 20);

        Assert.Contains("20", prompt);
    }

    [Fact]
    public void CodeSemanticExtraction_IncludesExpectedKeywords()
    {
        var prompt = ExtractionPrompts.CodeSemanticExtraction("test.cs", "code");

        Assert.Contains("design patterns", prompt.ToLowerInvariant());
        Assert.Contains("json", prompt.ToLowerInvariant());
        Assert.Contains("nodes", prompt.ToLowerInvariant());
        Assert.Contains("edges", prompt.ToLowerInvariant());
    }

    [Fact]
    public void DocumentationExtraction_IncludesFileContent()
    {
        var prompt = ExtractionPrompts.DocumentationExtraction("README.md", "# Hello World");

        Assert.Contains("# Hello World", prompt);
    }

    [Fact]
    public void DocumentationExtraction_IncludesExpectedKeywords()
    {
        var prompt = ExtractionPrompts.DocumentationExtraction("doc.md", "content");

        Assert.Contains("concepts", prompt.ToLowerInvariant());
        Assert.Contains("relationships", prompt.ToLowerInvariant());
    }

    [Fact]
    public void ImageVisionExtraction_IncludesFileName()
    {
        var prompt = ExtractionPrompts.ImageVisionExtraction("diagram.png");

        Assert.Contains("diagram.png", prompt);
    }

    [Fact]
    public void ImageVisionExtraction_IncludesMaxNodes()
    {
        var prompt = ExtractionPrompts.ImageVisionExtraction("img.png", maxNodes: 8);

        Assert.Contains("8", prompt);
    }

    [Fact]
    public void PaperExtraction_IncludesExtractedText()
    {
        var prompt = ExtractionPrompts.PaperExtraction("paper.pdf", "This paper proposes...");

        Assert.Contains("This paper proposes...", prompt);
    }

    [Fact]
    public void PaperExtraction_IncludesExpectedKeywords()
    {
        var prompt = ExtractionPrompts.PaperExtraction("paper.pdf", "text");

        Assert.Contains("contributions", prompt.ToLowerInvariant());
        Assert.Contains("relationships", prompt.ToLowerInvariant());
    }

    [Theory]
    [InlineData("test.cs", "code")]
    [InlineData("README.md", "docs")]
    public void AllPrompts_AreNonEmpty(string fileName, string content)
    {
        var codePrompt = ExtractionPrompts.CodeSemanticExtraction(fileName, content);
        var docPrompt = ExtractionPrompts.DocumentationExtraction(fileName, content);
        var imgPrompt = ExtractionPrompts.ImageVisionExtraction(fileName);
        var paperPrompt = ExtractionPrompts.PaperExtraction(fileName, content);

        Assert.NotEmpty(codePrompt);
        Assert.NotEmpty(docPrompt);
        Assert.NotEmpty(imgPrompt);
        Assert.NotEmpty(paperPrompt);
    }

    [Fact]
    public void AllPrompts_RequestJsonOutput()
    {
        var codePrompt = ExtractionPrompts.CodeSemanticExtraction("f.cs", "c");
        var docPrompt = ExtractionPrompts.DocumentationExtraction("f.md", "c");
        var imgPrompt = ExtractionPrompts.ImageVisionExtraction("f.png");
        var paperPrompt = ExtractionPrompts.PaperExtraction("f.pdf", "c");

        Assert.Contains("JSON", codePrompt);
        Assert.Contains("JSON", docPrompt);
        Assert.Contains("JSON", imgPrompt);
        Assert.Contains("JSON", paperPrompt);
    }
}
