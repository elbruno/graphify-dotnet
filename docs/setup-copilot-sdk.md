# Using graphify-dotnet with GitHub Copilot SDK

Use GitHub Copilot as your AI provider—no API keys needed, just your GitHub Copilot subscription.

## Quick Start

1. Install [GitHub Copilot CLI](https://docs.github.com/en/copilot/using-github-copilot/using-github-copilot-in-the-command-line) or sign in via VS Code
2. Verify you're authenticated: `copilot auth status`
3. Run graphify: `graphify run . --provider copilotsdk`

## Why Use Copilot SDK?

| Feature | Copilot SDK | Cloud APIs | Local (Ollama) |
|---------|-------------|-----------|----------------|
| **Setup** | Login once, done | API keys, endpoints | Install, pull models |
| **Auth** | GitHub login | API key management | None needed |
| **Cost** | Included in Copilot subscription | Pay per request | Free |
| **Models** | GPT-4.1, GPT-4o, Claude Sonnet, etc. | Provider-specific | Open models |
| **Privacy** | GitHub Copilot terms apply | Varies by provider | Data stays local |

Perfect for:
- **GitHub Copilot subscribers** who want zero-config AI
- **Quick prototyping** without managing API keys or endpoints
- **Teams** already using GitHub Copilot in their workflow

## Prerequisites

- **GitHub Copilot subscription**: Individual, Business, or Enterprise ([sign up](https://github.com/features/copilot))
- **GitHub Copilot CLI** installed, **or** signed in to GitHub via VS Code
- **.NET 10 SDK** or later

## Step 1: Install GitHub Copilot CLI

### Via GitHub CLI Extension

```bash
# Install GitHub CLI if you don't have it
# https://cli.github.com/

# Install the Copilot CLI extension
gh extension install github/gh-copilot
```

### Via VS Code

If you use VS Code with the GitHub Copilot extension, you're already authenticated — the SDK picks up your session automatically.

## Step 2: Authenticate

### Option A: GitHub Copilot CLI

```bash
# Log in to GitHub Copilot
copilot auth login

# Verify authentication status
copilot auth status
```

### Option B: GitHub CLI

```bash
# Log in with GitHub CLI
gh auth login

# Verify your Copilot access
gh copilot --help
```

### Option C: VS Code

1. Open VS Code
2. Sign in to GitHub via the Accounts menu (bottom-left)
3. Ensure the GitHub Copilot extension is installed and active

The Copilot SDK uses `UseLoggedInUser = true`, which detects your active GitHub session from any of these methods.

## Step 3: Configure graphify-dotnet

### CLI Usage (Recommended)

```bash
# Run with Copilot SDK (default model: gpt-4.1)
graphify run ./my-project --provider copilotsdk

# Specify a different model
graphify run ./my-project --provider copilotsdk --model gpt-4o

# Use Claude Sonnet via Copilot
graphify run ./my-project --provider copilotsdk --model claude-sonnet

# Verbose output to see progress
graphify run ./my-project --provider copilotsdk -v
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
export GRAPHIFY__Provider=CopilotSdk
export GRAPHIFY__CopilotSdk__ModelId=gpt-4.1

# Windows (PowerShell)
$env:GRAPHIFY__Provider = "CopilotSdk"
$env:GRAPHIFY__CopilotSdk__ModelId = "gpt-4.1"
```

### User Secrets

Use .NET user secrets for local development:

```bash
# Set secrets for your project
dotnet user-secrets set "Graphify:Provider" "CopilotSdk"
dotnet user-secrets set "Graphify:CopilotSdk:ModelId" "gpt-4.1"

# List configured secrets
dotnet user-secrets list
```

### appsettings.json

Configure in your application's appsettings.json:

```json
{
  "Graphify": {
    "Provider": "CopilotSdk",
    "CopilotSdk": {
      "ModelId": "gpt-4.1"
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

// Create CopilotSdk options
var aiOptions = new AiProviderOptions(
    Provider: AiProvider.CopilotSdk,
    ModelId: "gpt-4.1"
);

// Note: CopilotSdk requires async initialization
IChatClient client = await ChatClientFactory.CreateAsync(aiOptions);

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

public class CopilotCodeAnalyzer
{
    public static async Task Main(string[] args)
    {
        // 1. Create options (no API key needed!)
        var options = new AiProviderOptions(
            Provider: AiProvider.CopilotSdk,
            ModelId: "gpt-4.1"
        );

        // 2. Create the client (async required for Copilot SDK)
        IChatClient client = await ChatClientFactory.CreateAsync(options);

        // 3. Analyze code
        string codeSnippet = @"
public class Calculator {
    public int Add(int a, int b) => a + b;
    public int Multiply(int a, int b) => a * b;
}";

        string prompt = $"Analyze this C# code and explain its structure:\n\n{codeSnippet}";

        Console.WriteLine("Analyzing with GitHub Copilot (gpt-4.1)...");
        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, prompt)]);
        Console.WriteLine("\nAnalysis:");
        Console.WriteLine(response.Text);
    }
}
```

## Available Models

Models available through GitHub Copilot SDK depend on your subscription tier. Common options:

| Model | Description | Best For |
|-------|-------------|----------|
| **gpt-4.1** | Latest GPT-4.1 (default) | General code analysis, high quality |
| **gpt-4o** | GPT-4o multimodal | Fast, good for images + code |
| **gpt-4o-mini** | GPT-4o mini | Cost-effective, quick tasks |
| **claude-sonnet** | Claude Sonnet | Strong reasoning, detailed analysis |

> **Note**: Available models may change as GitHub Copilot updates its offerings. Use `--model` to select the model that fits your needs.

## How It Works

Under the hood, the Copilot SDK provider:

1. **Authenticates** via your existing GitHub Copilot session (CLI, VS Code, or GitHub CLI)
2. **Creates a `CopilotClient`** with `UseLoggedInUser = true`
3. **Wraps it in `CopilotChatClient`** — an `IChatClient` adapter that bridges the Copilot SDK to the standard Microsoft.Extensions.AI interface
4. **Creates sessions** per request, sending prompts and collecting responses

This means the Copilot SDK integrates seamlessly with the same graphify pipeline used by Azure OpenAI and Ollama — no provider-specific code needed in the analysis pipeline.

### Microsoft Agent Framework

The SDK also includes integration with the Microsoft Agent Framework (`Microsoft.Agents.AI` and `Microsoft.Agents.AI.GitHub.Copilot` packages). This enables advanced agentic scenarios beyond basic chat completions.

## Troubleshooting

### ❌ Authentication Failed / Not Logged In

**Cause**: No active GitHub Copilot session found

**Solution**:
```bash
# Check if you're logged in
copilot auth status

# Or with GitHub CLI
gh auth status

# Log in if needed
copilot auth login
# or
gh auth login
```

If using VS Code, ensure you're signed in to GitHub in the Accounts menu and the Copilot extension is active.

### ❌ Copilot CLI Not Found

**Cause**: GitHub Copilot CLI not installed or not in PATH

**Solution**:
```bash
# Install via GitHub CLI
gh extension install github/gh-copilot

# Verify installation
gh copilot --help
```

### ❌ "CopilotSdk requires async initialization"

**Cause**: Using `ChatClientFactory.Create()` (sync) instead of `CreateAsync()`

**Solution**: The Copilot SDK needs to start an async session. Use `CreateAsync`:
```csharp
// ❌ Wrong — throws InvalidOperationException
var client = ChatClientFactory.Create(options);

// ✅ Correct
var client = await ChatClientFactory.CreateAsync(options);
```

When using the CLI (`graphify run`), this is handled automatically.

### ❌ Subscription / Access Denied

**Cause**: Your GitHub account doesn't have an active Copilot subscription

**Solution**:
- Verify your subscription at [github.com/settings/copilot](https://github.com/settings/copilot)
- If using Copilot Business or Enterprise, ensure your organization admin has granted you access
- Try logging out and back in: `gh auth logout` then `gh auth login`

### ❌ Model Not Available

**Cause**: The requested model isn't available in your Copilot tier

**Solution**:
- Try the default model: `--model gpt-4.1`
- Check available models in your Copilot dashboard
- Some models may be restricted to certain subscription tiers

## See Also

- [Using graphify-dotnet with Azure OpenAI](./setup-azure-openai.md)
- [Using graphify-dotnet with Ollama (Local Models)](./setup-ollama.md)
- [GitHub Copilot Documentation](https://docs.github.com/en/copilot)
- [GitHub.Copilot.SDK NuGet Package](https://www.nuget.org/packages/GitHub.Copilot.SDK)

## Next Steps

Once configured:
1. Run your first analysis: `graphify run . --provider copilotsdk`
2. Try different models: `--model gpt-4o` or `--model claude-sonnet`
3. Explore the [README](../README.md) for full CLI features and export formats
4. Check out [Export Formats](./export-formats.md) for output options

---

**Need help?** Open an issue on [GitHub](https://github.com/elbruno/graphify-dotnet) or check the [documentation](../README.md).
