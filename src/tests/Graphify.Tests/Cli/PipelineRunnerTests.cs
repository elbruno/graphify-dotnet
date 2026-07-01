using System.Text;
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
    public async Task RunAsync_LadybugFormat_RoutesToLadybugExporter()
    {
        // Arrange: empty input dir - pipeline completes with zero nodes and exports DDL-only script
        var tempInput = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var tempOutput = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempInput);
        try
        {
            var sb = new StringBuilder();
            await using var writer = new StringWriter(sb);
            var runner = new PipelineRunner(writer, verbose: false);

            await runner.RunAsync(tempInput, tempOutput, formats: ["ladybug"], useCache: false);

            // Pipeline log should mention the Ladybug export
            var log = sb.ToString();
            Assert.Contains("Ladybug", log, StringComparison.OrdinalIgnoreCase);

            // Output file must exist and be non-empty (DDL is always emitted)
            var ladybugFile = Path.Combine(tempOutput, "graph.ladybug.cypher");
            Assert.True(File.Exists(ladybugFile), "graph.ladybug.cypher should be created for --format ladybug");
            Assert.True(new FileInfo(ladybugFile).Length > 0, "Ladybug output file should not be empty");
        }
        finally
        {
            try { Directory.Delete(tempInput, recursive: true); } catch { /* best-effort cleanup */ }
            try { if (Directory.Exists(tempOutput)) Directory.Delete(tempOutput, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    [Trait("Category", "Cli")]
    public async Task RunAsync_ReportsFileProgressDuringExtraction()
    {
        var tempInput = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var tempOutput = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempInput);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempInput, "alpha.cs"), "namespace Demo; public sealed class Alpha { }");
            await File.WriteAllTextAsync(Path.Combine(tempInput, "beta.cs"), "namespace Demo; public sealed class Beta { }");

            var sb = new StringBuilder();
            await using var writer = new StringWriter(sb);
            var runner = new PipelineRunner(writer, verbose: false);

            await runner.RunAsync(tempInput, tempOutput, formats: ["json"], useCache: false);

            var log = sb.ToString();
            Assert.Contains("Progress:", log, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("2/2", log, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(tempInput, recursive: true); } catch { /* best-effort cleanup */ }
            try { if (Directory.Exists(tempOutput)) Directory.Delete(tempOutput, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }
}
