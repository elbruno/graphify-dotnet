# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## graphify

This project has a graphify knowledge graph at graphify-out/.

Rules:
- Before answering architecture or codebase questions, read graphify-out/GRAPH_REPORT.md for god nodes and community structure
- If graphify-out/wiki/index.md exists, navigate it instead of reading raw files
- For cross-module "how does X relate to Y" questions, prefer `graphify query "<question>"`, `graphify path "<A>" "<B>"`, or `graphify explain "<concept>"` over grep — these traverse the graph's EXTRACTED + INFERRED edges instead of scanning files
- After modifying code files in this session, run `graphify update .` to keep the graph current (AST-only, no API cost)

## Build & Test

```bash
# Build entire solution
dotnet build graphify-dotnet.slnx

# Run all tests
dotnet test graphify-dotnet.slnx --no-build

# Run single test by name
dotnet test src/tests/Graphify.Tests -- --filter "DisplayName~MyTestMethod"

# Run the CLI locally
dotnet run --project src/Graphify.Cli -- run .
dotnet run --project src/Graphify.Cli -- run . --format json,html,report --verbose
```

Solution file is `.slnx` (XML format), **not** `.sln` — use it for all solution-level operations.

## CLI Commands

| Command | Purpose |
|---------|---------|
| `graphify run [path]` | Full pipeline: detect → extract → build → cluster → analyze → report → export |
| `graphify watch [path]` | Run pipeline then watch for file changes |
| `graphify benchmark [graph-path]` | Measure token reduction vs raw files |
| `graphify config` | Interactive config wizard |
| `graphify config show` | Display resolved config (all sources merged) |
| `graphify config set` | Set AI provider interactively |
| `graphify config folder` | Set default project folder |

Export formats (comma-separated `--format`): `json`, `html`, `svg`, `neo4j`, `obsidian`, `wiki`, `report`. Default: `json,html,report`.

## Architecture: Pipeline Pattern

Seven discrete stages, each implementing `IPipelineStage<TIn, TOut>`:

```
FileDetector → Extractor → GraphBuilder → ClusterEngine → Analyzer → ReportGenerator → IGraphExporter[]
```

**Extractor** runs two paths in parallel:
- **AST** (code files): `TreeSitter.Bindings` — extracts classes, functions, imports, call graphs. All edges marked `Confidence.Extracted`.
- **Semantic** (docs/images): `IChatClient` from `Microsoft.Extensions.AI` — extracts concepts, relationships, design rationale. Edges marked `Confidence.Inferred` or `Confidence.Ambiguous`.

**KnowledgeGraph** wraps `QuikGraph.BidirectionalGraph<GraphNode, GraphEdge>`. Node updates require remove+add cycle (QuikGraph limitation) — hidden behind `KnowledgeGraph` domain API. Community detection uses Louvain algorithm.

**SemanticCache** hashes files with SHA256; on `--update` only re-extracts changed files.

## Project Layout

All source under `src/` — never create project directories at repo root.

| Project | Role |
|---------|------|
| `src/Graphify/` | Core library: pipeline stages, graph, export, cache, validation |
| `src/Graphify.Cli/` | `PackAsTool` CLI entry point (`System.CommandLine` + `Spectre.Console`) |
| `src/Graphify.Sdk/` | GitHub Copilot SDK extractor integration |
| `src/Graphify.Mcp/` | MCP stdio server (`ModelContextProtocol`) |
| `src/tests/Graphify.Tests/` | Unit tests (xUnit, mock `IChatClient`) |
| `src/tests/Graphify.Integration.Tests/` | End-to-end pipeline tests |

## Key Conventions

- **Target**: `net10.0` only (no multi-targeting)
- **Nullable**: enabled project-wide; `WarningsAsErrors=nullable` (other warnings are not errors)
- **`ImplicitUsings`**: enabled
- All projects inherit from `Directory.Build.props` at repo root
- Test projects: `IsPackable=false`, `IsTestProject=true`, xUnit + coverlet

## Configuration Resolution Order

1. CLI args (`--provider`, `--endpoint`, `--api-key`, `--model`, `--deployment`)
2. Environment variables (`GRAPHIFY__Provider`, `GRAPHIFY__AzureOpenAI__Endpoint`, etc.)
3. User secrets (`dotnet user-secrets`)
4. `appsettings.local.json` (wizard-saved)
5. `appsettings.json` (defaults)

AI provider is optional — AST-only mode works with zero config and never sends code to external services.

## Extension Points

- **New language**: add tree-sitter grammar in `src/Graphify/Pipeline/Extractor.cs`
- **New export format**: implement `IGraphExporter` in `src/Graphify/Export/`
- **New pipeline stage**: implement `IPipelineStage<TIn, TOut>` and wire into `PipelineRunner`
- **New validation rule**: extend `src/Graphify/Validation/ExtractionValidator.cs`
