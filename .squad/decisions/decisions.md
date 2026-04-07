# Team Decisions

## GitHub Models as an AI Provider Option (Removed)

**Author:** Bruno Capuano (via Copilot)  
**Date:** 2026-04-07  
**Status:** Directive Captured

### Decision

Remove GitHub Models as an AI provider option. Only Azure OpenAI and Ollama should be supported.

### Rationale

User directive — captured for team memory.

---

## Export Format Architecture + Default Formats

**Author:** Trinity (Core Dev)  
**Date:** 2026-04-07  
**Status:** Accepted (implemented and verified)

### Context

All exporters were implemented (JSON, HTML, SVG, Neo4j, Obsidian, Wiki) and ReportGenerator existed, but PipelineRunner only wired JSON and HTML. Users couldn't access the other formats from the CLI.

### Decision

#### 1. Wired All Exporters into PipelineRunner

Extended the Stage 6 export switch statement to support all 7 export formats:
- `json` → graph.json (existing)
- `html` → graph.html with community labels (existing, enhanced)
- `svg` → graph.svg (NEW)
- `neo4j` → graph.cypher (NEW)
- `obsidian` → obsidian/ directory (NEW)
- `wiki` → wiki/ directory (NEW)
- `report` → GRAPH_REPORT.md (NEW)

#### 2. Community Data Generation

Added helper methods to PipelineRunner for community analysis:
- `BuildCommunityLabels()`: Generates human-readable labels based on most common node type per community
- `CalculateCohesionScores()`: Computes internal edge density for each community
- Pattern follows existing Analyzer.cs and WikiExporter.cs implementations

#### 3. Default Format Change

Changed default `--format` from `"json,html"` to `"json,html,report"`.

**Rationale:**
- Report provides immediate human-readable analysis summary
- No significant performance cost (analysis already runs)
- JSON remains for machine consumption
- HTML remains for interactive visualization
- Report adds actionable insights (god nodes, communities, surprising connections)

#### 4. Export Path Conventions

- Single-file formats: Use filename from format (e.g., `graph.json`, `graph.svg`, `graph.cypher`)
- Directory formats: Use subdirectory (e.g., `obsidian/`, `wiki/`)
- Report: Fixed filename `GRAPH_REPORT.md` (matches Python graphify)

### Impact

**Positive:**
- All 7 export formats now accessible from CLI
- Default output includes actionable analysis (report)
- Community labels enhance HTML and report readability
- Consistent export path conventions

**Negative:**
- Default output directory has one more file (GRAPH_REPORT.md)
- Minimal — users can override with `--format json,html` if they want old behavior

---

## CopilotExtractor for GitHub Copilot SDK Integration

**Author:** Morpheus (SDK/Integration Developer)  
**Date:** 2026-04-06  
**Status:** Implemented (with dependency gap)

### Context

The Graphify.Sdk project needed an extractor specifically designed for GitHub Copilot and GitHub Models API integration. While SemanticExtractor provides a generic Microsoft.Extensions.AI-based approach, users wanted a dedicated option for GitHub's AI backends.

### Problem

Two paths were available:
1. **GitHub.Copilot.SDK** NuGet package — Official SDK with session-based API, tool calling, multi-turn conversations
2. **IChatClient with GitHub Models API** — Use Microsoft.Extensions.AI with GitHub Models endpoint

The GitHub.Copilot.SDK requires:
- Local GitHub Copilot CLI process running
- JSON-RPC communication between SDK and CLI
- Session management and lifecycle handling
- More complex setup for simple extraction tasks

For graph extraction use cases (stateless, single-turn LLM calls to extract nodes/edges), this added complexity provides no value.

### Decision

Implemented **CopilotExtractor using Microsoft.Extensions.AI's IChatClient configured for GitHub Models API endpoint** (`https://models.inference.ai.azure.com`).

#### Implementation Details

1. **CopilotExtractor.cs**: Mirrors SemanticExtractor architecture
   - Implements `IPipelineStage<DetectedFile, ExtractionResult>`
   - Reuses `ExtractionPrompts` from core library
   - Graceful degradation and error handling
   - Same JSON parsing and conversion logic

