# Using graphify-dotnet with Ollama (Local Models)

Run powerful AI models locally on your machine with Ollama—no API keys, no internet required, and completely private.

## Quick Start

1. Install Ollama from [ollama.com](https://ollama.com)
2. Pull a model: `ollama pull llama3.2`
3. Start the Ollama server (runs on port 11434 by default)
4. Configure graphify-dotnet with `OllamaClientFactory` or unified `ChatClientFactory`
5. Analyze code locally!

## Why Use Ollama?

| Feature | Ollama | Cloud APIs |
|---------|--------|-----------|
| **Privacy** | Data stays on your machine | Sent to servers |
| **Cost** | Free (one-time download) | Pay per request |
| **Offline** | Works without internet | Requires connectivity |
| **Speed** | GPU-accelerated locally | Network latency |
| **Models** | Llama, CodeLlama, Mistral, etc. | Limited selection |

Perfect for:
- **Development & testing** without spending API credits
- **Sensitive code analysis** (keeps your code private)
- **Prototyping** features that will later use cloud APIs
- **Offline environments** (airgapped networks, laptops without internet)

## Prerequisites

- **4GB+ RAM** minimum (8GB+ recommended)
- **GPU strongly recommended** (NVIDIA, AMD, or Apple Silicon for best performance)
- **2GB+ disk space** per model

## Step 1: Install Ollama

### macOS

```bash
# Download and run the installer from https://ollama.com
# Or use Homebrew:
brew install ollama

# Start the server (runs in background)
ollama serve
```

### Linux

```bash
# Official installation script
curl -fsSL https://ollama.com/install.sh | sh

# Start the server
ollama serve
```

### Windows

1. Download the Windows installer from [ollama.com/download](https://ollama.com/download/windows)
2. Run the `.exe` installer
3. The server starts automatically in the background
4. Verify it's running: Open PowerShell and run:
   ```powershell
   curl http://localhost:11434/api/tags
   ```

### Docker

```bash
docker run -d -v ollama:/root/.ollama -p 11434:11434 ollama/ollama
```

## Step 2: Pull a Model

The first time you use a model, Ollama downloads it. This may take a few minutes.

### For Code Analysis (Recommended)

```bash
# llama3.2 - Excellent for general coding tasks, 8B/70B
ollama pull llama3.2

# Or pull the larger 70B version for better analysis
ollama pull llama3.2:70b

# CodeLlama - Specialized for code, faster
ollama pull codellama

# Deepseek Coder - Excellent code understanding
ollama pull deepseek-coder
```

### For General Tasks

```bash
ollama pull mistral
ollama pull neural-chat
```

### View Installed Models

```bash
ollama list
```

### Remove a Model

```bash
ollama rm llama3.2
```

## Step 3: Verify Ollama is Running

```bash
# Check if Ollama is serving (any response = success)
curl http://localhost:11434/api/tags

# Expected response:
# {"models":[{"name":"llama3.2:latest","modified_at":"..."}]}

# On Windows with PowerShell:
curl -Uri http://localhost:11434/api/tags -UseBasicParsing
```

If you see connection errors, restart the Ollama server:

**macOS/Linux**:
```bash
# Kill existing process
pkill ollama

# Start fresh
ollama serve
```

**Windows**:
- Restart the Ollama application from the system tray
- Or: `Restart-Service ollama` in PowerShell (admin)

## Step 4: Configure graphify-dotnet

### CLI Usage (Recommended)

Use the new System.CommandLine CLI syntax to configure Ollama:

```bash
# Run with default Ollama settings (localhost:11434, llama3.2)
graphify run ./my-project --provider ollama

# Specify a custom model
graphify run ./my-project --provider ollama --model codellama

# Use a custom endpoint
graphify run ./my-project --provider ollama --endpoint http://custom:11434

# Combine options
graphify run ./my-project --provider ollama --model deepseek-coder --endpoint http://192.168.1.100:11434
```

### Configuration Sources

graphify-dotnet supports a layered configuration system (priority order):
1. **CLI arguments** (highest priority)
2. **Environment variables**
3. **User secrets** (.NET user secrets)
4. **appsettings.json** (lowest priority)

### Environment Variables

Set these for automatic configuration:

```bash
# Linux/macOS
export GRAPHIFY__Provider=ollama
export GRAPHIFY__Ollama__Endpoint=http://localhost:11434
export GRAPHIFY__Ollama__ModelId=llama3.2

# Windows (PowerShell)
$env:GRAPHIFY__Provider = "ollama"
$env:GRAPHIFY__Ollama__Endpoint = "http://localhost:11434"
$env:GRAPHIFY__Ollama__ModelId = "llama3.2"
```

### User Secrets

Use .NET user secrets for local development (keeps secrets out of source):

```bash
# Set secrets for your project
dotnet user-secrets set "Graphify:Provider" "Ollama"
dotnet user-secrets set "Graphify:Ollama:Endpoint" "http://localhost:11434"
dotnet user-secrets set "Graphify:Ollama:ModelId" "llama3.2"

# List configured secrets
dotnet user-secrets list
```

### appsettings.json

Configure in your application's appsettings.json:

```json
{
  "Graphify": {
    "Provider": "Ollama",
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "ModelId": "llama3.2"
    }
  }
}
```

### View Current Configuration

Use the `graphify config show` command to verify your configuration:

```bash
graphify config show
```

This displays the active configuration values from all sources.

### Programmatic Configuration (Code)

For SDK usage in your own applications:

```csharp
using Graphify.Sdk;
using Microsoft.Extensions.AI;

// Create Ollama options
var aiOptions = new AiProviderOptions(
    Provider: AiProvider.Ollama,
    Endpoint: "http://localhost:11434",
    ModelId: "llama3.2"
);

IChatClient client = ChatClientFactory.Create(aiOptions);

// Use the client for local analysis
var response = await client.CompleteAsync("Explain this C# code...");
Console.WriteLine(response.Message);
```

## Full Working Example

```csharp
using System;
using Graphify.Sdk;
using Microsoft.Extensions.AI;

public class LocalCodeAnalyzer
{
    public static async Task Main(string[] args)
    {
        // 1. Create Ollama options
        var options = new AiProviderOptions(
            Provider: AiProvider.Ollama,
            Endpoint: "http://localhost:11434",
            ModelId: "llama3.2"
        );

        // 2. Create the client
        IChatClient client = ChatClientFactory.Create(options);

        // 3. Analyze code locally (no internet needed!)
        string codeSnippet = @"
public class Calculator {
    public int Add(int a, int b) => a + b;
    public int Multiply(int a, int b) => a * b;
}";

        string prompt = $"Analyze this C# code:\n\n{codeSnippet}";
        
        Console.WriteLine("Analyzing with Ollama (llama3.2)...");
        var response = await client.CompleteAsync(prompt);
        Console.WriteLine("\nAnalysis:");
        Console.WriteLine(response.Message);
    }
}
```

## Recommended Models for Code Analysis

| Model | Size | Speed | Quality | Best For |
|-------|------|-------|---------|----------|
| **llama3.2** | 8B / 70B | Fast / Slow | Good / Excellent | General coding, good balance |
| **codellama** | 7B / 34B | Fast / Moderate | Very Good | Code-specific tasks |
| **deepseek-coder** | 6B / 33B | Very Fast / Fast | Excellent | Code understanding |
| **mistral** | 7B | Very Fast | Good | Lightweight, fast |

### Model Size Guide

- **7B models**: ~4-5GB VRAM, use for laptop development
- **13B models**: ~8GB VRAM, balanced performance
- **70B+ models**: 16GB+ VRAM, best quality (GPU required)

**No GPU?** Use 7B models and set `OLLAMA_NUM_GPU=0` to use CPU (slower but works).

## Performance Tips

### 1. Use GPU Acceleration

**NVIDIA**:
```bash
# Automatically detected if CUDA installed
ollama serve
```

**AMD (ROCm)**:
```bash
# Requires ROCm installation
export OLLAMA_NUM_PARALLEL=4
ollama serve
```

**Apple Silicon (M1/M2/M3)**:
```bash
# Automatically uses Neural Engine
ollama serve
```

**CPU-only** (if no GPU):
```bash
OLLAMA_NUM_GPU=0 ollama serve
```

### 2. Tune Model Size vs. Performance

```csharp
// Smaller, faster model for quick analysis
var smallOptions = new OllamaOptions(
    Endpoint: "http://localhost:11434",
    ModelId: "mistral"  // 7B, very fast
);

// Larger, higher-quality model for detailed analysis
var largeOptions = new OllamaOptions(
    Endpoint: "http://localhost:11434",
    ModelId: "llama3.2:70b"  // 70B, slower but better
);

// In production, choose based on your needs:
var model = needsSpeed ? "mistral" : "llama3.2:70b";
```

### 3. Keep Ollama Running

```bash
# Background service (macOS/Linux)
nohup ollama serve > ollama.log 2>&1 &

# Or use systemd (Linux)
sudo systemctl enable ollama
sudo systemctl start ollama
```

### 4. Batch Processing

For analyzing multiple files, queue requests to avoid overloading local resources:

```csharp
var semaphore = new SemaphoreSlim(maxConcurrentRequests: 2);

foreach (var file in codeFiles)
{
    await semaphore.WaitAsync();
    _ = AnalyzeFile(file).ContinueWith(_ => semaphore.Release());
}
```

## Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `OLLAMA_ENDPOINT` | Ollama server URL | `http://localhost:11434` |
| `OLLAMA_MODEL` | Model to use | `llama3.2` |
| `OLLAMA_NUM_GPU` | GPU layers to load (0 = CPU only) | Auto-detect |
| `OLLAMA_NUM_PARALLEL` | Parallel requests | 1 |

Example:
```bash
# Use more GPU layers for faster inference
export OLLAMA_NUM_GPU=40
export OLLAMA_NUM_PARALLEL=2
ollama serve
```

## Troubleshooting

### ❌ Connection Refused (Cannot connect to Ollama)

**Cause**: Ollama server not running

**Solution**:
```bash
# Start the server
ollama serve

# Or check if it's running
curl http://localhost:11434/api/tags

# On Windows, restart from system tray
```

### ❌ Model Not Found

**Cause**: Model hasn't been pulled yet

**Solution**:
```bash
# List available models
ollama list

# Pull the model
ollama pull llama3.2

# Check progress
ollama list
```

### ❌ Out of Memory (OOM)

**Cause**: Model is too large for available VRAM

**Solution**:
- Use a smaller model: `ollama pull mistral` (7B)
- Disable GPU: `OLLAMA_NUM_GPU=0 ollama serve`
- Add more RAM or increase swap
- Reduce parallel requests: `OLLAMA_NUM_PARALLEL=1`

### ❌ Slow Responses (Running on CPU)

**Cause**: GPU not being used

**Solution**:
```bash
# Check GPU usage
ollama list

# Verify CUDA is installed (NVIDIA)
nvidia-smi

# Restart Ollama to detect GPU
ollama serve

# Force CPU if you want (not recommended)
OLLAMA_NUM_GPU=0 ollama serve
```

### ❌ Timeout Errors

**Cause**: Model taking too long (CPU inference or large model)

**Solution**:
- Increase timeout in your code:
  ```csharp
  var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300)); // 5 min
  var response = await client.CompleteAsync(prompt, cancellationToken: cts.Token);
  ```
- Use a smaller/faster model
- Increase available VRAM

## Development vs. Production

### Development (Your Machine)

```csharp
// Easy, local setup
var options = new OllamaOptions(
    Endpoint: "http://localhost:11434",
    ModelId: "mistral"  // Fast 7B model
);
var client = OllamaClientFactory.Create(options);
```

### Production (Shared Server)

```csharp
// Use environment variables, larger model
var options = new AiProviderOptions(
    Provider: AiProvider.Ollama,
    Endpoint: Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT"),
    ModelId: Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama3.2"
);
var client = ChatClientFactory.Create(options);
```

## See Also

- [Using graphify-dotnet with Azure OpenAI](./setup-azure-openai.md)
- [Ollama Documentation](https://ollama.com)
- [Available Models](https://ollama.com/library)
- [API Reference: OllamaClientFactory](../src/Graphify.Sdk/OllamaClientFactory.cs)

## Next Steps

1. Install Ollama and pull a model
2. Verify it's running: `curl http://localhost:11434/api/tags`
3. Run the example code above
4. Explore the [README](../README.md) for SDK features
5. Build your own code analysis tools!

---

**Need help?** Open an issue on [GitHub](https://github.com/BrunoCapuano/graphify-dotnet) or check the [documentation](../README.md).
