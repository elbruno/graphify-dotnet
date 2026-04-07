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
}
