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

### Decision: Documentation Overhaul — Neo's Improvement Plan

**Author:** Trinity (Core Developer)  
**Date:** 2026-04-07  
**Status:** Implemented

#### Context

Neo audited all 19 docs + README as a new user and produced a 13-item improvement plan (`neo-docs-improvement-plan.md`). Implemented the top 8 priority items covering the critical gap: no guided tutorial, shallow worked example, scattered troubleshooting, and inconsistencies across docs.

#### What Changed

##### New Files
- `docs/getting-started.md` — Real step-by-step tutorial (install → first analysis → interpret results → add AI → try your own code)
- `docs/troubleshooting.md` — Central FAQ with 10 common problems
- `ROADMAP.md` — Moved from `docs/future-plans.md` (OSS convention)
- `.squad/image-prompts.md` — Moved from `docs/` (not user-facing)

##### Rewritten
- `docs/worked-example.md` — Expanded from 48 to ~250 lines with real data walkthrough

##### Fixed
- Default formats: Normalized to `json,html,report` everywhere (was inconsistent between docs)
- Blog post: Fixed non-existent CLI commands (`query`, `explain`, `export`) and format name (`GraphML`)
- Obsidian: Removed `--filter "community:Auth"` (flag doesn't exist)
- Ollama: Fixed user-facing code to use `AiProviderOptions` + `ChatClientFactory` instead of internal `OllamaOptions`
- dotnet-tool-install: Added `copilotsdk` to provider list
- Cross-links: All 7 format-*.md files now link to worked example
- README: Added Supported Languages table, AST-only note, Getting Started + Troubleshooting in docs table

#### Key Decision

All user-facing code examples use `AiProviderOptions` + `ChatClientFactory.Create()` as the unified API. The internal `OllamaOptions`/`OllamaClientFactory` classes exist but aren't the recommended entry point for docs.

#### Impact

A new user can now: install → run getting-started tutorial → understand output → explore worked example → troubleshoot problems — all without reading every doc. The docs table in README puts Getting Started first.

### Decision: Documentation Fixes: SDK API + Config Priority + JSON Schema

**Author:** Morpheus (SDK Dev)  
**Date:** 2026-04-07  
**Status:** Applied

#### Context

Tank's validation report identified critical documentation errors across 8 files. All errors were in SDK-related content — wrong API methods, wrong configuration priority, and a fabricated JSON schema.

#### Changes Made

##### 1. M.E.AI API Migration (setup-ollama.md, setup-azure-openai.md)
- `client.CompleteAsync(prompt)` → `client.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)])`
- `response.Message` → `response.Text`
- Verified against SemanticExtractor.cs and CopilotChatClient.cs

##### 2. Configuration Priority (4 docs)
- Swapped env vars and user secrets to match ConfigurationFactory.cs
- Correct order: CLI args > user secrets > env vars > appsettings.local.json > appsettings.json
- Added missing appsettings.local.json layer to 3 setup docs

##### 3. JSON Schema (format-json.md)
- Removed fictitious top-level `communities` array
- Removed non-existent node fields: `degree`, `description`
- Removed non-existent edge field: `extractionMethod`
- Fixed metadata fields to snake_case matching JsonExporter.cs DTOs
- Removed non-existent `version` and `source` metadata fields

##### 4. Other Fixes
- worked-example.md: 35 → 30 obsidian files
- watch-mode.md: namespace, constructor params, default format
- dotnet-tool-install.md: added `-p` short alias
- GitHub URLs: BrunoCapuano → elbruno in 2 docs

#### Impact

All documentation now matches actual source code. No code changes were needed — docs-only fixes.

### Decision: Interactive `config set` Wizard + ConfigurationFactory CopilotSdk Routing

**Author:** Trinity (Core Developer)  
**Date:** 2026-04-07  
**Status:** Implemented

#### Context

The CLI had `config show` but no way to interactively set provider configuration. Additionally, `ConfigurationFactory` had a bug where `--model` and `--endpoint` CLI overrides didn't recognize the CopilotSdk provider, falling through to Azure OpenAI defaults.

#### Decision

##### 1. `config set` uses Console.ReadLine() interactive prompts
- Numbered menu: 1=Ollama, 2=Azure OpenAI, 3=Copilot SDK
- Provider-specific follow-up prompts with defaults where applicable
- Azure OpenAI requires all fields (endpoint, key, deployment, model)
- Ollama/CopilotSdk have sensible defaults

##### 2. Persistence via `dotnet user-secrets set --id`
- Uses `System.Diagnostics.Process` to shell out to `dotnet user-secrets`
- UserSecretsId is hardcoded: `graphify-dotnet-3134eb8e-5948-4541-b6e4-ab9f52f3df62`
- Each key/value pair saved individually

##### 3. ConfigurationFactory now routes all three providers
- `--model` routes to correct section key per provider
- `--endpoint` is silently ignored for CopilotSdk (no endpoint needed)

#### Impact

- Users can now configure providers without manually editing secrets.json
- CopilotSdk users can use `--model` CLI flag correctly
- No breaking changes to existing commands

### Decision: ConfigPersistence, ConfigurationFactory & CopilotSdk Test Coverage

**Author:** Tank (Tester)  
**Date:** 2026-04-07  
**Status:** Implemented

#### Context

Trinity implemented an interactive configuration wizard (ConfigWizard, ConfigPersistence, ConfigurationFactory updates) for the CLI using Spectre.Console, plus CopilotSdk routing fixes. Tests were needed for file I/O, configuration layering, and CopilotSdk CLI override routing.

#### Decision

- **ConfigWizard is NOT unit-testable** in its current form — it uses static `AnsiConsole` methods that require a TTY. If we want wizard-level testing in the future, we'd need to inject `IAnsiConsole` instead.
- **ConfigPersistence** is fully tested via file round-trips. The `[Collection("ConfigFile")]` attribute is required on any test class that reads/writes `appsettings.local.json` in `AppContext.BaseDirectory`.
- **ConfigurationFactory** integration tests write temp files and verify layer priority (local file < CLI args).
- **CopilotSdk routing** tests verify that `CliProviderOptions(Provider: "copilotsdk", Model: X)` routes to `Graphify:CopilotSdk:ModelId` (not AzureOpenAI), and that `--endpoint` is silently dropped.

#### Impact

- Tests covering ConfigPersistence code paths, ConfigurationFactory local config loading, and CopilotSdk CLI override routing.
- Any future test class that touches `appsettings.local.json` in the test output directory MUST use `[Collection("ConfigFile")]` to avoid race conditions.
- appsettings.json defaults (Ollama endpoint) are loaded by ConfigurationFactory.Build() — tests must not assert those are null

### Decision: Comprehensive Plan for NuGet Publishing graphify-dotnet as a dotnet Global Tool

**Author:** Neo (Lead/Architect)  
**Date:** 2026-04-07  
**Status:** Plan (not yet implemented)  
**Scope:** NuGet.org publishing workflow, icon/metadata, OIDC trusted publishing, version management  
**Requested by:** Bruno Capuano

#### Problem Statement

**Current state:**  
Graphify-dotnet targets `net10.0` and already has `Graphify.Cli.csproj` configured with:
- `PackAsTool=true`, `ToolCommandName=graphify`, `PackageId=graphify-dotnet`
- Version 0.1.0, MIT license, README.md included

**Gap:**  
The project is not distributable as a public NuGet package. Users cannot install via `dotnet tool install -g graphify-dotnet`.

**What's missing:**
1. Package icon (`images/nuget_logo.png`)
2. Symbol package configuration (`<IncludeSymbols>`, `<SymbolPackageFormat>`)
3. `<PackageIcon>` metadata pointing to the icon
4. GitHub Actions publish workflow (`.github/workflows/publish.yml`)
5. GitHub `release` environment with NUGET_USER secret
6. OIDC trusted publisher registration on NuGet.org
7. Version management strategy (Git tags, validation)
8. README badges linking to NuGet package page
9. Team documentation on release procedures

#### Approach

**Core strategy:**  
Adapt the ElBruno.MarkItDotNet `publish.yml` pattern for our .NET 10 tooling context.

**Key differences from reference:**
- ElBruno targets `net8.0` (multi-target library); graphify-dotnet targets `net10.0` only (single-target tool)
- Use OIDC trusted publishing (no long-lived API keys in repo)
- Trigger on GitHub `release` (published) event + manual `workflow_dispatch` with optional version override

**Publish workflow:**
1. Create GitHub release with tag `v0.1.0` (version extracted, `v` prefix stripped)
2. Workflow triggers: restore → build (Release) → test → pack (generates .nupkg + .snupkg)
3. OIDC login to NuGet.org via `NuGet/login@v1`
4. Push packages with `--skip-duplicate` (safe for re-runs)
5. Add NuGet badges to README

**Security:**
- No API keys in repo; OIDC handles auth
- GitHub's `id-token: write` permission enables short-lived JWT exchange
- One-time NuGet.org trust configuration (secure, maintainable)

#### Success Criteria

✅ `graphify-dotnet` published to NuGet.org  
✅ Installation works: `dotnet tool install -g graphify-dotnet`  
✅ Command available in PATH: `graphify --help`  
✅ Package page shows icon, metadata, download counts  
✅ README badges link to package page  
✅ Publish workflow runs automatically on GitHub release  
✅ No long-lived API keys in repo  
✅ Team knows how to create releases and monitor status  

#### Dependencies

- GitHub repository access (Settings → Environments)
- NuGet.org account (Bruno's username)
- .NET 10 SDK availability on GitHub runners
- No external dependencies added (existing packages sufficient)

### Decision: Relative Path Handling — TDD Test Suite for Issue #2

**Author:** Tank (Tester)  
**Date:** 2026-04-07  
**Status:** Implemented  
**Related:** GitHub Issue #2 "Graphify files are taking absolute path instead of relative path"

#### Context

Users reported that JSON and HTML exports store absolute file paths (e.g., `C:\Users\...\src\Class1.cs`) instead of relative paths. This breaks portability: exported graphs are hard to share or move because path references are machine-specific.

#### Decision

Implemented **test-driven specification** for relative path handling:
- **15 comprehensive xUnit tests** validating JSON/HTML export relative path behavior
- Tests cover: basic relative paths, cross-platform normalization, nested directories, special characters, edge cases
- Tests are **intentionally failing** (by design) — they spec out what implementation needs to provide

#### Test Suite Details

**File:** `src/tests/Graphify.Tests/Export/RelativePathHandlingTests.cs`

**Test Categories:**

1. **JSON Relative Paths (3 tests)**
   - Node file paths must not be `Path.IsPathRooted()` == true
   - Multiple output locations preserve same relative path strings
   - Edge metadata paths also relative

2. **HTML Relative Paths (2 tests)**
   - Embedded JSON within HTML has relative file paths
   - `source_file` field in vis.js nodes uses relative paths

3. **Cross-Platform Normalization (3 tests)**
   - Unix (`/`) paths → relative
   - Windows (`\`) paths → relative
   - Mixed separators handled gracefully

4. **Nested & Complex Structures (2 tests)**
   - Deeply nested directories (4 levels) stay relative
   - Output dir nested within project preserves root-relative paths

5. **Special Characters (1 test)**
   - Dashes, underscores, dots handled correctly

6. **Edge Cases (4 tests)**
   - Null/empty paths (concept nodes)
   - Paths outside project root
   - Multiple exports consistency
   - All critical paths covered

#### Key Testing Patterns

- **Path validation:** `Assert.False(Path.IsPathRooted(filePath))` universally validates relative vs absolute
- **JSON inspection:** Parse exported JSON with `JsonDocument.Parse()` to verify `file_path` fields
- **HTML embedded JSON:** Search for `"source_file":"C:\\"` patterns in embedded vis.js data
- **Test isolation:** Each test creates fresh temp directory structure
- **Known gotchas:** HTML exporter overload ambiguity (use named parameters), NaN in single-node degree calc (test with 2+ connected nodes)

#### Acceptance Criteria

✅ All 15 tests pass after implementation  
✅ Relative paths consistent across export formats (JSON, HTML)  
✅ Cross-platform path normalization works on Windows and Linux  
✅ Edge cases (deeply nested dirs, external files, null paths) handled gracefully  
✅ No regressions in existing export tests

#### Status

**Pre-implementation:** 7 tests passing (non-file-path scenarios), 8 failing by design  
**Post-implementation:** 15/15 passing + 599/599 total tests passing (zero regressions)

### Decision: Relative Path Fix (Issue #2) — Core Implementation by Trinity

**Author:** Trinity (Core Dev)  
**Date:** 2026-04-07  
**Status:** Implemented & Merged  
**Related:** GitHub Issue #2, NEO APPROVAL: Relative Path Fix (Issue #2)

#### Context

Issue #2: Users report absolute paths in JSON/HTML exports break portability. Graphs must use relative paths for cross-platform and cross-machine sharing.

#### Decision

Implemented relative path tracking through the entire pipeline:

1. **Data Model (additive, zero breaking changes)**
   - `ExtractionResult.cs` — Added `string? RelativeSourceFilePath`
   - `GraphNode.cs` — Added `string? RelativePath`
   - Immutable records: new property is purely additive

2. **Pipeline Integration**
   - `FileDetector.cs` — Pre-existing; calculates relative paths from start
   - `Extractor.cs` — Passes relative paths to `ExtractionResult`
   - `SemanticExtractor.cs` — Passes relative paths to `ExtractionResult`
   - `GraphBuilder.cs` — Builds `relativePathMap`, normalizes to forward slashes (`/`), applies to nodes

3. **Export Layer (backward compatible)**
   - `JsonExporter.cs` — Uses `RelativePath ?? FilePath` fallback
   - `HtmlExporter.cs` — Uses `RelativePath ?? FilePath` fallback, fixed division-by-zero bug

#### Implementation Details

**Path Normalization:**
```csharp
// GraphBuilder.cs line 81
relativePath = relativePath.Replace('\\', '/');
```

All paths normalized to forward slashes for consistency:
- Graphs generated on Windows have identical paths on Linux/Mac
- Universal portability without platform-specific logic

**Backward Compatibility:**
```csharp
// JsonExporter.cs line 45
FilePath = n.RelativePath ?? n.FilePath,

// HtmlExporter.cs line 132
var sourceFile = node.RelativePath ?? node.FilePath ?? "";
```

Graceful fallback chain:
- If `RelativePath` set → use it (portable)
- If `RelativePath` null (concept nodes, edge cases) → fall back to `FilePath`
- Empty case → empty string (no crashes)

Existing behavior preserved; no breaking changes.

#### Edge Cases

- **Concept nodes** (null FilePath) — fallback prevents crashes
- **Files outside project root** — handled gracefully via `Path.GetRelativePath()`
- **Deeply nested directories** — normalized paths maintain structure
- **Mixed separators** — normalized to `/` for consistency

#### Architectural Review

**Reviewed by:** Neo (Lead Architect)  
**Assessment:** ✅ **EXCELLENT** (production-quality code)

- **Pipeline Integration:** Idiomatic .NET, clean flow
- **Design:** Immutable models, additive property, zero breaking changes
- **Portability:** Cross-platform tested (Unix/Windows/mixed)
- **Compatibility:** Graceful fallback, existing code continues to work
- **Testing:** 15 comprehensive tests, all passing, zero regressions
- **Bonus fix:** Division-by-zero in HtmlExporter degree calculation

#### Test Results

```
Relative Path Tests:      15 PASSED ✅
All Unit Tests:          573 PASSED ✅
All Integration Tests:    41 PASSED ✅
─────────────────────────────────────
Total:                   614 PASSED ✅
```

#### Deployment

**Commit:** c22c426 (Fix: Convert absolute paths to relative paths)  
**Branch:** main  
**Status:** ✅ Merged to origin  
**Issue Status:** ✅ Closed with user comment

#### Follow-Up Work

1. ⏭️ **Documentation**: Update README.md to mention relative path portability
2. ⏭️ **Release notes**: Call out Issue #2 fix in next release
3. ⏭️ **Optional**: Add `--absolute-paths` CLI flag if users request it

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