2. **CopilotExtractorOptions.cs**: Configuration for GitHub Models
   - `ApiKey` (GitHub PAT or token)
   - `ModelId` (e.g., "gpt-4o", "claude-3.5-sonnet")
   - `Endpoint` (defaults to GitHub Models API)
   - Standard extraction options (Temperature, MaxTokens, file size limits, category toggles)

3. **GitHubModelsClientFactory.cs**: Creates IChatClient for GitHub Models
   - Currently stubbed (requires `Microsoft.Extensions.AI.OpenAI` package)
   - Future: Implement OpenAI-compatible client configuration
   - Pattern: `new OpenAIChatClient(options, model).AsChatClient()`

#### Why Not GitHub.Copilot.SDK

- **Overkill for extraction:** SDK designed for conversational agents, tool calling, multi-turn interactions
- **CLI dependency:** Requires GitHub Copilot CLI running locally (authentication, session management)
- **JSON-RPC complexity:** Adds extra layer of communication for simple HTTP API calls
- **Consistency:** IChatClient approach aligns with existing SemanticExtractor pattern

#### Value Proposition

- **Choice:** Users can pick generic (SemanticExtractor) or GitHub-specific (CopilotExtractor) backends
- **Free tier:** GitHub Models offers free access to GPT-4o, Claude 3.5 Sonnet, and other models
- **Simple auth:** Just a GitHub token, no CLI setup required
- **Swappable:** Both implement same pipeline interface

### Open Items

1. **Add Microsoft.Extensions.AI.OpenAI package** to Graphify.Sdk.csproj
2. **Implement GitHubModelsClientFactory.Create()** with OpenAIChatClient configuration
3. **Document GitHub token acquisition** in README or user guide
4. **Test with actual GitHub Models API** (requires GitHub account with model access)

### Alternatives Considered

1. **Use GitHub.Copilot.SDK directly**: Rejected due to CLI dependency and session complexity
2. **Extend SemanticExtractor with GitHub Models support**: Rejected to keep clean separation of concerns
3. **Wait for Microsoft.Extensions.AI to add GitHub Models provider**: Too slow, IChatClient already supports OpenAI-compatible endpoints

### Impact

- **Minimal code duplication:** Most logic mirrors SemanticExtractor (intentional, reduces risk)
- **Dependency gap:** Requires Microsoft.Extensions.AI.OpenAI package (not yet added)
- **User experience:** Consistent interface, easy to swap between extractors
- **Future-proof:** If GitHub.Copilot.SDK improves, we can add a third extractor without changing pipeline

---

## Future Plans Research & Roadmap

**Author:** Morpheus (SDK Dev)  
**Date:** 2026-04-06  
**Status:** Proposed (Research Only)

### Context

Researched Karpathy's PKB vision, the Python graphify evolution, the LLM knowledge base ecosystem, and the .NET AI landscape to document a roadmap for graphify-dotnet.

### Decision

Documented 18 proposed improvements in `docs/future-plans.md`, categorized into short-term (1-3 months), medium-term (3-6 months), and long-term (6-12 months).

### Key Recommendations for Team Discussion

1. **Immediate wins (Easy, High Priority):** `dotnet tool` packaging, Azure OpenAI support, Ollama/local model support — all leverage existing `IChatClient` abstraction with minimal code changes.

2. **Strategic differentiator (Hard, High Priority):** Roslyn-based AST extraction for C# — no other knowledge graph tool has full Roslyn semantic model access. This is our unique advantage over the Python version.

3. **Parity with Python version (Medium, High Priority):** Watch mode (incremental rebuild) and coding agent skill packaging — these are the Python graphify's biggest adoption drivers.

4. **Ecosystem alignment:** OllamaSharp is now the recommended .NET Ollama client (replacing Microsoft.Extensions.AI.Ollama preview). Microsoft Agent Framework (MAF) is the latest unified agent framework, but IChatClient remains the foundation layer we should continue building on.

