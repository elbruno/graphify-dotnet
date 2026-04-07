using Graphify.Sdk;
using Xunit;

namespace Graphify.Tests.Sdk;

/// <summary>
/// Tests for CopilotExtractorOptions: default values, full construction,
/// and property validation.
/// </summary>
[Trait("Category", "SDK")]
public sealed class CopilotExtractorOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new CopilotExtractorOptions();

        Assert.Null(options.ApiKey);
        Assert.Equal("gpt-4o", options.ModelId);
        Assert.Equal("", options.Endpoint);
        Assert.Equal(4096, options.MaxTokens);
        Assert.Equal(0.1f, options.Temperature);
        Assert.True(options.ExtractFromCode);
        Assert.True(options.ExtractFromDocs);
        Assert.True(options.ExtractFromMedia);
        Assert.Equal(15, options.MaxNodesPerFile);
        Assert.Equal(1024 * 1024, options.MaxFileSizeBytes);
    }

    [Fact]
    public void Construction_WithAllParameters()
    {
        var options = new CopilotExtractorOptions
        {
            ApiKey = "my-key",
            ModelId = "gpt-4o-mini",
            Endpoint = "https://custom.endpoint.com",
            MaxTokens = 8192,
            Temperature = 0.5f,
            ExtractFromCode = false,
            ExtractFromDocs = false,
            ExtractFromMedia = false,
            MaxNodesPerFile = 25,
            MaxFileSizeBytes = 2 * 1024 * 1024
        };

        Assert.Equal("my-key", options.ApiKey);
        Assert.Equal("gpt-4o-mini", options.ModelId);
        Assert.Equal("https://custom.endpoint.com", options.Endpoint);
        Assert.Equal(8192, options.MaxTokens);
        Assert.Equal(0.5f, options.Temperature);
        Assert.False(options.ExtractFromCode);
        Assert.False(options.ExtractFromDocs);
        Assert.False(options.ExtractFromMedia);
        Assert.Equal(25, options.MaxNodesPerFile);
        Assert.Equal(2 * 1024 * 1024, options.MaxFileSizeBytes);
    }

    [Fact]
    public void ApiKey_CanBeSetAndRetrieved()
    {
        var options = new CopilotExtractorOptions();
        options.ApiKey = "test-key";

        Assert.Equal("test-key", options.ApiKey);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(0.5f)]
    [InlineData(1.0f)]
    public void Temperature_AcceptsValidRange(float temp)
    {
        var options = new CopilotExtractorOptions { Temperature = temp };

        Assert.Equal(temp, options.Temperature);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(100)]
    public void MaxNodesPerFile_AcceptsPositiveValues(int maxNodes)
    {
        var options = new CopilotExtractorOptions { MaxNodesPerFile = maxNodes };

        Assert.Equal(maxNodes, options.MaxNodesPerFile);
    }
}
