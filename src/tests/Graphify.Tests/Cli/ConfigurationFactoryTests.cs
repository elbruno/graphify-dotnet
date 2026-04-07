using System.Text.Json;
using Graphify.Cli.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Graphify.Tests.Cli;

[Collection("ConfigFile")]
public class ConfigurationFactoryTests : IDisposable
{
    private readonly string _localConfigPath;
    private readonly string? _backupPath;

    public ConfigurationFactoryTests()
    {
        _localConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");

        if (File.Exists(_localConfigPath))
        {
            _backupPath = _localConfigPath + ".test-backup";
            File.Copy(_localConfigPath, _backupPath, overwrite: true);
            File.Delete(_localConfigPath);
        }
    }

    public void Dispose()
    {
        try { if (File.Exists(_localConfigPath)) File.Delete(_localConfigPath); } catch { }

        if (_backupPath != null && File.Exists(_backupPath))
        {
            try
            {
                File.Copy(_backupPath, _localConfigPath, overwrite: true);
                File.Delete(_backupPath);
            }
            catch { }
        }
    }
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

    // ── Local config file loading ──────────────────────────────────────

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_LoadsLocalConfigFile_WhenPresent()
    {
        // Write a local config with known values
        var localConfig = new Dictionary<string, object>
        {
            ["Graphify"] = new Dictionary<string, object>
            {
                ["Provider"] = "ollama",
                ["Ollama"] = new Dictionary<string, object>
                {
                    ["Endpoint"] = "http://local-test:11434",
                    ["ModelId"] = "test-model"
                }
            }
        };
        File.WriteAllText(_localConfigPath, JsonSerializer.Serialize(localConfig));

        var config = ConfigurationFactory.Build();

        Assert.Equal("ollama", config["Graphify:Provider"]);
        Assert.Equal("http://local-test:11434", config["Graphify:Ollama:Endpoint"]);
        Assert.Equal("test-model", config["Graphify:Ollama:ModelId"]);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_CliArgs_OverrideLocalConfig()
    {
        // Local config says ollama with specific endpoint
        var localConfig = new Dictionary<string, object>
        {
            ["Graphify"] = new Dictionary<string, object>
            {
                ["Provider"] = "ollama",
                ["Ollama"] = new Dictionary<string, object>
                {
                    ["Endpoint"] = "http://local-file:11434",
                    ["ModelId"] = "file-model"
                }
            }
        };
        File.WriteAllText(_localConfigPath, JsonSerializer.Serialize(localConfig));

        // CLI args override the provider and model
        var cliOptions = new CliProviderOptions(
            Provider: "ollama",
            Endpoint: "http://cli-override:11434",
            ApiKey: null,
            Model: "cli-model",
            Deployment: null);

        var config = ConfigurationFactory.Build(cliOptions);

        // CLI values win over local file values
        Assert.Equal("http://cli-override:11434", config["Graphify:Ollama:Endpoint"]);
        Assert.Equal("cli-model", config["Graphify:Ollama:ModelId"]);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_LocalConfig_BindsToGraphifyConfig()
    {
        var localConfig = new Dictionary<string, object>
        {
            ["Graphify"] = new Dictionary<string, object>
            {
                ["Provider"] = "azureopenai",
                ["AzureOpenAI"] = new Dictionary<string, object>
                {
                    ["Endpoint"] = "https://bind-test.openai.azure.com/",
                    ["ApiKey"] = "bind-test-key",
                    ["DeploymentName"] = "bind-deploy",
                    ["ModelId"] = "gpt-4o"
                }
            }
        };
        File.WriteAllText(_localConfigPath, JsonSerializer.Serialize(localConfig));

        var configuration = ConfigurationFactory.Build();
        var graphifyConfig = new GraphifyConfig();
        configuration.GetSection("Graphify").Bind(graphifyConfig);

        Assert.Equal("azureopenai", graphifyConfig.Provider);
        Assert.Equal("https://bind-test.openai.azure.com/", graphifyConfig.AzureOpenAI.Endpoint);
        Assert.Equal("bind-test-key", graphifyConfig.AzureOpenAI.ApiKey);
        Assert.Equal("bind-deploy", graphifyConfig.AzureOpenAI.DeploymentName);
    }

    // ── CopilotSdk CLI override tests ──────────────────────────────────

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_CopilotSdkCliOptions_SetsModelIdCorrectly()
    {
        var cliOptions = new CliProviderOptions(
            Provider: "copilotsdk",
            Endpoint: null,
            ApiKey: null,
            Model: "gpt-4o",
            Deployment: null);

        var config = ConfigurationFactory.Build(cliOptions);

        Assert.Equal("gpt-4o", config["Graphify:CopilotSdk:ModelId"]);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_CopilotSdkCliOptions_DoesNotSetAzureModel()
    {
        var cliOptions = new CliProviderOptions(
            Provider: "copilotsdk",
            Endpoint: null,
            ApiKey: null,
            Model: "gpt-4o",
            Deployment: null);

        var config = ConfigurationFactory.Build(cliOptions);

        // Model should NOT bleed into Azure OpenAI config
        Assert.Null(config["Graphify:AzureOpenAI:ModelId"]);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_CopilotSdkCliOptions_IgnoresEndpoint()
    {
        var cliOptions = new CliProviderOptions(
            Provider: "copilotsdk",
            Endpoint: "http://something",
            ApiKey: null,
            Model: null,
            Deployment: null);

        var config = ConfigurationFactory.Build(cliOptions);

        // CopilotSdk has no endpoint config — the CLI endpoint must be silently dropped
        Assert.Null(config["Graphify:CopilotSdk:Endpoint"]);
        // Endpoint must NOT bleed into AzureOpenAI either
        Assert.Null(config["Graphify:AzureOpenAI:Endpoint"]);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_CopilotSdkCanBindToGraphifyConfig()
    {
        var cliOptions = new CliProviderOptions(
            Provider: "copilotsdk",
            Endpoint: null,
            ApiKey: null,
            Model: "claude-sonnet",
            Deployment: null);

        var configuration = ConfigurationFactory.Build(cliOptions);
        var graphifyConfig = new GraphifyConfig();
        configuration.GetSection("Graphify").Bind(graphifyConfig);

        Assert.Equal("copilotsdk", graphifyConfig.Provider);
        Assert.Equal("claude-sonnet", graphifyConfig.CopilotSdk.ModelId);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_CopilotSdkCliOptions_ApiKeyDoesNotSetCopilotSdkKey()
    {
        var cliOptions = new CliProviderOptions(
            Provider: "copilotsdk",
            Endpoint: null,
            ApiKey: "stray-api-key",
            Model: null,
            Deployment: null);

        var config = ConfigurationFactory.Build(cliOptions);

        Assert.Null(config["Graphify:CopilotSdk:ApiKey"]);
    }

    [Fact]
    [Trait("Category", "Cli")]
    public void Build_CopilotSdkCliOptions_DeploymentIsIgnored()
    {
        var cliOptions = new CliProviderOptions(
            Provider: "copilotsdk",
            Endpoint: null,
            ApiKey: null,
            Model: "gpt-4o",
            Deployment: "my-deploy");

        var config = ConfigurationFactory.Build(cliOptions);

        Assert.Equal("gpt-4o", config["Graphify:CopilotSdk:ModelId"]);
        Assert.Null(config["Graphify:AzureOpenAI:ModelId"]);
    }
}
