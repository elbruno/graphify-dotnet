# graphify-dotnet

[![CI Build](https://github.com/elbruno/graphify-dotnet/actions/workflows/build.yml/badge.svg)](https://github.com/elbruno/graphify-dotnet/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![GitHub stars](https://img.shields.io/github/stars/elbruno/graphify-dotnet?style=social)](https://github.com/elbruno/graphify-dotnet/stargazers)

🔍 **Build AI-powered knowledge graphs from any codebase.** Understand structure you didn't know was there.

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

## Getting Started

**Requirements**: .NET 10 SDK

```bash
# Clone the repository
git clone https://github.com/elbruno/graphify-dotnet.git
cd graphify-dotnet

# Build the solution
dotnet build graphify-dotnet.slnx

# Run the CLI
dotnet run --project src/Graphify.Cli -- run .
```

## Usage

### Basic Commands

```bash
# Build a knowledge graph from current directory
dotnet run --project src/Graphify.Cli -- run .

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

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) file for details.

## Author

**Bruno Capuano (ElBruno)**

- 🌐 Blog: https://elbruno.com
- 📺 YouTube: https://youtube.com/@inthelabs
- 💼 LinkedIn: https://linkedin.com/in/inthelabs
- 🐦 Twitter: https://twitter.com/inthelabs
- 🎙️ Podcast: https://inthelabs.dev

## Acknowledgments

Inspired by [this tweet](https://x.com/socialwithaayan/status/2041192946369007924) by @socialwithaayan showcasing [graphify](https://github.com/safishamsi/graphify) by @safishamsi — an AI knowledge graph builder for codebases.

This project is a .NET 10 port of [safishamsi/graphify](https://github.com/safishamsi/graphify), reimagined with C# idioms, .NET 10 features, and the Microsoft.Extensions.AI abstraction layer.
