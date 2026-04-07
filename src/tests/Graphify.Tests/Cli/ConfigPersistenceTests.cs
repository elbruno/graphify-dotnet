using System.Text.Json;
using Graphify.Cli.Configuration;
using Xunit;

namespace Graphify.Tests.Cli;

/// <summary>
/// Tests for ConfigPersistence save/load round-trips and edge cases.
/// Uses [Collection] to prevent parallel execution since all tests share
/// the same appsettings.local.json file path (AppContext.BaseDirectory).
/// </summary>
[Collection("ConfigFile")]
[Trait("Category", "Cli")]
public class ConfigPersistenceTests : IDisposable
{
    private readonly string _configPath;
    private readonly string? _backupPath;

    public ConfigPersistenceTests()
    {
        _configPath = ConfigPersistence.GetLocalConfigPath();

        // Back up any existing file so tests don't destroy real config
        if (File.Exists(_configPath))
        {
            _backupPath = _configPath + ".test-backup";
            File.Copy(_configPath, _backupPath, overwrite: true);
            File.Delete(_configPath);
        }
    }

    public void Dispose()
    {
        // Remove test artifact
        try { if (File.Exists(_configPath)) File.Delete(_configPath); } catch { }

        // Restore original if it existed
        if (_backupPath != null && File.Exists(_backupPath))
        {
            try
            {
                File.Copy(_backupPath, _configPath, overwrite: true);
                File.Delete(_backupPath);
            }
            catch { }
        }
    }

    // ── Path ──────────────────────────────────────────────────────────

    [Fact]
    public void GetLocalConfigPath_ContainsExpectedFileName()
    {
        var path = ConfigPersistence.GetLocalConfigPath();

        Assert.EndsWith("appsettings.local.json", path);
        Assert.True(Path.IsPathRooted(path), "Path should be absolute");
    }

    // ── Round-trip: Azure OpenAI ──────────────────────────────────────

    [Fact]
    public void Save_Load_RoundTrip_AzureOpenAI()
    {
        var original = new GraphifyConfig
        {
            Provider = "azureopenai",
            AzureOpenAI = new AzureOpenAIConfig
            {
                Endpoint = "https://myinstance.openai.azure.com/",
                ApiKey = "sk-test-key-12345",
                DeploymentName = "gpt-4o-deploy",
                ModelId = "gpt-4o"
            }
        };

        ConfigPersistence.Save(original);
        var loaded = ConfigPersistence.Load();

        Assert.NotNull(loaded);
        Assert.Equal("azureopenai", loaded.Provider);
        Assert.Equal("https://myinstance.openai.azure.com/", loaded.AzureOpenAI.Endpoint);
        // API key is no longer stored in the JSON file (FINDING-001 security fix)
        Assert.Null(loaded.AzureOpenAI.ApiKey);
        Assert.Equal("gpt-4o-deploy", loaded.AzureOpenAI.DeploymentName);
        Assert.Equal("gpt-4o", loaded.AzureOpenAI.ModelId);
    }

    // ── Round-trip: Ollama ─────────────────────────────────────────────

    [Fact]
    public void Save_Load_RoundTrip_Ollama()
    {
        var original = new GraphifyConfig
        {
            Provider = "ollama",
            Ollama = new OllamaConfig
            {
                Endpoint = "http://remote-server:11434",
                ModelId = "codellama"
            }
        };

        ConfigPersistence.Save(original);
        var loaded = ConfigPersistence.Load();

        Assert.NotNull(loaded);
        Assert.Equal("ollama", loaded.Provider);
        Assert.Equal("http://remote-server:11434", loaded.Ollama.Endpoint);
        Assert.Equal("codellama", loaded.Ollama.ModelId);
    }

    // ── Round-trip: CopilotSdk ────────────────────────────────────────

