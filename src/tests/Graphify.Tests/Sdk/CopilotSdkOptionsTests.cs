using Graphify.Sdk;
using Xunit;

namespace Graphify.Tests.Sdk;

/// <summary>
/// Tests for CopilotSdkOptions record: default values, custom construction,
/// record equality, and with-expression semantics.
/// </summary>
public class CopilotSdkOptionsTests
{
    [Fact]
    [Trait("Category", "Sdk")]
    public void DefaultModelId_IsGpt41()
    {
        var options = new CopilotSdkOptions();

        Assert.Equal("gpt-4.1", options.ModelId);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void CustomModelId_IsPreserved()
    {
        var options = new CopilotSdkOptions(ModelId: "gpt-4o");

        Assert.Equal("gpt-4o", options.ModelId);
    }

    [Theory]
    [InlineData("claude-sonnet")]
    [InlineData("gpt-4o-mini")]
    [InlineData("llama3.2")]
    [Trait("Category", "Sdk")]
    public void CustomModelId_VariousModels_ArePreserved(string modelId)
    {
        var options = new CopilotSdkOptions(ModelId: modelId);

        Assert.Equal(modelId, options.ModelId);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new CopilotSdkOptions(ModelId: "gpt-4.1");
        var b = new CopilotSdkOptions(ModelId: "gpt-4.1");

        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void RecordEquality_DefaultInstances_AreEqual()
    {
        var a = new CopilotSdkOptions();
        var b = new CopilotSdkOptions();

        Assert.Equal(a, b);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var a = new CopilotSdkOptions(ModelId: "gpt-4.1");
        var b = new CopilotSdkOptions(ModelId: "gpt-4o");

        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void WithExpression_CreatesModifiedCopy()
    {
        var original = new CopilotSdkOptions(ModelId: "gpt-4.1");
        var modified = original with { ModelId = "claude-sonnet" };

        Assert.Equal("gpt-4.1", original.ModelId);
        Assert.Equal("claude-sonnet", modified.ModelId);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void WithExpression_SameValue_ProducesEqualRecord()
    {
        var original = new CopilotSdkOptions(ModelId: "gpt-4.1");
        var copy = original with { ModelId = "gpt-4.1" };

        Assert.Equal(original, copy);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void ToString_ContainsModelId()
    {
        var options = new CopilotSdkOptions(ModelId: "gpt-4.1");

        var str = options.ToString();
        Assert.Contains("gpt-4.1", str);
    }

    [Fact]
    [Trait("Category", "Sdk")]
    public void GetHashCode_EqualRecords_HaveSameHashCode()
    {
        var a = new CopilotSdkOptions(ModelId: "gpt-4o");
        var b = new CopilotSdkOptions(ModelId: "gpt-4o");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
