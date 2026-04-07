# graphify-dotnet

[![CI Build](https://github.com/elbruno/graphify-dotnet/actions/workflows/build.yml/badge.svg)](https://github.com/elbruno/graphify-dotnet/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![GitHub stars](https://img.shields.io/github/stars/elbruno/graphify-dotnet?style=social)](https://github.com/elbruno/graphify-dotnet/stargazers)

🔍 **Build AI-powered knowledge graphs from any codebase.** Understand structure you didn't know was there.

> 💡 **Origin story** — This project traces back to [Andrej Karpathy's tweet](https://x.com/karpathy/status/2039805659525644595) on using LLMs to build personal knowledge bases: ingesting raw sources, compiling them into structured Markdown wikis, and navigating knowledge through graph views instead of keyword search. That idea inspired [graphify](https://github.com/safishamsi/graphify) by [@safishamsi](https://github.com/safishamsi), which was then [showcased by @socialwithaayan](https://x.com/socialwithaayan/status/2041192946369007924) — and that's what kicked off this .NET port.

graphify-dotnet is a .NET 10 port of the Python graphify project — an AI knowledge graph builder for codebases. It reads your files (code, docs, papers, images), extracts concepts and relationships through AST parsing and semantic analysis, builds a knowledge graph with community detection, and exports to multiple formats. Navigate codebases by structure instead of keyword search.

## Features

- **Multi-stage pipeline**: detect → extract → build → cluster → analyze → report → export
- **Hybrid extraction**: AST-based code parsing (tree-sitter) + AI semantic extraction for docs/images
- **Graph clustering**: Louvain community detection to find natural groupings in your codebase
- **Multiple export formats**: JSON, HTML (vis.js interactive graph), SVG, GraphML, Wiki, Obsidian vault, Neo4j Cypher
- **MCP server**: Model Context Protocol server for AI assistant integration
- **Confidence tracking**: Every inferred edge tagged with confidence score (EXTRACTED, INFERRED, AMBIGUOUS)
- **SHA256 caching**: Only re-process changed files
- **Language support**: Python, TypeScript, JavaScript, Go, Rust, Java, C, C++, C#, Ruby, Kotlin, Scala, PHP, Swift, Lua
- **Multimodal**: Handles code, markdown, PDFs, and images (diagrams, screenshots, whiteboard photos)
- **Multi-provider AI**: Azure OpenAI and Ollama via unified `ChatClientFactory`
- **Global dotnet tool**: Install with `dotnet tool install -g graphify-dotnet` and run from anywhere
- **Incremental watch mode**: File change detection with SHA256 caching — only re-processes what changed

## Getting Started

**Requirements**: .NET 10 SDK

### Quick Install (Global Tool)

```bash
dotnet tool install -g graphify-dotnet
graphify run .
```

### Build from Source

```bash
# Clone the repository
git clone https://github.com/elbruno/graphify-dotnet.git
cd graphify-dotnet

# Build the solution
dotnet build graphify-dotnet.slnx

# Run the CLI
dotnet run --project src/Graphify.Cli -- run .
```

## AI Providers

graphify-dotnet supports multiple AI backends through a unified `ChatClientFactory`. Pick the one that fits your needs:

| Provider | Best For | Guide |
|----------|----------|-------|
| Azure OpenAI | Enterprise, private endpoints | [Setup Guide](docs/setup-azure-openai.md) |
| Ollama | Local/offline, privacy | [Setup Guide](docs/setup-ollama.md) |

## Usage

### Basic Commands

```bash
# Build a knowledge graph from current directory
dotnet run --project src/Graphify.Cli -- run .

# Watch for changes, incrementally update graph
dotnet run --project src/Graphify.Cli -- watch .

# If installed as a global tool:
graphify run .
graphify watch .

# Build from a specific folder
dotnet run --project src/Graphify.Cli -- run ./your-project

# Query the graph
dotnet run --project src/Graphify.Cli -- query "what connects AuthService to Database?"

# Explain a specific node
dotnet run --project src/Graphify.Cli -- explain "UserController"

# Export in different formats
dotnet run --project src/Graphify.Cli -- export --format html
dotnet run --project src/Graphify.Cli -- export --format svg
dotnet run --project src/Graphify.Cli -- export --format neo4j

# Analyze the graph
dotnet run --project src/Graphify.Cli -- analyze

# Run benchmarks
dotnet run --project src/Graphify.Cli -- benchmark
```

### AI Provider Configuration

```bash
# Run with default Azure OpenAI (configured via env vars or secrets)
graphify run . --provider azureopenai

# Run with Ollama (local models)
graphify run . --provider ollama

# Specify endpoint and API key (Azure OpenAI)
graphify run . --provider azureopenai --endpoint https://myresource.openai.azure.com/ --api-key sk-... --deployment gpt-4o

# Custom Ollama endpoint
graphify run . --provider ollama --endpoint http://custom:11434 --model codellama

# View current configuration
graphify config show
```

### Advanced Options

```bash
# Deep mode (more aggressive inferred edge extraction)
dotnet run --project src/Graphify.Cli -- run . --mode deep

# Update only changed files
dotnet run --project src/Graphify.Cli -- run . --update

# Rerun clustering without re-extraction
dotnet run --project src/Graphify.Cli -- run . --cluster-only

# Skip HTML visualization
dotnet run --project src/Graphify.Cli -- run . --no-viz

# Generate Obsidian vault
dotnet run --project src/Graphify.Cli -- run . --obsidian
```

## Configuration

graphify-dotnet uses a layered configuration system for AI provider settings:

**Priority order** (highest to lowest):
1. **CLI arguments** — e.g., `--provider ollama --model codellama`
2. **Environment variables** — e.g., `GRAPHIFY__Provider=ollama`
3. **User secrets** — e.g., `dotnet user-secrets set "Graphify:Provider" "AzureOpenAI"`
4. **appsettings.json** — Configuration file in the app directory
5. **Defaults** — Built-in fallback values

**Common configuration examples:**

```bash
# Using environment variables
export GRAPHIFY__Provider=AzureOpenAI
export GRAPHIFY__AzureOpenAI__Endpoint=https://myresource.openai.azure.com/
export GRAPHIFY__AzureOpenAI__ApiKey=sk-...
export GRAPHIFY__AzureOpenAI__DeploymentName=gpt-4o

# Using user secrets
dotnet user-secrets set "Graphify:Provider" "Ollama"
dotnet user-secrets set "Graphify:Ollama:Endpoint" "http://localhost:11434"

# Using CLI arguments (highest priority)
graphify run . --provider azureopenai --endpoint https://... --api-key sk-... --deployment gpt-4o

# View active configuration
graphify config show
```

For detailed setup guides, see:
- [Azure OpenAI Setup](docs/setup-azure-openai.md)
- [Ollama Setup](docs/setup-ollama.md)

## Architecture

graphify-dotnet implements a multi-stage pipeline:

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  Detect  │ -> │ Extract  │ -> │  Build   │ -> │ Cluster  │
│  Files   │    │ Features │    │  Graph   │    │ (Louvain)│
└──────────┘    └──────────┘    └──────────┘    └──────────┘
                                                       │
                                                       v
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  Export  │ <- │  Report  │ <- │ Analyze  │ <- │ Clustered│
│ Formats  │    │Generator │    │  Graph   │    │  Graph   │
└──────────┘    └──────────┘    └──────────┘    └──────────┘
```

**Pipeline Stages:**

1. **Detect**: Scan directories and identify file types (code, docs, images)
2. **Extract**: AST parsing for code + AI semantic extraction for docs/images
3. **Build**: Construct graph from extracted nodes and edges
4. **Cluster**: Apply Louvain community detection to find natural groupings
5. **Analyze**: Calculate centrality metrics, identify god nodes and surprising connections
6. **Report**: Generate human-readable summary with suggested questions
7. **Export**: Output to JSON, HTML, SVG, Wiki, Obsidian, Neo4j

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed component documentation.

## Export Formats

- **JSON** (`graph.json`): Complete graph with nodes, edges, communities, metadata
- **HTML** (`graph.html`): Interactive vis.js visualization — click nodes, search, filter by community
- **SVG** (`graph.svg`): Static vector graphic for documentation
- **GraphML** (`graph.graphml`): Import into Gephi or yEd
- **Wiki** (`wiki/`): Wikipedia-style markdown articles per community
- **Obsidian** (`obsidian-vault/`): Vault with backlinks and graph view
- **Neo4j Cypher** (`cypher.txt`): Import script or direct push to Neo4j instance

## Building from Source

```bash
# Prerequisites
# - .NET 10 SDK or later

# Clone and build
git clone https://github.com/elbruno/graphify-dotnet.git
cd graphify-dotnet

# Restore dependencies
dotnet restore graphify-dotnet.slnx

# Build solution
dotnet build graphify-dotnet.slnx --configuration Release

# Run tests
dotnet test graphify-dotnet.slnx

# Run the CLI
dotnet run --project src/Graphify.Cli -- run .
```

## Documentation

- [Azure OpenAI Setup](docs/setup-azure-openai.md)
- [Ollama Setup](docs/setup-ollama.md)
- [Global Tool Install](docs/dotnet-tool-install.md)
- [Watch Mode](docs/watch-mode.md)
- [Architecture](ARCHITECTURE.md)

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) file for details.

## 👋 About the Author

**Made with ❤️ by [Bruno Capuano (ElBruno)](https://github.com/elbruno)**

- 📝 **Blog**: [elbruno.com](https://elbruno.com)
- 📺 **YouTube**: [youtube.com/elbruno](https://youtube.com/elbruno)
- 🔗 **LinkedIn**: [linkedin.com/in/elbruno](https://linkedin.com/in/elbruno)
- 𝕏 **Twitter**: [twitter.com/elbruno](https://twitter.com/elbruno)
- 🎙️ **Podcast**: [notienenombre.com](https://notienenombre.com)

## Acknowledgments

- [Andrej Karpathy's tweet](https://x.com/karpathy/status/2039805659525644595) on LLM-powered personal knowledge bases — the original idea that started the chain.
- [This tweet](https://x.com/socialwithaayan/status/2041192946369007924) by @socialwithaayan showcasing [graphify](https://github.com/safishamsi/graphify) by @safishamsi — which directly inspired this .NET port.

This project is a .NET 10 port of [safishamsi/graphify](https://github.com/safishamsi/graphify), reimagined with C# idioms, .NET 10 features, and the Microsoft.Extensions.AI abstraction layer.
