# Squad Decisions

## Active Decisions

### Decision: Copilot Instructions Convention File

**Author:** Neo (Lead/Architect)  
**Date:** 2026-04-06  
**Status:** Implemented

#### Context

The project needed a `.github/copilot-instructions.md` to codify repo conventions for Copilot and the squad team. Used ElBruno's convention style (from ElBruno.MarkItDotNet) as the starting template.

#### Decision

Created `.github/copilot-instructions.md` with the following key architectural choices:

1. **Not a NuGet package** — all NuGet publishing sections, trusted publishing, pack workflows, nuget_logo.png references, and multi-target framework guidance removed entirely.
2. **Single target: `net10.0`** — no multi-targeting. This simplifies CI and avoids preview SDK issues.
3. **Project structure under `src/`**: Graphify (core), Graphify.Cli, Graphify.Sdk, Graphify.Mcp, with tests under `src/tests/`.
4. **Solution format: `.slnx`** (XML-based).
5. **CI only** — `build.yml` with restore/build/test. No publish workflow.
6. **Pipeline architecture** documented: detect → extract → build → cluster → analyze → report → export.
7. **Key dependencies** listed as consumed packages (not published): Microsoft.Extensions.AI, GitHub.Copilot.SDK, TreeSitter.DotNet, QuikGraph, System.CommandLine, ModelContextProtocol, xUnit.

#### Impact

All team members and Copilot agents now have a single source of truth for project conventions, eliminating ambiguity about structure, naming, and CI setup.

### Decision: Complete .NET 10 Solution Scaffolding

**Author:** Neo (Lead/Architect)  
**Date:** 2026-04-06  
**Status:** Implemented

#### Context

The project needed a complete .NET 10 solution structure as the foundation for all future work. This unblocks all other todos.

#### Decision

Scaffolded the complete solution with 6 projects:

1. **src/Graphify/Graphify.csproj** — Core library targeting `net10.0`
   - Dependencies: Microsoft.Extensions.AI 10.*, QuikGraph 2.*, TreeSitter.Bindings 0.*
   - Stub folders: Pipeline/, Graph/, Export/, Cache/, Security/, Validation/, Ingest/, Models/
   - InternalsVisibleTo: Graphify.Tests

2. **src/Graphify.Cli/Graphify.Cli.csproj** — Console app (Exe)
   - Dependencies: System.CommandLine 2.*
   - Project ref: Graphify
   - Minimal console stub (System.CommandLine API simplified for now)

3. **src/Graphify.Sdk/Graphify.Sdk.csproj** — SDK library
   - Dependencies: Microsoft.Extensions.AI 10.*
   - Project ref: Graphify
   - Stub: CopilotExtractor class

4. **src/Graphify.Mcp/Graphify.Mcp.csproj** — MCP stdio server
   - Dependencies: ModelContextProtocol 0.* (actual: 1.2.0 available)
   - Project ref: Graphify
   - Stub: TODO comment for MCP server setup

5. **src/tests/Graphify.Tests/Graphify.Tests.csproj** — Unit tests
   - Dependencies: xunit 2.*, xunit.runner.visualstudio 2.*, Microsoft.NET.Test.Sdk 17.*, coverlet.collector 6.*
   - Project ref: Graphify
   - IsPackable: false, IsTestProject: true
   - One passing sample test

6. **src/tests/Graphify.Integration.Tests/Graphify.Integration.Tests.csproj** — Integration tests
   - Same test dependencies
   - Project refs: Graphify, Graphify.Cli, Graphify.Sdk
   - One passing sample test

Root files created:
- **global.json** — SDK 10.0.100, rollForward: latestMajor
- **Directory.Build.props** — Shared build properties (latest LangVersion, nullable, implicit usings, code analysis, CI build support)
- **graphify-dotnet.slnx** — XML-based solution file with logical folders (/src/, /src/tests/)

#### Key Learnings

- TreeSitter package on NuGet is `TreeSitter.Bindings` (version 0.4.0), not `TreeSitter.Bindings.CSharp`
- ModelContextProtocol package exists and is at v1.2.0
- System.CommandLine API: simplified Program.cs to basic console output stub (full CLI implementation deferred)
- All projects target `net10.0` single target (no multi-targeting)

#### Validation

- `dotnet restore graphify-dotnet.slnx` — ✅ passed
- `dotnet build graphify-dotnet.slnx` — ✅ passed
- `dotnet test graphify-dotnet.slnx` — ✅ 2/2 tests passed
- Git commit c5f1114 pushed to main

#### Impact