    [Fact]
    public void Save_Load_RoundTrip_CopilotSdk()
    {
        var original = new GraphifyConfig
        {
            Provider = "copilotsdk",
            CopilotSdk = new CopilotSdkConfig
            {
                ModelId = "gpt-4.1"
            }
        };

        ConfigPersistence.Save(original);
        var loaded = ConfigPersistence.Load();

        Assert.NotNull(loaded);
        Assert.Equal("copilotsdk", loaded.Provider);
        Assert.Equal("gpt-4.1", loaded.CopilotSdk.ModelId);
    }

    // ── Round-trip: Null provider (AST-only) ──────────────────────────

    [Fact]
    public void Save_Load_RoundTrip_NullProvider_AstOnlyMode()
    {
        var original = new GraphifyConfig { Provider = null };

        ConfigPersistence.Save(original);
        var loaded = ConfigPersistence.Load();

        // Provider is null and WhenWritingNull suppresses it in JSON,
        // so the Graphify section may be empty. Load should still return
        // a valid config (or null if the section is empty).
        // The actual behavior: Graphify section exists but is empty dict → deserializes to default GraphifyConfig
        if (loaded != null)
        {
            Assert.Null(loaded.Provider);
        }
        // Either null or a config with null provider is acceptable
    }

    // ── Load: file does not exist ─────────────────────────────────────

    [Fact]
    public void Load_ReturnsNull_WhenFileDoesNotExist()
    {
        // Ensure file doesn't exist (constructor already deleted it if present)
        Assert.False(File.Exists(_configPath), "Pre-condition: file should not exist");

        var result = ConfigPersistence.Load();

        Assert.Null(result);
    }

    // ── Save produces valid JSON with "Graphify" wrapper ──────────────