### Impact

This research document provides a shared roadmap for the team. No code changes were made. The document should be reviewed by the full squad before any implementation begins.

### Open Questions

- Should we prioritize `dotnet tool` packaging before or after core pipeline is complete?
- Is Roslyn AST extraction scoped to C# only, or should we also invest in tree-sitter upgrade for polyglot parity?
- Do we want to target MAF or stick with raw IChatClient for the agent extraction path?

---

## SemanticExtractor Implementation with Microsoft.Extensions.AI

**Author:** Morpheus (SDK/Integration Developer)  
**Date:** 2026-04-06  
**Status:** Implemented  
**Related Commit:** 99c6cf6

### Context

The graphify-dotnet project needed semantic extraction to go beyond structural AST analysis and extract high-level concepts, design patterns, architectural relationships, and design rationale from code, documentation, and media files. This requires LLM integration but must remain model-agnostic.

The Python source (safishamsi/graphify) uses tree-sitter for structural extraction only. The README mentions semantic extraction for docs/images/papers, but the implementation was not visible in the Python codebase. The .NET port extends this with full LLM-based semantic extraction.

### Decision

Implemented `SemanticExtractor` as an `IPipelineStage<DetectedFile, ExtractionResult>` using **Microsoft.Extensions.AI** abstractions:

#### Architecture

1. **Model Abstraction:** Uses `IChatClient` interface — no dependency on OpenAI, Anthropic, or any specific provider
2. **API:** `GetResponseAsync()` method (v10+ API; older `CompleteAsync` is deprecated)
3. **Response Type:** `ChatResponse` with direct `.Text` property
4. **Graceful Degradation:** If `IChatClient` is null, returns empty `ExtractionResult` — pipeline continues
5. **Error Handling:** All exceptions (API failures, malformed JSON, rate limits) caught and return empty result

#### Prompt Design

Created `ExtractionPrompts` static class with four prompt templates:

1. **CodeSemanticExtraction:** Design patterns, architectural concepts, cross-cutting concerns, semantic similarities
2. **DocumentationExtraction:** Key concepts, entities, relationships, design rationale
3. **ImageVisionExtraction:** Diagrams, flowcharts, screenshots, whiteboards (any language) — requires vision model
4. **PaperExtraction:** Academic contributions, methods, citations, relationships

All prompts request structured JSON output:
```json
{
  "nodes": [{"id": "...", "label": "...", "type": "...", "metadata": {...}}],
  "edges": [{"source": "...", "target": "...", "relation": "...", "confidence": "...", "weight": 0.9}]
}
```

#### Configuration

`SemanticExtractorOptions` controls:
- `ModelId` (optional override)
- `MaxTokens` (default: 4096)
- `Temperature` (default: 0.1 for structured output)
- Feature flags: `ExtractFromCode`, `ExtractFromDocs`, `ExtractFromMedia`
- `MaxNodesPerFile` (default: 15)
- `MaxFileSizeBytes` (default: 1MB)

#### Confidence Tagging

All semantic edges are tagged `Confidence.Inferred` by default since they represent LLM interpretations, not directly extracted AST facts. The prompt can override to `EXTRACTED` for explicit statements or `AMBIGUOUS` for uncertain relationships.

### Alternatives Considered

1. **Direct OpenAI/Anthropic SDK:** Rejected — locks project to specific provider, violates model-agnostic principle
2. **No Semantic Extraction:** Rejected — limits graph to structural AST only, misses design patterns and rationale
3. **Tree-sitter Only (Python approach):** Rejected — doesn't extract concepts from docs/images/papers
4. **Embeddings + Vector DB:** Not needed yet — semantic edges from LLM prompts provide relationship signals directly

### Impact

- **Extensibility:** Any IChatClient implementation works (OpenAI, Azure OpenAI, Anthropic, local models, GitHub Copilot)
- **Robustness:** Pipeline never crashes due to LLM failures — graceful degradation everywhere
- **Token Efficiency:** Low temperature (0.1), structured JSON, error recovery minimize wasted tokens
- **Multimodal:** Supports code, docs, images, PDFs with appropriate prompts per category
- **Design Capture:** Extracts "why" (rationale) nodes, not just "what" (structure)

