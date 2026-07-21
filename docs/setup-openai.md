# Using graphify-dotnet with OpenAI (Compatible) Endpoints

Use any OpenAI-compatible service for semantic code analysis — OpenAI API, OpenCode Zen, LocalAI, LiteLLM, vLLM, Groq, Together AI, and more.

## Quick Start

1. Get an API key from your OpenAI-compatible provider
2. Find the endpoint URL (e.g., `https://api.openai.com/v1` or `https://opencode.ai/zen/v1`)
3. Configure graphify-dotnet with `OpenAIClientFactory` or unified `ChatClientFactory`
4. Start analyzing!

## Why Use OpenAI-Compatible?

| Feature | OpenAI-Compatible | Azure OpenAI | Ollama |
|---------|-------------------|-------------|--------|
| **Choice** | Any provider, any model | Azure-only models | Open-source only |
| **Setup** | Endpoint + API key + model | Azure resource + deployment | Install + pull models |
| **Cost** | Varies by provider | Pay per request | Free (local hardware) |
| **Free options** | OpenCode Zen, others | Free tier available | Free |
| **Privacy** | Varies by provider | Enterprise-grade | Data stays local |

Perfect for:
- **Using specific models** not available on other providers
- **Free or low-cost alternatives** like OpenCode Zen
- **Self-hosted backends** (LocalAI, vLLM, LiteLLM) for privacy
- **Multi-provider flexibility** — switch models without reconfiguration

## Prerequisites

- An OpenAI-compatible API endpoint and API key
- The endpoint must support the standard `/v1/chat/completions` format

## Step 1: Choose a Provider

### OpenAI API (Official)

```bash
Endpoint: https://api.openai.com/v1
Models:   gpt-4o, gpt-4o-mini, gpt-4.1, o3, etc.
Sign up:  https://platform.openai.com
```

### OpenCode Zen (Free Models Available)

```bash
Endpoint: https://opencode.ai/zen/v1
Free models: deepseek-v4-flash-free, mimo-v2.5-free, qwen3.6-plus-free
Sign up:  https://opencode.ai/auth
```

### LocalAI (Self-Hosted)

```bash
Endpoint: http://localhost:8080/v1
Models:   Any open-source model
Setup:    https://localai.io
```

### LiteLLM (Self-Hosted Proxy)

```bash
Endpoint: http://localhost:4000
Models:   Route to any provider through a unified endpoint
Setup:    https://litellm.vercel.app
```

### vLLM (Self-Hosted)

```bash
Endpoint: http://localhost:8000/v1
Models:   High-throughput open-source models
Setup:    https://github.com/vllm-project/vllm
```

## Step 2: Configure graphify-dotnet

### CLI Usage (Recommended)

```bash
# Run with any OpenAI-compatible endpoint
graphify run ./my-project \
  --provider openai \
  --endpoint https://api.openai.com/v1 \
  --api-key sk-... \
  --model gpt-4o

# With OpenCode Zen free model
graphify run ./my-project \
  --provider openai \
  --endpoint https://opencode.ai/zen/v1 \
  --api-key sk-opencode-zen-... \
  --model deepseek-v4-flash-free

# With a self-hosted LocalAI instance
graphify run ./my-project \
  --provider openai \
  --endpoint http://localhost:8080/v1 \
  --api-key not-needed \
  --model llama-3.2
```

### Configuration Sources

graphify-dotnet supports a layered configuration system (priority order):
1. **CLI arguments** (highest priority)
2. **User secrets** (.NET user secrets)
3. **.env file** (`GRAPHIFY__*` variables in `.env`)
4. **Environment variables** (`GRAPHIFY__*`)
5. **appsettings.local.json** (saved by `graphify config` wizard)
6. **appsettings.json** (lowest priority)

### .env File

Create a `.env` file in your project root:

```bash
GRAPHIFY__Provider=OpenAi
GRAPHIFY__OpenAi__Endpoint=https://opencode.ai/zen/v1
GRAPHIFY__OpenAi__ApiKey=sk-opencode-zen-...
GRAPHIFY__OpenAi__ModelId=deepseek-v4-flash-free
```

### Environment Variables

```bash
# Linux/macOS
export GRAPHIFY__Provider=OpenAi
export GRAPHIFY__OpenAi__Endpoint=https://api.openai.com/v1
export GRAPHIFY__OpenAi__ApiKey=sk-...
export GRAPHIFY__OpenAi__ModelId=gpt-4o

# Windows (PowerShell)
$env:GRAPHIFY__Provider = "OpenAi"
$env:GRAPHIFY__OpenAi__Endpoint = "https://api.openai.com/v1"
$env:GRAPHIFY__OpenAi__ApiKey = "sk-..."
$env:GRAPHIFY__OpenAi__ModelId = "gpt-4o"
```

### User Secrets

Use .NET user secrets to keep API keys out of source control:

