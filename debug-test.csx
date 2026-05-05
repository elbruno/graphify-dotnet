using System.Text.Json;
using Graphify.Cli.Configuration;

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

var configPath = ConfigPersistence.GetLocalConfigPath();
var json = File.ReadAllText(configPath);
Console.WriteLine("=== SAVED JSON ===");
Console.WriteLine(json);
Console.WriteLine("=== END JSON ===");

using var doc = JsonDocument.Parse(json);
if (doc.RootElement.TryGetProperty("Graphify", out var graphify))
{
    Console.WriteLine($"Graphify section found: {graphify}");
    Console.WriteLine($"Has Ollama: {graphify.TryGetProperty("Ollama", out _)}");
    if (graphify.TryGetProperty("Ollama", out var ollama))
    {
        Console.WriteLine($"Ollama: {ollama}");
    }
}
