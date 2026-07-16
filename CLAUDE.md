# graphify-dotnet — ECC Project Guide

**Project:** AI-powered knowledge graph builder for codebases (.NET 10)  
**Architecture:** Multi-project solution with pipeline-based design  
**Status:** Active development (AST extraction, semantic analysis, graph export)

## Quick Commands

```bash
# Build
dotnet build

# Test (all)
dotnet test

# Test (specific)
dotnet test --filter "FullyQualifiedName~Graphify.Tests"

# Run CLI
dotnet run --project src/Graphify.Cli -- --help

# Package
dotnet pack src/Graphify.Sdk -o ./nupkg

# Clean build artifacts
dotnet clean
```

## Project Structure

### Core Library (`src/Graphify/`)
- **Pipeline**: File detection → extraction → graph building → clustering → analysis → export
- **Models**: Immutable records for nodes, edges, analysis results
- **Export**: JSON, HTML (vis.js), SVG, Wiki (Markdown), Obsidian, Neo4j, Ladybug graph database
- **Extraction**: Hybrid AST + semantic analysis (configurable AI provider)
- **Cache**: SHA256-based semantic cache for reproducible runs

### CLI (`src/Graphify.Cli/`)
- Entry point for command-line usage
- Configuration wizard (interactive setup for AI provider + analysis folder)
- Pipeline orchestration

### SDK (`src/Graphify.Sdk/`)
- GitHub Copilot SDK integration
- Copilot-specific extractors

### MCP Server (`src/Graphify.Mcp/`)
- Model Context Protocol server (stdio-based)
- Exposes graph operations to Claude and other MCP clients

### Tests
- **Unit tests** (`Graphify.Tests`): Component-level testing
- **Integration tests** (`Graphify.Integration.Tests`): Pipeline & export validation

## Design Principles

- **Composition over inheritance**: Interfaces + DI instead of deep hierarchies
- **Immutable data**: Records for thread-safety
- **.NET idioms**: `IOptions<T>`, `ILogger<T>`, `async/await`, `CancellationToken`
- **Type safety**: Nullable reference types enabled; strong typing throughout
- **Pipeline pattern**: Each stage is an `IPipelineStage<TIn, TOut>` implementation
- **Decoupled stages**: Output from one stage feeds into the next via well-defined models

## Code Health

**Language Settings** (see `Directory.Build.props`):
- C# latest (13)
- Nullable reference types: enabled
- Implicit usings: enabled
- Code analysis: enforced
- Warnings as errors: nullable violations only

**Key Dependencies**:
- **QuikGraph**: Graph data structures & algorithms (stable, 2.x)
- **ModelContextProtocol**: MCP protocol for Claude integration (pre-1.0, monitor for CVEs)
- **Spectre.Console**: CLI output formatting (pre-1.0, track releases)
- **System.CommandLine**: CLI parsing (stable, 2.x)
- **Microsoft.Extensions.*** packages: Follow .NET 10 preview releases (10.x)

## AI Provider Setup

The project supports three extraction modes:

1. **Azure OpenAI**: Enterprise-grade semantic analysis (recommended for production)
2. **Ollama**: Local/self-hosted LLM for privacy-first analysis
3. **None (AST-only)**: Fast, zero-config extraction (no semantic relationships)

Configure via the interactive wizard: `dotnet run --project src/Graphify.Cli -- config`

## Code Review Guidance

**Required for C# changes**: `/code-review` (ecc:csharp-reviewer)

**Focus areas**:
- Nullable reference type correctness (no #nullable disable)
- Pipeline stage contracts (ensure `IPipelineStage<TIn, TOut>` invariants)
- DI registration (Microsoft.Extensions.DependencyInjection patterns)
- Async correctness (ConfigureAwait, CancellationToken propagation)
- Export path safety (prevent directory traversal attacks)

**Known constraints**:
- MCP server uses stdio (not TCP) — test message framing end-to-end
- Semantic cache keys are SHA256(file path + content) — invalidate on API changes
- QuikGraph uses ref-based edges — be careful with value-type copies

## Integration Points

- **GitHub Actions**: CI build & publish workflows (see `.github/workflows/`)
- **MCP Clients**: Claude, IDEs, chat applications via model context protocol
- **Export formats**: Neo4j, Obsidian, vis.js visualization, custom wikis

## Getting Help

- See `docs/getting-started.md` for step-by-step walkthrough
- See `ROADMAP.md` for planned features
- See `ARCHITECTURE.md` for deep technical details