```bash
# Set secrets for your project
dotnet user-secrets set "Graphify:Provider" "OpenAi"
dotnet user-secrets set "Graphify:OpenAi:Endpoint" "https://api.openai.com/v1"
dotnet user-secrets set "Graphify:OpenAi:ApiKey" "sk-..."
dotnet user-secrets set "Graphify:OpenAi:ModelId" "gpt-4o"

# List configured secrets
dotnet user-secrets list
```

### appsettings.json

```json
{
  "Graphify": {
    "Provider": "OpenAi",
    "OpenAi": {
      "Endpoint": "https://api.openai.com/v1",
      "ModelId": "gpt-4o"
    }
  }
}
```

> **Note:** The API key is stored in user-secrets, not in this file.

### View Current Configuration

```bash
graphify config show
```

This displays the active configuration values from all sources (API keys are masked).

### Programmatic Configuration (Code)

For SDK usage in your own applications:

```csharp
using Graphify.Sdk;
using Microsoft.Extensions.AI;

// Create OpenAI-compatible options
var aiOptions = new AiProviderOptions(
    Provider: AiProvider.OpenAi,
    Endpoint: "https://api.openai.com/v1",
    ApiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    ModelId: "gpt-4o"
);

IChatClient client = ChatClientFactory.Create(aiOptions);

// Use the client
var response = await client.GetResponseAsync(
    [new ChatMessage(ChatRole.User, "Analyze this code structure...")]);
Console.WriteLine(response.Text);
```

## Full Working Example

```csharp
using System;
using Graphify.Sdk;
using Microsoft.Extensions.AI;

public class OpenCodeAnalyzer
{
    public static async Task Main(string[] args)
    {
        // 1. Create options
        var options = new AiProviderOptions(
            Provider: AiProvider.OpenAi,
            Endpoint: "https://opencode.ai/zen/v1",
            ApiKey: Environment.GetEnvironmentVariable("OPENCODE_API_KEY")
                ?? throw new InvalidOperationException("Missing OPENCODE_API_KEY"),
            ModelId: "deepseek-v4-flash-free"
        );

        // 2. Create the chat client
        IChatClient client = ChatClientFactory.Create(options);

        // 3. Analyze code
        string codeSnippet = @"
public class Calculator {
    public int Add(int a, int b) => a + b;
    public int Multiply(int a, int b) => a * b;
}";

        string prompt = $"Analyze this C# code and explain its structure:\n\n{codeSnippet}";

        Console.WriteLine("Analyzing with OpenAI-compatible endpoint...");
        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)]);
        Console.WriteLine("\nAnalysis:");
        Console.WriteLine(response.Text);
    }
}
```

## Recommended Models

| Model | Provider | Use Case |
|-------|----------|----------|
| **gpt-4o** | OpenAI API | Production, best quality |
| **gpt-4o-mini** | OpenAI API | Cost-efficient, fast |
| **deepseek-v4-flash-free** | OpenCode Zen | Free, good for testing |
| **qwen3.6-plus-free** | OpenCode Zen | Free alternative |

## Troubleshooting

### ❌ 401 Unauthorized

**Cause**: Invalid API key or endpoint

**Solution**:
- Double-check your API key
- Verify the endpoint URL is correct
- Some self-hosted backends may not require an API key — use any placeholder value

### ❌ 404 Not Found

**Cause**: Wrong endpoint URL or path

**Solution**:
- Ensure the endpoint includes `/v1` suffix if required (e.g., `https://api.openai.com/v1`)
- Some providers use different base paths — check their documentation

### ❌ Model Not Found

**Cause**: The specified model ID doesn't exist on that provider

**Solution**:
- List available models: `curl <endpoint>/models` (many providers support this)
- Check the provider's model catalog
- Some free tiers have limited model access

### ❌ Rate Limited (429)

**Cause**: Exceeded rate limits or quota

**Solution**:
- Wait and retry with exponential backoff
- Upgrade to a higher tier (paid plans have higher limits)
- Free models often have strict rate limits — consider a paid alternative

## See Also

- [Using graphify-dotnet with Azure OpenAI](./setup-azure-openai.md)
- [Using graphify-dotnet with Ollama (Local Models)](./setup-ollama.md)
- [Using graphify-dotnet with GitHub Copilot SDK](./setup-copilot-sdk.md)
- [OpenAI API Documentation](https://platform.openai.com/docs)
- [OpenCode Zen Documentation](https://opencode.ai/docs/zen)
- [Configuration Guide](./configuration.md)
- [API Reference: OpenAIClientFactory](../src/Graphify.Sdk/OpenAIClientFactory.cs)

## Next Steps

Once configured:
1. Run your first analysis: `graphify run . --provider openai --endpoint <url> --api-key <key> --model <model>`
2. Try different providers by changing the endpoint and model
3. Explore the [README](../README.md) for full CLI features and export formats
4. Check out [Export Formats](./export-formats.md) for output options

---

**Need help?** Open an issue on [GitHub](https://github.com/elbruno/graphify-dotnet) or check the [documentation](../README.md).
