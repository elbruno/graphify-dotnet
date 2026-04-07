using Xunit;

namespace Graphify.Tests.Sdk;

/// <summary>
/// Tests for OllamaClientFactory and OllamaOptions.
/// Factory tests are commented out until implementation lands in Graphify.Sdk.
/// Record contract tests use inline definitions to validate the expected API shape.
/// </summary>
public class OllamaClientFactoryTests
{
    // ──────────────────────────────────────────────
    // OllamaOptions record contract tests
    // Uses inline record to validate expected shape.
    // Replace with `using Graphify.Sdk;` when real type lands.
    // ──────────────────────────────────────────────

    /// <summary>
    /// Expected shape of the OllamaOptions record.
    /// Remove this and use the real type from Graphify.Sdk once implemented.
    /// </summary>
    private record OllamaOptions(
        string Endpoint = "http://localhost:11434",
        string ModelId = "llama3.2");

    [Fact]
    [Trait("Category", "Sdk")]
    public void OllamaOptions_DefaultValues_AreCorrect()
    {
        var options = new OllamaOptions();

        Assert.Equal("http://localhost:11434", options.Endpoint);
        Assert.Equal("llama3.2", options.ModelId);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void OllamaOptions_CustomEndpoint_IsPreserved()
    {
        var options = new OllamaOptions(Endpoint: "http://gpu-server:11434");

        Assert.Equal("http://gpu-server:11434", options.Endpoint);
        Assert.Equal("llama3.2", options.ModelId);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void OllamaOptions_CustomModelId_IsPreserved()
    {
        var options = new OllamaOptions(ModelId: "codellama:13b");

        Assert.Equal("http://localhost:11434", options.Endpoint);
        Assert.Equal("codellama:13b", options.ModelId);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void OllamaOptions_FullyCustom_AllValuesPreserved()
    {
        var options = new OllamaOptions(
            Endpoint: "http://remote:8080",
            ModelId: "mistral:7b");

        Assert.Equal("http://remote:8080", options.Endpoint);
        Assert.Equal("mistral:7b", options.ModelId);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void OllamaOptions_RecordEquality_WorksCorrectly()
    {
        var a = new OllamaOptions(Endpoint: "http://localhost:11434", ModelId: "llama3.2");
        var b = new OllamaOptions(Endpoint: "http://localhost:11434", ModelId: "llama3.2");

        Assert.Equal(a, b);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void OllamaOptions_With_CreatesModifiedCopy()
    {
        var original = new OllamaOptions();
        var modified = original with { ModelId = "phi3:mini" };

        Assert.Equal("llama3.2", original.ModelId);
        Assert.Equal("phi3:mini", modified.ModelId);
        Assert.Equal(original.Endpoint, modified.Endpoint);
    }

    // ──────────────────────────────────────────────
    // OllamaClientFactory.Create tests
    // TODO: Uncomment when OllamaClientFactory lands in Graphify.Sdk
    // ──────────────────────────────────────────────

    // [Fact]
    // [Trait("Category", "Sdk")]
    // public void Create_DefaultOptions_ReturnsIChatClient()
    // {
    //     var options = new Graphify.Sdk.OllamaOptions();
    //     var client = OllamaClientFactory.Create(options);
    //
    //     Assert.NotNull(client);
    //     Assert.IsAssignableFrom<Microsoft.Extensions.AI.IChatClient>(client);
    // }

    // [Fact]
    // [Trait("Category", "Sdk")]
    // public void Create_NullEndpoint_ThrowsArgumentException()
    // {
    //     var options = new Graphify.Sdk.OllamaOptions(
    //         Endpoint: null!,
    //         ModelId: "llama3.2");
    //
    //     Assert.Throws<ArgumentException>(() =>
    //         OllamaClientFactory.Create(options));
    // }

    // [Fact]
    // [Trait("Category", "Sdk")]
    // public void Create_NullOptions_ThrowsArgumentNullException()
    // {
    //     Assert.Throws<ArgumentNullException>(() =>
    //         OllamaClientFactory.Create(null!));
    // }
}