    [Fact]
    public void Save_CreatesValidJson_WithGraphifyWrapper()
    {
        var config = new GraphifyConfig
        {
            Provider = "ollama",
            Ollama = new OllamaConfig
            {
                Endpoint = "http://localhost:11434",
                ModelId = "llama3.2"
            }
        };

        ConfigPersistence.Save(config);

        Assert.True(File.Exists(_configPath), "Save should create the file");

        var json = File.ReadAllText(_configPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Must have a "Graphify" top-level section
        Assert.True(root.TryGetProperty("Graphify", out var graphifySection),
            "JSON must contain a 'Graphify' top-level key");

        // Provider should be under Graphify
        Assert.True(graphifySection.TryGetProperty("Provider", out var providerProp));
        Assert.Equal("ollama", providerProp.GetString());
    }

    // ── Provider-specific fields only ─────────────────────────────────

    [Fact]
    public void Save_AzureOpenAI_DoesNotIncludeOllamaOrCopilotSdkFields()
    {
        var config = new GraphifyConfig
        {
            Provider = "azureopenai",
            AzureOpenAI = new AzureOpenAIConfig
            {
                Endpoint = "https://test.openai.azure.com/",
                ApiKey = "key123",
                DeploymentName = "deploy1",
                ModelId = "gpt-4o"
            }
        };

        ConfigPersistence.Save(config);

        var json = File.ReadAllText(_configPath);
        using var doc = JsonDocument.Parse(json);
        var graphify = doc.RootElement.GetProperty("Graphify");

        Assert.True(graphify.TryGetProperty("AzureOpenAI", out _),
            "Azure config should have AzureOpenAI section");
        Assert.False(graphify.TryGetProperty("Ollama", out _),
            "Azure config should NOT have Ollama section");
        Assert.False(graphify.TryGetProperty("CopilotSdk", out _),
            "Azure config should NOT have CopilotSdk section");
    }

    [Fact]
    public void Save_Ollama_DoesNotIncludeAzureOrCopilotSdkFields()
    {
        var config = new GraphifyConfig
        {
            Provider = "ollama",
            Ollama = new OllamaConfig
            {
                Endpoint = "http://localhost:11434",
                ModelId = "llama3.2"
            }
        };

        ConfigPersistence.Save(config);

        var json = File.ReadAllText(_configPath);
        using var doc = JsonDocument.Parse(json);
        var graphify = doc.RootElement.GetProperty("Graphify");

        Assert.True(graphify.TryGetProperty("Ollama", out _),
            "Ollama config should have Ollama section");
        Assert.False(graphify.TryGetProperty("AzureOpenAI", out _),
            "Ollama config should NOT have AzureOpenAI section");
        Assert.False(graphify.TryGetProperty("CopilotSdk", out _),
            "Ollama config should NOT have CopilotSdk section");
    }

    [Fact]
    public void Save_CopilotSdk_DoesNotIncludeAzureOrOllamaFields()
    {
        var config = new GraphifyConfig
        {
            Provider = "copilotsdk",
            CopilotSdk = new CopilotSdkConfig { ModelId = "gpt-4.1" }
        };

        ConfigPersistence.Save(config);

        var json = File.ReadAllText(_configPath);
        using var doc = JsonDocument.Parse(json);
        var graphify = doc.RootElement.GetProperty("Graphify");

        Assert.True(graphify.TryGetProperty("CopilotSdk", out _),
            "CopilotSdk config should have CopilotSdk section");
        Assert.False(graphify.TryGetProperty("AzureOpenAI", out _),
            "CopilotSdk config should NOT have AzureOpenAI section");
        Assert.False(graphify.TryGetProperty("Ollama", out _),
            "CopilotSdk config should NOT have Ollama section");
    }

    // ── Malformed JSON handling ───────────────────────────────────────

    [Fact]
    public void Load_ReturnsNull_ForMalformedJson()
    {
        File.WriteAllText(_configPath, "{ this is not valid json!!!");

        var result = ConfigPersistence.Load();

        Assert.Null(result);
    }

    // ── Missing "Graphify" section ────────────────────────────────────

    [Fact]
    public void Load_ReturnsNull_WhenGraphifySectionMissing()
    {
        File.WriteAllText(_configPath, """{ "SomeOtherSection": { "Key": "Value" } }""");

        var result = ConfigPersistence.Load();

        Assert.Null(result);
    }

    // ── Empty Graphify section ────────────────────────────────────────

    [Fact]
    public void Load_ReturnsConfig_WhenGraphifySectionIsEmpty()
    {
        File.WriteAllText(_configPath, """{ "Graphify": { } }""");

        var result = ConfigPersistence.Load();

        // Empty section should still deserialize to a default GraphifyConfig
        Assert.NotNull(result);
        Assert.Null(result.Provider);
    }

    // ── Overwrite existing file ───────────────────────────────────────

    [Fact]
    public void Save_OverwritesPreviousFile()
    {
        var first = new GraphifyConfig
        {
            Provider = "ollama",
            Ollama = new OllamaConfig { ModelId = "first-model" }
        };
        ConfigPersistence.Save(first);

        var second = new GraphifyConfig
        {
            Provider = "azureopenai",
            AzureOpenAI = new AzureOpenAIConfig
            {
                Endpoint = "https://second.openai.azure.com/",
                ApiKey = "second-key",
                DeploymentName = "deploy",
                ModelId = "gpt-4o"
            }
        };
        ConfigPersistence.Save(second);

        var loaded = ConfigPersistence.Load();

        Assert.NotNull(loaded);
        Assert.Equal("azureopenai", loaded.Provider);
        Assert.Equal("https://second.openai.azure.com/", loaded.AzureOpenAI.Endpoint);
    }

    // ── JSON is indented (human-readable) ─────────────────────────────

    [Fact]
    public void Save_ProducesIndentedJson()
    {
        var config = new GraphifyConfig
        {
            Provider = "ollama",
            Ollama = new OllamaConfig { ModelId = "llama3.2" }
        };

        ConfigPersistence.Save(config);

        var json = File.ReadAllText(_configPath);

        // Indented JSON has newlines and spaces
        Assert.Contains("\n", json);
        Assert.Contains("  ", json);
    }
}
