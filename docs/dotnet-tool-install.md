# Installing graphify-dotnet as a Global .NET Tool

This guide walks you through installing and using graphify-dotnet as a command-line tool on your machine.

## Quick Start

```bash
# Install from NuGet
dotnet tool install -g graphify-dotnet

# Run against current directory
graphify run .

# Check it worked
graphify --help
```

## Prerequisites

- **.NET 10 SDK** or later ([download](https://dotnet.microsoft.com/download))
- Command-line access (PowerShell, Bash, or equivalent)

Verify installation:
```bash
dotnet --version
```

## Installation Methods

### Method 1: From NuGet (Recommended)

Once graphify-dotnet is published to NuGet.org:

```bash
dotnet tool install -g graphify-dotnet
```

This installs the global tool and adds it to your PATH automatically.

### Method 2: From Local Build

If you're developing or testing a local build:

```bash
# Build the NuGet package
dotnet pack src/Graphify.Cli/ -c Release

# Install from your local build output
dotnet tool install -g graphify-dotnet \
  --add-source src/Graphify.Cli/bin/Release \
  --allow-prerelease-versions
```

### Method 3: From Source

For quick testing without installing globally:

```bash
# Clone or navigate to the repository
cd graphify-dotnet

# Run directly from source
dotnet run --project src/Graphify.Cli -- run .

# With options
dotnet run --project src/Graphify.Cli -- watch . --output ./my-graph
```

## Usage

Once installed, the `graphify` command is available system-wide:

```bash
# Analyze current directory
graphify run .

# Analyze a specific path with custom output
graphify run ./src --output ./docs/graph

# Watch for changes and incrementally update the graph
graphify watch . --output ./graph-out

# Measure token reduction in an existing graph
graphify benchmark ./graph-out/graph.json

# View current configuration
graphify config show
```

### Provider Configuration

```bash
# Run with Azure OpenAI (configured via env vars or secrets)
graphify run . --provider azureopenai

# Run with Ollama (local models)
graphify run . --provider ollama

# Specify custom endpoint and credentials
graphify run . --provider azureopenai \
  --endpoint https://myresource.openai.azure.com/ \
  --api-key sk-... \
  --deployment gpt-4o

# Custom Ollama endpoint
graphify run . --provider ollama \
  --endpoint http://custom:11434 \
  --model codellama
```

### Available Commands

- **`graphify run [path] [options]`** — Full pipeline: detect files, extract knowledge, build graph, export
- **`graphify watch [path] [options]`** — Watch for file changes and incrementally re-process
- **`graphify benchmark [graph.json]`** — Measure token reduction of a generated graph
- **`graphify config show`** — Display active configuration from all sources

### Common Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--output` | `-o` | `graphify-out` | Directory where output files are saved |
| `--format` | `-f` | `json,html` | Export formats (comma-separated: `json`, `html`) |
| `--verbose` | `-v` | — | Enable detailed output and diagnostics |
| `--provider` | — | — | AI provider: `azureopenai` or `ollama` |
| `--endpoint` | — | — | Custom endpoint URL |
| `--api-key` | — | — | API key (Azure OpenAI) |
| `--deployment` | — | — | Deployment name (Azure OpenAI) |
| `--model` | — | — | Model ID (Ollama) |
| `--help` | `-h` | — | Show command help |

### Examples

```bash
# Analyze with HTML and JSON output
graphify run ./src --format json,html --output ./reports

# Watch with verbose logging
graphify watch . --verbose

# Custom output location
graphify run /path/to/project --output /path/to/output

# Benchmark a graph
graphify benchmark ./graphify-out/graph.json

# Run with Ollama using custom endpoint
graphify run . --provider ollama --endpoint http://192.168.1.100:11434 --model llama3.2
```

## Updating

To get the latest version:

```bash
dotnet tool update -g graphify-dotnet
```

This updates to the latest stable release on NuGet.

## Uninstalling

To remove the global tool:

```bash
dotnet tool uninstall -g graphify-dotnet
```

This removes the tool from your PATH and deletes the installation.

## Troubleshooting

### Command Not Found

**Problem:** `graphify: command not found` or `graphify is not recognized`

**Solution:** Verify the tool installed:
```bash
dotnet tool list -g | grep graphify
```

If it's listed, your PATH may not be updated:
- **PowerShell:** Restart your terminal or run `$env:PATH -split ';' | Select-String 'dotnet'`
- **Bash/Linux/macOS:** Source your shell profile: `source ~/.bashrc` or `source ~/.zshrc`

### Version Conflicts

**Problem:** Multiple versions installed or conflicts with project tools

**Check installed version:**
```bash
graphify --help
```

**View all installed tools:**
```bash
dotnet tool list -g
```

**Force reinstall:**
```bash
dotnet tool uninstall -g graphify-dotnet
dotnet tool install -g graphify-dotnet
```

### No Permission / Access Denied

**Problem:** Installation fails with permission errors

**Solution (Windows):** Run PowerShell as Administrator
```powershell
# Run as Admin
dotnet tool install -g graphify-dotnet
```

**Solution (Linux/macOS):** Use `sudo` if necessary:
```bash
sudo dotnet tool install -g graphify-dotnet
```

### .NET SDK Not Found

**Problem:** `dotnet: command not found` or SDK not detected

**Solution:** Install .NET 10 SDK from [dotnet.microsoft.com](https://dotnet.microsoft.com/download)

Verify after installation:
```bash
dotnet --version
```

## See Also

- [Watch Mode Guide](./watch-mode.md) — Incremental knowledge graph updates
- [README](../README.md) — Project overview
