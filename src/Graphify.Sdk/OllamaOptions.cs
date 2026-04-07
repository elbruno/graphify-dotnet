namespace Graphify.Sdk;

/// <summary>
/// Configuration options for Ollama / local model provider.
/// </summary>
/// <param name="Endpoint">Ollama server URL. Default: http://localhost:11434</param>
/// <param name="ModelId">Model to use. Default: llama3.2</param>
public record OllamaOptions(
    string Endpoint = "http://localhost:11434",
    string ModelId = "llama3.2"
);
