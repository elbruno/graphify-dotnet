using Graphify.Cli.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Graphify.Tests.Cli;

public class ConfigurationFactoryTests
{
    [Fact]
    [Trait("Category", "Cli")]
    public void Build_NoArguments_ReturnsNonNullConfiguration()
    {
        var config = ConfigurationFactory.Build();
        Assert.NotNull(config);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_NullCliOptions_ReturnsNonNullConfiguration()
    {
        var config = ConfigurationFactory.Build(cliOptions: null);
        Assert.NotNull(config);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_WithProviderOverride_SetsProviderValue()
    {
        var cliOptions = new CliProviderOptions(
            Provider: "ollama",
            Endpoint: null,
            ApiKey: null,
            Model: null,
            Deployment: null);

        var config = ConfigurationFactory.Build(cliOptions);

        Assert.Equal("ollama", config["Graphify:Provider"]);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_OllamaCliOptions_SetsOllamaEndpointAndModel()
    {
        var cliOptions = new CliProviderOptions(
            Provider: "ollama",
            Endpoint: "http://remote:11434",
            ApiKey: null,
            Model: "codellama",
            Deployment: null);

        var config = ConfigurationFactory.Build(cliOptions);

        Assert.Equal("http://remote:11434", config["Graphify:Ollama:Endpoint"]);
        Assert.Equal("codellama", config["Graphify:Ollama:ModelId"]);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_AzureOpenAICliOptions_SetsAzureFields()
    {
        var cliOptions = new CliProviderOptions(
            Provider: "azureopenai",
            Endpoint: "https://myinstance.openai.azure.com/",
            ApiKey: "my-key",
            Model: "gpt-4o",
            Deployment: "my-deployment");

        var config = ConfigurationFactory.Build(cliOptions);

        Assert.Equal("azureopenai", config["Graphify:Provider"]);
        Assert.Equal("https://myinstance.openai.azure.com/", config["Graphify:AzureOpenAI:Endpoint"]);
        Assert.Equal("my-key", config["Graphify:AzureOpenAI:ApiKey"]);
        Assert.Equal("gpt-4o", config["Graphify:AzureOpenAI:ModelId"]);
        Assert.Equal("my-deployment", config["Graphify:AzureOpenAI:DeploymentName"]);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_CliOverridesAreHighestPriority()
    {
        // Even if appsettings.json has defaults, CLI should override
        var cliOptions = new CliProviderOptions(
            Provider: "ollama",
            Endpoint: "http://custom:9999",
            ApiKey: null,
            Model: "custom-model",
            Deployment: null);

        var config = ConfigurationFactory.Build(cliOptions);

        // CLI values win
        Assert.Equal("http://custom:9999", config["Graphify:Ollama:Endpoint"]);
        Assert.Equal("custom-model", config["Graphify:Ollama:ModelId"]);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_PartialCliOptions_OnlySetsProvidedValues()
    {
        // Only provider is set; other fields remain null in overrides
        var cliOptions = new CliProviderOptions(
            Provider: "ollama",
            Endpoint: null,
            ApiKey: null,
            Model: null,
            Deployment: null);

        var config = ConfigurationFactory.Build(cliOptions);

        Assert.Equal("ollama", config["Graphify:Provider"]);
        // Endpoint should come from appsettings.json if present, or be null
        // We don't assert a specific value here since appsettings.json may not be in test output
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_CanBindToGraphifyConfig()
    {
        var cliOptions = new CliProviderOptions(
            Provider: "ollama",
            Endpoint: "http://localhost:11434",
            ApiKey: null,
            Model: "llama3.2",
            Deployment: null);

        var configuration = ConfigurationFactory.Build(cliOptions);
        var graphifyConfig = new GraphifyConfig();
        configuration.GetSection("Graphify").Bind(graphifyConfig);

        Assert.Equal("ollama", graphifyConfig.Provider);
        Assert.Equal("http://localhost:11434", graphifyConfig.Ollama.Endpoint);
        Assert.Equal("llama3.2", graphifyConfig.Ollama.ModelId);
    }
}