This scaffolding unblocks ALL other project work. The team can now begin implementing:
- Core pipeline stages
- AST parsing and extraction
- Graph building and export
- CLI commands
- SDK integration
- MCP server

### Decision: Core Graph Data Model Architecture

**Author:** Trinity (Core Developer)  
**Date:** 2026-04-06  
**Status:** Implemented

#### Context

The graphify-dotnet pipeline requires a graph data structure to represent code relationships. The Python version uses NetworkX (pure Python, dict-based nodes). We need a .NET equivalent that:
1. Supports graph algorithms (degree, betweenness, clustering)
2. Works with immutable records for thread-safety
3. Handles node/edge metadata flexibly
4. Performs well for graphs with 100-10,000 nodes

#### Decision

Use **QuikGraph's BidirectionalGraph<GraphNode, GraphEdge>** wrapped in a custom `KnowledgeGraph` class.

##### Why QuikGraph?
- **Mature library**: 2.x stable, maintained, targets modern .NET
- **Algorithm library**: Betweenness centrality, shortest paths, topological sort — we'll need these for analysis
- **Generic design**: Works with any `IEdge<TVertex>` implementation
- **Bidirectional**: O(1) access to both in-edges and out-edges (critical for degree calculations)

##### Why wrap it?
- **Domain API**: Methods like `GetNodesByCommunity()`, `AssignCommunities()`, `MergeGraph()` hide graph theory from pipeline code
- **Node indexing**: Maintain `Dictionary<string, GraphNode>` for O(1) lookup by string Id (QuikGraph only indexes by vertex reference)
- **Node replacement**: NetworkX allows `G.add_node(id, **attrs)` to overwrite. QuikGraph requires explicit remove+add. Wrapper provides this semantic.
- **Future-proofing**: If we swap graph libraries later, only `KnowledgeGraph.cs` changes

##### Alternatives Considered

1. **Pure Dictionary<string, GraphNode> + List<GraphEdge>**: Simple, but we'd reimplement graph algorithms. Not worth it.
2. **NetworkX.NET** (if it existed): Doesn't exist. Python NetworkX is pure Python, no .NET port.
3. **AdjacencyGraph** (QuikGraph): No in-edge access (only out-edges). Can't efficiently compute degree or find reverse relationships.

#### Implications

- **Immutability cost**: Updating node properties (e.g., assigning communities) requires remove+add cycle for all affected nodes and edges. Acceptable because clustering happens once per pipeline run.
- **Edge storage**: Parallel edges allowed (same Source/Target, different Relationship). Deduplication is caller's responsibility if needed.
- **Metadata schema**: No typed metadata objects yet. Use `IReadOnlyDictionary<string, string>` until we know what's needed.

#### Open Questions

1. **Hyperedges**: Python graphify has `hyperedges` list (N-to-M relationships). QuikGraph doesn't support this natively. Store as separate list in metadata?
2. **Graph serialization**: Do we serialize the entire QuikGraph or just nodes+edges as JSON? Likely the latter (export stage concern).
3. **Community assignment mutability**: Should we store community assignments in a separate `Dictionary<string, int>` instead of mutating nodes? Current approach is simple but expensive.

#### Impact

- **Foundation for pipeline**: KnowledgeGraph is the central data structure for pipeline stages (build → cluster → analyze → export)
- **Thread-safe immutability**: Graphify can parallelize analysis stages over independent graphs
- **Algorithm readiness**: QuikGraph algorithm library accessible for betweenness centrality, shortest path, clustering

### Decision: Extraction Schema Design

**Author:** Trinity (Core Developer)  
**Date:** 2026-04-06  
**Status:** Implemented

#### Context

The extraction pipeline needs a schema for representing extracted nodes and edges from source code. The Python version uses dictionaries with specific required fields validated by validate.py.

#### Decision

Created two distinct model layers:

##### 1. Extraction Models (Raw Output)
- **ExtractedNode**: Contains `Id`, `Label`, `FileType`, `SourceFile`, optional `SourceLocation` and `Metadata`
- **ExtractedEdge**: Contains `Source`, `Target` (as string IDs), `Relation`, `Confidence`, `SourceFile`, `SourceLocation`, `Weight`
- **ExtractionResult**: Aggregates lists of ExtractedNode and ExtractedEdge, plus metadata (raw text, source file path, extraction method, timestamp, confidence scores)

These use string IDs for node references because they represent raw extraction output before graph assembly.

##### 2. Graph Models (QuikGraph Integration)
- **GraphNode**: Simple node with `Id` and `Type`
- **GraphEdge**: Implements `IEdge<GraphNode>` with actual object references to source/target nodes
- Used by `KnowledgeGraph` which wraps QuikGraph's `BidirectionalGraph`

