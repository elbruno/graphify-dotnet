using Graphify.Cli;
using Graphify.Sdk;
using Xunit;

namespace Graphify.Tests.Cli;

public class PipelineRunnerTests
{
    [Fact]
    [Trait("Category", "Cli")]
    public void Constructor_NullChatClient_IsAccepted()
    {
        var runner = new PipelineRunner(TextWriter.Null, verbose: false, chatClient: null);
        Assert.NotNull(runner);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Constructor_WithChatClient_IsAccepted()
    {
        // OllamaApiClient constructor doesn't make network calls
        var client = ChatClientFactory.Create(new AiProviderOptions(Provider: AiProvider.Ollama));
        var runner = new PipelineRunner(TextWriter.Null, verbose: false, chatClient: client);
        Assert.NotNull(runner);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Constructor_NullOutput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PipelineRunner(null!, verbose: false));
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Constructor_DefaultParameters_ChatClientIsOptional()
    {
        var runner = new PipelineRunner(TextWriter.Null);
        Assert.NotNull(runner);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public async Task RunAsync_AstOnlyMode_WritesProgressCounter()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graphify-progress-{Guid.NewGuid():N}");
        var outputDir = Path.Combine(Path.GetTempPath(), $"graphify-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "Sample.cs"), "public class Sample { }");

        var output = new StringWriter();
        var runner = new PipelineRunner(output, verbose: false, chatClient: null);

        try
        {
            await runner.RunAsync(tempDir, outputDir, ["json"], useCache: false, CancellationToken.None);
            var text = output.ToString();
            Assert.Matches(@"\[1/1\]", text);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Cli")]
    public async Task RunAsync_AstOnlyMode_ProgressCounterReachesTotal()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graphify-progress-{Guid.NewGuid():N}");
        var outputDir = Path.Combine(Path.GetTempPath(), $"graphify-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "A.cs"), "public class A { }");
        await File.WriteAllTextAsync(Path.Combine(tempDir, "B.cs"), "public class B { }");

        var output = new StringWriter();
        var runner = new PipelineRunner(output, verbose: false, chatClient: null);

        try
        {
            await runner.RunAsync(tempDir, outputDir, ["json"], useCache: false, CancellationToken.None);
            var text = output.ToString();
            Assert.Matches(@"\[2/2\]", text);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
        }
    }
}
