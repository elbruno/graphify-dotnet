namespace Graphify.Cli.Configuration;

/// <summary>
/// Root configuration model for Graphify settings.
/// </summary>
public class GraphifyConfig
{
    public string? Provider { get; set; }
    public string? WorkingFolder { get; set; }
    public string? OutputFolder { get; set; }
    public string? ExportFormats { get; set; }
    public AzureOpenAIConfig AzureOpenAI { get; set; } = new();
    public OllamaConfig Ollama { get; set; } = new();
    public CopilotSdkConfig CopilotSdk { get; set; } = new();
    public SurrealDbConfig SurrealDb { get; set; } = new();
}

/// <summary>
/// SurrealDB connection configuration.
/// When Endpoint is set, remote mode is used; otherwise embedded mode.
/// </summary>
public class SurrealDbConfig
{
    public string? Endpoint { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? Namespace { get; set; }
    public string? Database { get; set; }
}

/// <summary>
/// Azure OpenAI provider configuration.
/// </summary>
public class AzureOpenAIConfig
{
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? DeploymentName { get; set; }
    public string? ModelId { get; set; }
}

/// <summary>
/// Ollama provider configuration with sensible local defaults.
/// </summary>
public class OllamaConfig
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string ModelId { get; set; } = "llama3.2";
}

/// <summary>
/// GitHub Copilot SDK provider configuration.
/// Authentication is handled by the GitHub Copilot CLI (user must be logged in).
/// </summary>
public class CopilotSdkConfig
{
    public string ModelId { get; set; } = "gpt-4.1";
}
