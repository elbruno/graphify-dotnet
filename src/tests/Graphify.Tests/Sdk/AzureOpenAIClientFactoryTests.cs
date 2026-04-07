using Xunit;

namespace Graphify.Tests.Sdk;

/// <summary>
/// Tests for AzureOpenAIClientFactory and AzureOpenAIOptions.
/// Factory tests are commented out until implementation lands in Graphify.Sdk.
/// Record contract tests use inline definitions to validate the expected API shape.
/// </summary>
public class AzureOpenAIClientFactoryTests
{
    // ──────────────────────────────────────────────
    // AzureOpenAIOptions record contract tests
    // Uses inline record to validate expected shape.
    // Replace with `using Graphify.Sdk;` when real type lands.
    // ──────────────────────────────────────────────

    /// <summary>
    /// Expected shape of the AzureOpenAIOptions record.
    /// Remove this and use the real type from Graphify.Sdk once implemented.
    /// </summary>
    private record AzureOpenAIOptions(
        string Endpoint = "",
        string ApiKey = "",
        string DeploymentName = "gpt-4o");

    [Fact]
    [Trait("Category", "Sdk")]
    public void AzureOpenAIOptions_DefaultConstruction_HasEmptyStrings()
    {
        var options = new AzureOpenAIOptions();

        Assert.Equal("", options.Endpoint);
        Assert.Equal("", options.ApiKey);
        Assert.Equal("gpt-4o", options.DeploymentName);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void AzureOpenAIOptions_CustomValues_ArePreserved()
    {
        var options = new AzureOpenAIOptions(
            Endpoint: "https://myinstance.openai.azure.com/",
            ApiKey: "my-secret-key",
            DeploymentName: "gpt-4o-mini");

        Assert.Equal("https://myinstance.openai.azure.com/", options.Endpoint);
        Assert.Equal("my-secret-key", options.ApiKey);
        Assert.Equal("gpt-4o-mini", options.DeploymentName);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void AzureOpenAIOptions_RecordEquality_WorksCorrectly()
    {
        var a = new AzureOpenAIOptions(Endpoint: "https://e.com", ApiKey: "k", DeploymentName: "d");
        var b = new AzureOpenAIOptions(Endpoint: "https://e.com", ApiKey: "k", DeploymentName: "d");

        Assert.Equal(a, b);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void AzureOpenAIOptions_With_CreatesModifiedCopy()
    {
        var original = new AzureOpenAIOptions(Endpoint: "https://e.com", ApiKey: "key1", DeploymentName: "gpt-4o");
        var modified = original with { ApiKey = "key2" };

        Assert.Equal("key1", original.ApiKey);
        Assert.Equal("key2", modified.ApiKey);
        Assert.Equal(original.Endpoint, modified.Endpoint);
    }

    // ──────────────────────────────────────────────
    // AzureOpenAIClientFactory.Create tests
    // TODO: Uncomment when AzureOpenAIClientFactory lands in Graphify.Sdk
    // ──────────────────────────────────────────────

    // [Fact]
    // [Trait("Category", "Sdk")]
    // public void Create_NullOptions_ThrowsArgumentNullException()
    // {
    //     Assert.Throws<ArgumentNullException>(() =>
    //         AzureOpenAIClientFactory.Create(null!));
    // }

    // [Fact]
    // [Trait("Category", "Sdk")]
    // public void Create_EmptyApiKey_ThrowsArgumentException()
    // {
    //     var options = new Graphify.Sdk.AzureOpenAIOptions(
    //         Endpoint: "https://myinstance.openai.azure.com/",
    //         ApiKey: "",
    //         DeploymentName: "gpt-4o");
    //
    //     Assert.Throws<ArgumentException>(() =>
    //         AzureOpenAIClientFactory.Create(options));
    // }

    // [Fact]
    // [Trait("Category", "Sdk")]
    // public void Create_EmptyEndpoint_ThrowsArgumentException()
    // {
    //     var options = new Graphify.Sdk.AzureOpenAIOptions(
    //         Endpoint: "",
    //         ApiKey: "my-key",
    //         DeploymentName: "gpt-4o");
    //
    //     Assert.Throws<ArgumentException>(() =>
    //         AzureOpenAIClientFactory.Create(options));
    // }

    // [Fact]
    // [Trait("Category", "Sdk")]
    // public void Create_EmptyDeploymentName_ThrowsArgumentException()
    // {
    //     var options = new Graphify.Sdk.AzureOpenAIOptions(
    //         Endpoint: "https://myinstance.openai.azure.com/",
    //         ApiKey: "my-key",
    //         DeploymentName: "");
    //
    //     Assert.Throws<ArgumentException>(() =>
    //         AzureOpenAIClientFactory.Create(options));
    // }
}