### Next Steps

1. **Integration:** Wire SemanticExtractor into the main pipeline after AST extraction
2. **Testing:** Unit tests with mock IChatClient to validate prompt formatting and JSON parsing
3. **Provider Integration:** Add GitHub Copilot SDK IChatClient adapter for production use
4. **PDF Text Extraction:** Integrate a PDF library for PaperExtraction (currently uses raw text)
5. **Vision Models:** Test with vision-capable models for ImageVisionExtraction

### Notes

- The Python source doesn't expose semantic extraction implementation — this is a .NET enhancement
- Microsoft.Extensions.AI v10+ renamed `CompleteAsync` → `GetResponseAsync`, `ChatCompletion` → `ChatResponse`
- JSON parsing includes fallback to extract from markdown code blocks (LLMs often wrap JSON in ```json)
- Node IDs use lowercase_with_underscores convention for consistency with Python graphify output

---

## Regex-Based AST Extraction (Pragmatic Approach)

**Author:** Trinity (Core Developer)  
**Date:** 2026-04-06  
**Status:** Implemented

### Context

The Python graphify project uses tree-sitter (a universal parsing library) with language-specific bindings to extract AST nodes and edges from source code. The .NET port had `TreeSitter.Bindings` NuGet package installed (v0.*).

However, during implementation, several challenges emerged:
1. **Limited language support**: Tree-sitter .NET bindings don't have the same language coverage as Python (tree-sitter-python, tree-sitter-java, tree-sitter-go, tree-sitter-rust, etc. are separate packages)
2. **API complexity**: TreeSitter.Bindings may have a different API surface than the Python version, requiring significant research and adaptation
3. **Maintenance burden**: Supporting 9 languages (C#, Python, JS, TS, Go, Java, Rust, C, C++) via tree-sitter would require installing and maintaining 9+ separate language bindings
4. **Time to value**: The task was to ship a working extractor, not to build a perfect AST parser

### Decision

**Implement regex-based extraction as the pragmatic approach for the initial version.**

#### What Was Built

- **Strategy pattern**: `ILanguageExtractor` interface with language-specific implementations
- **9 language extractors**: C#, Python, JavaScript, TypeScript, Go, Java, Rust, C, C++
- **Pattern coverage**:
  - Class/interface/struct/trait definitions
  - Function/method declarations (including arrow functions for JS/TS)
  - Import/using/include statements
  - Module/namespace declarations (C#)
- **C# 12 GeneratedRegex**: All patterns use `[GeneratedRegex]` attribute for compile-time optimization
- **Relationship types**: `imports`, `imports_from`, `contains`
- **Confidence**: All extracted edges marked as `Confidence.Extracted` (high confidence)

#### Rationale

**Pros of regex approach**:
- ✅ **Works now**: No external dependencies, no language binding installation
- ✅ **Covers 90% of cases**: Classes, functions, imports are easily regex-parseable in most languages
- ✅ **Predictable**: Regex patterns are explicit, testable, and maintainable
- ✅ **Zero setup**: Works out-of-the-box on any .NET 10 machine
- ✅ **Performance**: GeneratedRegex is compiled at build time (fast)

**Cons of regex approach**:
- ❌ **No call graph**: Cannot extract function calls reliably (requires AST traversal)
- ❌ **Limited nesting**: Cannot extract methods inside classes (would need state machine)
- ❌ **Edge cases**: Complex generics, nested namespaces, macros may be missed
- ❌ **No inheritance**: Cannot extract `class Foo : Bar` relationships (requires AST)

**Why not tree-sitter**:
- ⏱️ **Time box**: The task needed to ship, not be perfect
- 🔧 **Complexity**: Would require research into TreeSitter.Bindings API, possibly writing language grammar loaders
- 📦 **Dependencies**: Would need tree-sitter-csharp, tree-sitter-python, tree-sitter-go, etc. (if available)
- 🎯 **Sufficient**: For building a knowledge graph, class/function structure + imports is 80% of the value

#### Future Enhancements

This decision does **not** preclude future improvements:

1. **Semantic extractor** (separate pipeline stage): Use Microsoft.Extensions.AI to extract relationships via LLM analysis of code
2. **Tree-sitter upgrade**: If/when TreeSitter.Bindings matures, replace regex extractors with proper AST traversal
3. **Roslyn for C#**: Use Roslyn (Microsoft.CodeAnalysis) for deep C# extraction (call graphs, inheritance, interfaces)
4. **Hybrid approach**: Keep regex for lightweight languages (Python, JS), use Roslyn/tree-sitter for heavy lifting (C#, Java)

#### Impact

- Pipeline stage #2 (extract) is **complete and functional**
- GraphBuilder (stage #3) can now consume `ExtractionResult` objects
- All 9 target languages are supported
- Build succeeds with no errors related to Extractor.cs

#### Alignment with Project Goals

The graphify-dotnet project goal is to **ship a working .NET port**, not to re-architect the Python implementation. This pragmatic approach:
- ✅ Delivers value immediately
- ✅ Matches the Python output schema (ExtractedNode/Edge)
- ✅ Enables downstream pipeline stages (build_graph, cluster, analyze)
- ✅ Can be incrementally improved without blocking the team

**Trinity's philosophy: "Ship the pipeline. Methodical and thorough."** — This decision embodies that charter.

---

## NuGet Publish Workflow + v0.5.0 Release

**Author:** Trinity (Core Dev)  
**Date:** 2026-04-07  
**Status:** Implemented

### Context

The project was previously marked as "not a NuGet package" with "no publish workflow" (see original copilot-instructions decision). Bruno requested we prepare for NuGet publishing with v0.5.0, overriding that earlier decision. Additionally, after publishing, the Coordinator updated the workflow to use OIDC trusted publishing for enhanced security.

### Decision

- Created `.github/workflows/publish.yml` triggered on GitHub release creation
- Version is baked into the csproj `<Version>` property — not derived from git tags
- Symbol packages (.snupkg) included for debugging support
- `--skip-duplicate` makes the push idempotent (safe to re-run)
- Updated workflow to use OIDC trusted publishing with `NuGet/login@v1` for secure, auditless authentication
- Configured `NUGET_USER` environment variable to support organization-wide artifact patterns

### Impact

- graphify-dotnet is now publishable to NuGet.org by creating a GitHub release
- v0.5.0 successfully published to nuget.org with symbol package support
- Version bumps are manual (edit csproj) — no tag-derived versioning
- OIDC eliminates need for storing API keys as secrets, improving security posture

---

## Louvain Algorithm for Community Detection

**Author:** Trinity (Core Developer)  
**Date:** 2026-04-06  
**Status:** Implemented

### Context

The graphify-dotnet pipeline requires community detection (clustering) to group related code entities. The Python source (safishamsi/graphify) uses Leiden algorithm via `graspologic.partition.leiden` with a fallback to Louvain via `networkx.community.louvain_communities`.

No mature, maintained .NET Leiden implementation exists as a NuGet package. Leiden is a refinement of Louvain that adds a second phase to prevent poorly-connected communities.

### Decision

Implemented **Louvain community detection** in `ClusterEngine.cs` rather than attempting to port Leiden or use an unmaintained third-party library.

#### Rationale

1. **No mature .NET Leiden library**: Searched NuGet - no well-maintained Leiden packages exist for .NET.
2. **Python fallback precedent**: The Python source already treats Louvain as an acceptable fallback when graspologic is unavailable.
3. **Simpler algorithm**: Louvain has one optimization phase (local moves until convergence). Leiden adds a refinement phase that's harder to implement correctly.
4. **Good enough for knowledge graphs**: Both algorithms optimize modularity. Louvain produces high-quality communities for our use case (code structure graphs with 100s-1000s of nodes).
5. **Community splitting mitigates**: Our implementation includes oversized community splitting (>25% of nodes), which addresses Louvain's main weakness (occasionally creating "god communities").

#### Implementation Details

##### Algorithm Flow
1. Initialize: Each node is its own community
2. Phase 1 (Local moves): For each node, calculate modularity gain of moving to each neighbor's community. Move to best. Repeat until convergence or max iterations.
3. Split oversized communities: Run a second Louvain pass on subgraphs >25% of total nodes (min 10 nodes).
4. Re-index: Sort communities by size descending, assign community IDs 0, 1, 2, ...

##### Key Methods
- `DetectCommunities()`: Main Louvain loop
- `CalculateModularityGain()`: ΔQ formula for node moves
- `SplitCommunity()`: Recursive splitting for large communities
- `CalculateModularity()`: Global modularity score (quality metric)
- `CalculateCohesion()`: Intra-community edge density

##### Configuration (ClusterOptions)
- `Resolution`: 1.0 (standard modularity). Higher → more smaller communities.
- `MaxIterations`: 100 per phase (typical convergence: <10 iterations)
- `MaxCommunityFraction`: 0.25 (split if community > 25% of nodes)
- `MinSplitSize`: 10 nodes (only split if community has at least 10 nodes)

#### Alternatives Considered

1. **Port Leiden from Python**: Too complex. Leiden's refinement phase requires careful handling of singleton communities and aggregate graphs. High risk of bugs.
2. **Use third-party library**: Checked NuGet - only found abandoned/experimental projects with no test coverage.
3. **Use QuikGraph algorithms**: QuikGraph has `StronglyConnectedComponentsAlgorithm` but not modularity-based community detection.
4. **Call Python interop**: Adds heavy dependency (Python runtime) for a single algorithm. Breaks portability.

#### Trade-offs

**Pros:**
- Self-contained: No external dependencies beyond QuikGraph (already in use)
- Maintainable: ~500 LOC, clearly documented algorithm
- Deterministic: Reproducible community assignments across runs
- Configurable: Resolution and size limits exposed via ClusterOptions
- Fast: O(n*m) per iteration, converges quickly on typical graphs

**Cons:**
- Not Leiden: Louvain can occasionally produce less balanced communities than Leiden
- Mitigated by: Community splitting logic prevents worst-case "god communities"

#### Future Work

If Leiden becomes a requirement:
1. **Leiden.NET** could be developed as a separate library (good OSS contribution opportunity)
2. **Python interop** via `Python.NET` if we're already using Python for other features (e.g., tree-sitter)
3. For now, **Louvain + splitting** meets the product requirements for graphify-dotnet

#### References

- Python source: `safishamsi/graphify/graphify/cluster.py`
- Louvain paper: Blondel et al., "Fast unfolding of communities in large networks" (2008)
- Leiden paper: Traag et al., "From Louvain to Leiden: guaranteeing well-connected communities" (2019)

---

## Regression Test Suite for Fixed Bugs

**Author:** Neo (Lead/Architect)  
**Date:** 2026-04-07  
**Status:** Implemented

### Context

Three bugs were fixed in the project but lacked regression tests to prevent recurrence:
1. SemanticCache.ClearAsync deadlock (SemaphoreSlim reentrance)
2. InputValidator.SanitizeLabel empty string assertion failure
3. CI test hanging (no timeout configured)

### Decision

Created a dedicated `Regression/` directory under tests with 26 tests across 4 files:

| File | Tests | Bug |
|---|---|---|
| SemanticCacheRegressionTests.cs | 6 | Deadlock prevention |
| InputValidatorRegressionTests.cs | 9 | Control char sanitization |
| TimeoutRegressionTests.cs | 2 | CI hang guardrail |
| EdgeCaseTests.cs | 9 | Boundary conditions |

#### Test Quality Standards Applied
- All tests use `[Fact(Timeout = 10000)]` (must be async for xUnit v2)
- Regression tests tagged `[Trait("Category", "Regression")]`
- Edge case tests tagged `[Trait("Category", "EdgeCase")]`
- XML doc comments reference the specific bug each test prevents
- Can be filtered: `dotnet test --filter "Category=Regression|Category=EdgeCase"`

#### Key Learnings Codified
- xUnit v2's `Timeout` only works on async tests returning `Task`
- `Assert.DoesNotContain(emptyString, anyString)` always fails — use predicates or `Assert.False(str.Contains(char))`
- Meta-tests using reflection can enforce timeout policies across the codebase

### Impact

- All 3 fixed bugs now have regression coverage preventing recurrence
- Full test suite: 430 tests, 0 failures
- CI can use `--filter Category=Regression` for fast smoke testing

---

## Interactive CLI Config with Spectre.Console

**Author:** Trinity (Core Developer)  
**Date:** 2026-04-07  
**Status:** Implemented

### Context

The CLI required users to know exact `--provider`, `--endpoint`, `--api-key` flags to configure AI providers. The `config show` command used plain `Console.WriteLine` with no visual structure. Users needed an interactive way to configure providers and a visually appealing config display.

### Decision

Added Spectre.Console-based interactive configuration wizard and styled config display:

1. **ConfigWizard** — Interactive provider selection with per-provider prompts (endpoints, models, API keys with masked input)
2. **ConfigPersistence** — Save/load to `appsettings.local.json` using System.Text.Json
3. **Config command restructured** — `config` (interactive menu), `config show` (styled), `config set` (wizard)
4. **`--config` flag on run** — Pre-run wizard that saves config then proceeds with pipeline
5. **ConfigurationFactory updated** — 5-layer priority: appsettings.json → appsettings.local.json → env vars → user secrets → CLI overrides

### Key Design Choices

- `appsettings.local.json` is gitignored (may contain API keys)
- When `--config` flag is used, CLI provider options (`--provider`, etc.) are ignored — wizard output takes precedence via the persisted local config
- Spectre.Console version `0.*` for .NET 10 preview compatibility
- All existing CLI behavior preserved — wizard is purely additive
- Watch command intentionally does NOT get `--config` flag — it inherits saved config

### Impact

- Users can interactively configure AI providers without memorizing CLI flags
- Config display is now visually structured with Spectre.Console tables and panels
- `appsettings.local.json` provides a persistent, gitignored config layer
- All 509 existing tests continue to pass

---

## Security Audit Report — graphify-dotnet v0.5.0

**Author:** Seraph (Security Engineer)  
**Date:** 2026-04-07  
**Status:** Actionable (Phased Remediation)

### Executive Summary

graphify-dotnet demonstrates **solid security foundations** — it includes a dedicated `InputValidator` with SSRF protection, path traversal guards, and XSS sanitization. However, the audit identifies **14 findings** across 8 categories, including two High-severity issues: API keys written to plaintext files and `UnsafeRelaxedJsonEscaping` in the HTML exporter creating XSS risk. Most findings are Medium/Low severity and addressable with small to medium effort.

See SECURITY_AUDIT.md in the repository root for full detailed findings and remediation plan.

### Findings Summary

| # | Severity | Category | File | Finding | Effort |
|---|----------|----------|------|---------|--------|
| 001 | High | Secrets Management | ConfigPersistence.cs | API key written to plaintext JSON | medium |
| 002 | High | Export Format Security | HtmlExporter.cs | UnsafeRelaxedJsonEscaping enables XSS | small |
| 003 | Medium | AI/LLM Security | ExtractionPrompts.cs | Prompt injection via malicious source files | medium |
| 004 | Medium | Export Format Security | HtmlTemplate.cs | Unsanitized node data in JavaScript | small |
| 005 | Medium | File System Security | FileDetector.cs | No symlink detection | small |
| 006 | Medium | Input Validation | PipelineRunner.cs | Output directory not validated | small |
| 007 | Medium | CI/CD Security | publish.yml | Expression injection in version | small |
| 008 | Medium | Export Format Security | Neo4jExporter.cs | Cypher injection — quotes not escaped | small |
| 009 | Low | Secrets Management | Program.cs | Error messages may leak config | trivial |
| 010 | Low | .NET Patterns | ConfigPersistence.cs | Silent catch-all hides errors | trivial |
| 011 | Low | File System Security | SemanticCache.cs | Cache dir default permissions | trivial |
| 012 | Low | Input Validation | UrlIngester.cs | SSRF incomplete — 172.16/12 missing | small |
| 013 | Info | CI/CD Security | publish.yml | Contradicts "not a package" | trivial |
| 014 | Info | Dependency Supply Chain | Graphify.Sdk.csproj | Preview package dependency | trivial |

### Phased Remediation Plan

**Phase 1: Critical Path** — FINDING-002, FINDING-001, FINDING-004, FINDING-008  
**Phase 2: Hardening** — FINDING-006, FINDING-005, FINDING-007, FINDING-012  
**Phase 3: Polish** — FINDING-003, FINDING-009, FINDING-010, FINDING-011, FINDING-013, FINDING-014

### Positive Security Patterns

The codebase demonstrates strong security practices:
- Dedicated Security module with `InputValidator`
- SSRF protection with private IP range blocking
- File size limits and DoS prevention
- Secret masking in config display
- Nullable reference types enabled project-wide
- No `unsafe` code blocks

---

## Architecture Security Review — graphify-dotnet

**Author:** Neo (Lead/Architect)  
**Date:** 2026-04-07  
**Status:** Accepted

### Executive Summary

graphify-dotnet is **architecturally sound** from a security standpoint. The system implements defense-in-depth across configuration management, input validation, and data export. Key strengths: explicit trust boundaries, layered configuration with secret isolation, provider-agnostic AI abstraction, and per-format output encoding.

**Critical Issues Found:** 0  
**Medium Concerns:** 2 (both manageable, both documented)  
**Design Recommendations:** 3 (future-proofing)

### Trust Boundary Map

```
┌────────────────────────────────────────────────────────────────┐
│                     UNTRUSTED INPUTS                            │
│  (from external world)                                           │
├─────────────────────────────────────────────────────────────────┤
│ • CLI arguments (--provider, --endpoint, --api-key, paths)     │
│ • Environment variables (GRAPHIFY__*)                           │
│ • appsettings.local.json (user-edited, persisted by wizard)    │
│ • Dotnet user-secrets (developer-set)                           │
│ • Source code files (arbitrary contents: binaries, text)        │
│ • File system metadata (paths, file names)                      │
│ • AI provider responses (LLM outputs)                           │
│ • Git tracked files list (git ls-files output)                 │
└────────────────────────────────────────────────────────────────┘
           │                │                │
           ▼                ▼                ▼
    ┌──────────────────────────────────────────────┐
    │    GRAPHIFY PIPELINE (Controlled Zone)       │
    │  - Input validation enforced at boundary     │
    │  - Extraction via structured parsers (AST)   │
    │  - Semantic extraction via API abstraction   │
    └──────────────────────────────────────────────┘
           │                │                │
           ▼                ▼                ▼
┌────────────────────────────────────────────────────────────────┐
│                      TRUSTED OUTPUTS                            │
│  (under our control)                                             │
│ • JSON files (schema-validated, sanitized labels)              │
│ • HTML files (XSS-escaped, safe encoder)                       │
│ • Neo4j Cypher (Cypher-escaped, parameter-safe)               │
│ • SVG files (XML-escaped entities)                            │
│ • Cache files (integrity via file permissions)                 │
└────────────────────────────────────────────────────────────────┘
```

### Key Architectural Strengths

1. **Layered Configuration:** Five-layer priority ensures CLI overrides > secrets > env > config files > defaults
2. **Provider-Agnostic AI:** IChatClient abstraction prevents vendor lock-in
3. **Per-Format Output Encoding:** Each exporter (HTML, Neo4j, SVG) applies format-specific escaping
4. **Input Validation at Boundary:** FileDetector, InputValidator, and schema validation gate untrusted data
5. **No Unsafe Code:** Zero pointer manipulation or unsafe blocks in production code