##### Supporting Types
- **Confidence** enum: Extracted, Inferred, Ambiguous (matching Python)
- **FileType** enum: Code, Document, Paper, Image (matching Python)
- **ExtractionMethod** enum: Ast, Semantic, Hybrid
- **ValidationResult** record: Non-throwing validation with success flag and error list

##### Validation
- **ExtractionValidator**: Ports Python's validate.py logic
  - Validates all nodes have non-empty Id, Label, SourceFile
  - Validates all edges have valid Source/Target IDs that match existing nodes
  - Validates all edges have non-empty Relation and SourceFile
  - Returns ValidationResult instead of throwing exceptions

#### Alternatives Considered

1. **Single unified model**: Would require mixing string IDs with object references, making it unclear when nodes are resolved
2. **Throwing validator**: Rejected in favor of returning ValidationResult for better error handling

#### Impact

- Clear separation between extraction (string IDs) and graph assembly (object references)
- Both AST and semantic extractors can output ExtractionResult
- Validation logic matches Python implementation ensuring schema compatibility
- Non-throwing validation enables better error reporting and batch validation

### Decision: Multi-Provider IChatClient Architecture (Features 2 & 3)

**Author:** Morpheus (SDK Dev)
**Date:** 2026-04-07
**Status:** Implemented

#### Context

Graphify.Sdk had a single provider stub (GitHub Models) that threw `NotImplementedException`. The short-term roadmap called for Azure OpenAI and Ollama/local model support, both using the `IChatClient` abstraction from Microsoft.Extensions.AI.

#### Decision

Implemented three provider factories behind a unified `ChatClientFactory`:

| Provider | Package | Pattern |
|---|---|---|
| GitHub Models | `OpenAI` (via M.E.AI.OpenAI) | `OpenAIClient` with custom endpoint → `.AsIChatClient()` |
| Azure OpenAI | `Azure.AI.OpenAI` 2.* | `AzureOpenAIClient` → `GetChatClient(deployment)` → `.AsIChatClient()` |
| Ollama | `OllamaSharp` 5.* | `OllamaApiClient` implements `IChatClient` natively |

##### Unified Entry Point

```csharp
ChatClientFactory.Create(new AiProviderOptions(AiProvider.Ollama, ModelId: "phi3"));
```

Dispatches to the correct factory. Required fields are validated per provider at creation time.

##### Why OllamaSharp (not Microsoft.Extensions.AI.Ollama)

Microsoft.Extensions.AI.Ollama was deprecated in favour of OllamaSharp, which natively implements `IChatClient`. OllamaSharp is the recommended client per the .NET AI ecosystem docs.

#### Impact

- Users can now target Azure OpenAI, GitHub Models, or local Ollama with zero code changes beyond config
- `CopilotExtractor` and any future pipeline stage can accept any `IChatClient` from this factory
- All 203 existing tests continue to pass
- Trinity can wire `ChatClientFactory` into the CLI when ready

#### Open Questions

1. Should we add `DefaultAzureCredential` support for Azure OpenAI (Entra ID / managed identity)?
2. Should `ChatClientFactory` live in DI as a registered service, or stay as a static factory?

### Decision: dotnet tool Packaging + Watch Mode Architecture

**Author:** Trinity (Core Developer)
**Date:** 2026-04-07
**Status:** Implemented

#### Context

Two features needed for the short-term roadmap:
1. Make the CLI installable as `dotnet tool install --global graphify-dotnet`
2. Add incremental watch mode that monitors files and re-processes only changes

#### Decision

##### Feature 1: dotnet tool packaging
Added full NuGet tool metadata to `Graphify.Cli.csproj`:
- `PackAsTool=true`, `ToolCommandName=graphify`, `PackageId=graphify-dotnet`
- Version 0.1.0, MIT license, Bruno Capuano as author
- README.md included as package readme

##### Feature 4: Watch mode architecture
- `WatchMode` class lives in **core library** (`Graphify.Pipeline`), not CLI
- Accepts a pre-built `KnowledgeGraph` via `SetInitialGraph()` — the initial pipeline run is caller's responsibility
- This avoids circular dependency (CLI references Core, not vice versa) while keeping WatchMode reusable by SDK/MCP
- Uses `FileSystemWatcher` + 500ms debounce + SHA256 content check
- Incremental: extract changed → merge into graph → re-cluster → re-export

#### Impact

- CLI is now packageable as a global dotnet tool
- Watch mode enables dev-loop usage (edit code → see graph update)
- WatchMode is reusable outside CLI (e.g., MCP server could use it)

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
