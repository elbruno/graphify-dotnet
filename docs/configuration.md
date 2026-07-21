# Configuration

graphify-dotnet uses a layered configuration system so you can set AI provider settings once and forget about them.

## Interactive Config Wizard

The fastest way to configure graphify is the interactive wizard:

```bash
graphify config
```

This presents a 3-option menu:

```
? What would you like to do?
  📋 View current configuration
  🔧 Set up AI provider
  📂 Set folder to analyze
```

### Set up AI provider

Select your AI backend:

| Provider | When to use |
|----------|-------------|
| **Azure OpenAI** | Enterprise, private endpoints — requires endpoint, API key, deployment name |
| **OpenAI (compatible)** | Any OpenAI-compatible service (OpenAI API, LocalAI, LiteLLM, vLLM, etc.) — requires endpoint, API key, model |
| **Ollama (local)** | Offline/privacy — requires a running Ollama instance |
| **GitHub Copilot SDK** | Zero-config for Copilot subscribers — just select a model |
| **None (AST-only)** | No AI needed — structural extraction only |

The wizard prompts for provider-specific settings (endpoint, API key, model, etc.) and saves them to `appsettings.local.json`.

### Set folder to analyze

Configure the default project folder, output directory, and export formats. These become the defaults when you run `graphify run` with no arguments.

### View current configuration

Displays all resolved settings including the active provider, project folder, and export formats.

## Config Subcommands

You can also call config subcommands directly:

```bash
# View current configuration
graphify config show

# Launch AI provider wizard
graphify config set

# Launch folder wizard
graphify config folder
```

## Layered Configuration

Settings are resolved in priority order (highest wins):

| Priority | Source | Example |
|----------|--------|---------|
| 1 (highest) | **CLI arguments** | `--provider ollama --model codellama` |
| 2 | **User secrets** | `dotnet user-secrets set "Graphify:Provider" "Ollama"` |
| 3 | **.env file** | `GRAPHIFY__Provider=OpenAi` in `.env` |
| 4 | **Environment variables** | `GRAPHIFY__Provider=ollama` |
| 5 | **appsettings.local.json** | Saved by `graphify config` wizard |
| 6 (lowest) | **appsettings.json** | Built-in defaults |

This means CLI flags always override everything else, user secrets override environment variables, and so on.

## .env File

For local development, create a `.env` file in the project root directory. Graphify loads it automatically at startup. This is useful for keeping API keys out of your shell history or IDE configuration.

```bash
# .env file — loaded automatically by graphify
GRAPHIFY__Provider=OpenAi
GRAPHIFY__OpenAi__Endpoint=https://opencode.ai/zen/v1
GRAPHIFY__OpenAi__ApiKey=sk-opencode-zen-...
GRAPHIFY__OpenAi__ModelId=deepseek-v4-flash-free
```

> **Security:** Add `.env` to your `.gitignore` to avoid committing secrets.

> **Tip:** A comprehensive `.env.example` file with all available options is available in the [`docs/`](.env.example) folder.

## Environment Variables

All settings use the `GRAPHIFY__` prefix with double-underscore nesting:

```bash
# Azure OpenAI
export GRAPHIFY__Provider=AzureOpenAI
export GRAPHIFY__AzureOpenAI__Endpoint=https://myresource.openai.azure.com/
export GRAPHIFY__AzureOpenAI__ApiKey=sk-...
export GRAPHIFY__AzureOpenAI__DeploymentName=gpt-4o

# OpenAI (compatible)
export GRAPHIFY__Provider=OpenAi
export GRAPHIFY__OpenAi__Endpoint=https://api.openai.com/v1
export GRAPHIFY__OpenAi__ApiKey=sk-...
export GRAPHIFY__OpenAi__ModelId=gpt-4o

# Ollama
export GRAPHIFY__Provider=Ollama
export GRAPHIFY__Ollama__Endpoint=http://localhost:11434
export GRAPHIFY__Ollama__ModelId=llama3.2

# Copilot SDK (no keys needed)
export GRAPHIFY__Provider=CopilotSdk
```

## User Secrets

For development, use .NET user secrets to avoid committing API keys:

```bash
# Initialize secrets for the CLI project
cd src/Graphify.Cli
dotnet user-secrets init

# Azure OpenAI
# Set provider and credentials
dotnet user-secrets set "Graphify:Provider" "AzureOpenAI"
dotnet user-secrets set "Graphify:AzureOpenAI:Endpoint" "https://myresource.openai.azure.com/"
dotnet user-secrets set "Graphify:AzureOpenAI:ApiKey" "sk-..."
dotnet user-secrets set "Graphify:AzureOpenAI:DeploymentName" "gpt-4o"

# OpenAI (compatible)
dotnet user-secrets set "Graphify:Provider" "OpenAi"
dotnet user-secrets set "Graphify:OpenAi:Endpoint" "https://api.openai.com/v1"
dotnet user-secrets set "Graphify:OpenAi:ApiKey" "sk-..."
dotnet user-secrets set "Graphify:OpenAi:ModelId" "gpt-4o"
```

## The `--config` Flag on Run

You can launch the config wizard before a pipeline run:

```bash
graphify run --config
```

This opens the interactive wizard, saves your choices, then immediately runs the pipeline with the new settings.

## Provider Setup Guides

For detailed setup instructions per provider:

- [Azure OpenAI Setup](setup-azure-openai.md)
- [OpenAI (Compatible) Setup](setup-openai.md)
- [Ollama Setup](setup-ollama.md)
- [Copilot SDK Setup](setup-copilot-sdk.md)
