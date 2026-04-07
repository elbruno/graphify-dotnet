# Project Context

- **Owner:** Bruno Capuano
- **Project:** graphify-dotnet — A .NET 10 port of safishamsi/graphify, a Python AI coding assistant skill that reads files, builds a knowledge graph, and surfaces hidden structure. Uses GitHub Copilot SDK and Microsoft.Extensions.AI for semantic extraction.
- **Stack:** .NET 10, C#, GitHub Copilot SDK, Microsoft.Extensions.AI, Roslyn (AST parsing), xUnit, NuGet
- **Source:** https://github.com/safishamsi/graphify — Python pipeline: detect → extract → build_graph → cluster → analyze → report → export. Uses NetworkX, tree-sitter, Leiden community detection, vis.js.
- **Created:** 2026-04-06

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

### 2026-04-06: SemanticExtractor Implementation

**What:** Implemented SemanticExtractor using Microsoft.Extensions.AI (IChatClient) for LLM-based semantic extraction of concepts and relationships from code, docs, and media files.

**Key Decisions:**
- Used `IChatClient` interface from Microsoft.Extensions.AI for model-agnostic LLM calls (no dependency on specific providers)
- API changed from `CompleteAsync` to `GetResponseAsync` in recent versions (v10+)
- Response type is `ChatResponse` with direct `.Text` property (not `.Message.Text`)
- Designed prompts to return structured JSON: `{ "nodes": [...], "edges": [...] }`
- All semantic relationships tagged as INFERRED by default (since they're interpretations, not direct AST)
- Graceful degradation: if IChatClient is null or errors occur, return empty ExtractionResult (pipeline continues)
- Error handling wraps API failures, malformed JSON, rate limits — never crashes pipeline

**Prompt Strategy:**
- **Code:** Extract design patterns, architectural concepts, cross-cutting concerns, semantic similarities
- **Docs:** Extract concepts, entities, relationships, design rationale
- **Images:** Vision-capable prompts for diagrams, flowcharts, screenshots, whiteboards (any language)
- **Papers:** Extract contributions, methods, citations, relationships

**Why Not Python Semantic Extraction:**
The Python source (safishamsi/graphify) uses tree-sitter for structural extraction only. The .NET port adds LLM-based semantic extraction as an enhancement, enabling deeper concept extraction beyond AST.

### 2026-04-06: CopilotExtractor Implementation

**What:** Implemented CopilotExtractor in Graphify.Sdk as an alternative to SemanticExtractor, specifically designed for GitHub Copilot/GitHub Models API integration.

**Key Decisions:**
- **Wrapper pattern over SemanticExtractor:** CopilotExtractor mirrors SemanticExtractor's architecture but targets GitHub Models API endpoint (https://models.inference.ai.azure.com)
- **Reuses ExtractionPrompts:** Both extractors share the same prompt engineering strategy from the core library
- **GitHub Models via Microsoft.Extensions.AI:** Instead of using the GitHub.Copilot.SDK (which requires CLI dependency and JSON-RPC), we use IChatClient configured for GitHub Models API endpoint
- **Factory pattern:** GitHubModelsClientFactory creates IChatClient instances configured for GitHub Models (placeholder until Microsoft.Extensions.AI.OpenAI package is added)
- **Identical behavior:** Same graceful degradation, error handling, and output format as SemanticExtractor
- **Configuration options:** CopilotExtractorOptions includes ApiKey, ModelId, Endpoint, Temperature, MaxTokens, and extraction toggles

**GitHub Copilot SDK vs IChatClient:**
- **GitHub.Copilot.SDK** exists as a NuGet package but requires local GitHub Copilot CLI process running (JSON-RPC communication, session-based, tool calling support)
- **Microsoft.Extensions.AI approach** is simpler for extraction use cases: direct HTTP API calls, no CLI dependency, model-agnostic abstraction
- **Decision:** Use IChatClient with GitHub Models API endpoint for cleaner integration and consistency with existing SemanticExtractor

**Missing dependency:**
- Requires `Microsoft.Extensions.AI.OpenAI` package to create OpenAI-compatible clients
- Currently stubbed with NotImplementedException in GitHubModelsClientFactory
- Future work: Add package reference and implement OpenAIChatClient configuration

**Value proposition:**
- Users can choose between generic semantic extraction (any IChatClient provider) or GitHub Models-specific extraction
- GitHub Models offers free tier access to GPT-4, Claude, and other models via GitHub token
- Consistent interface (IPipelineStage<DetectedFile, ExtractionResult>) allows easy swapping

### 2026-04-06: MCP Stdio Server Implementation

**What:** Implemented MCP (Model Context Protocol) stdio server in Graphify.Mcp that exposes knowledge graph operations via the ModelContextProtocol NuGet package.

**Key Decisions:**
- **ModelContextProtocol NuGet (v0.*):** Used official C# SDK for MCP with stdio transport for JSON-RPC communication over stdin/stdout
- **Tool auto-discovery:** Decorated GraphTools methods with `[McpServerTool]` and `[Description]` attributes for automatic registration via `WithToolsFromAssembly()`
- **Hosting infrastructure:** Added Microsoft.Extensions.Hosting (v10.*) and Microsoft.Extensions.DependencyInjection (v10.*) for proper DI and lifecycle management
- **Graph loading:** Loads pre-built KnowledgeGraph from JSON file (from JsonExporter output) at startup, registers as singleton
- **Logging to stderr:** Configured console logging to stderr only (stdout reserved for MCP JSON-RPC protocol)

**Implemented Tools:**
1. **query** — Search nodes/edges by term (ID, label, type), returns matching nodes with degree and connections (limit: 10)
2. **path** — Find shortest path between two nodes using BFS algorithm, returns path nodes and length
3. **explain** — Explain a node's role, connections, and statistics (incoming/outgoing edges, degree, community)
4. **communities** — List all communities or query specific community members, ordered by degree
5. **analyze** — Run graph analysis: top nodes by degree, node types distribution, relationship types, isolated nodes, insights

**Path Finding:**
- Initially attempted QuikGraph's `UndirectedBreadthFirstSearchAlgorithm` but encountered type mismatches (needs IUndirectedGraph, we have BidirectionalGraph)
- Simplified to manual BFS implementation using KnowledgeGraph.GetNeighbors() — works for directed graphs, cleaner code
- Returns full path with node IDs, labels, and types in JSON format

**Design Patterns:**
- **Pragmatic MCP adoption:** Checked API surface of ModelContextProtocol package before implementation, adapted to actual SDK patterns (not assumptions)
- **JSON-only tool responses:** All tools return JSON strings (not objects) for consistent MCP message format
- **Error handling in tools:** Each tool catches exceptions and returns JSON error responses instead of throwing
- **File-scoped types:** Used file-scoped `GraphJsonData` record for internal graph deserialization

**Usage:**
```bash
Graphify.Mcp.exe <path-to-graph.json> [--verbose]
```

**Why MCP:**
- Standardizes tool exposure for LLM agents (Claude Desktop, Copilot, VS Code)
- stdio transport enables local embedding in agent workflows
- Clean separation: graph operations live in core library, MCP bindings are thin wrapper layer

**Next Steps:**
- Add JsonExporter implementation to generate graph.json files from KnowledgeGraph
- Consider adding resource endpoints for graph metadata and statistics
- Add prompt templates for common graph queries

### 2026-04-06: Future Plans Research

**What:** Researched Karpathy's PKB vision, Python graphify evolution, LLM knowledge base ecosystem, and .NET AI landscape. Documented 18 proposed improvements in `docs/future-plans.md`.

**Key Findings:**

1. **Karpathy's PKB pattern is now mainstream** — LLMs as "knowledge compilers" ingesting raw data into structured Markdown wikis. At PKB scale (~400K words), RAG/vector DBs are unnecessary; structured text + indexes suffice.

2. **Python graphify has evolved significantly** — Watch mode (auto-rebuild on file change), 70x token efficiency, coding agent skill packaging for Claude Code/Codex, and PyPI distribution. We should match parity on watch mode and agent skill packaging.

3. **The ecosystem is converging on graph + LLM + local-first** — awesome-llm-knowledge-bases, AnythingLLM, knowledge-base-builder, cognee all validate our approach. graphify-dotnet is uniquely positioned as the .NET-native option.

4. **.NET AI stack is maturing fast** — Microsoft Agent Framework (MAF) is the newest unified framework on IChatClient. OllamaSharp replaces Microsoft.Extensions.AI.Ollama as the recommended Ollama client. Aspire 9.4 has interactive dashboards with custom commands.

5. **Tree-sitter .NET bindings are production-ready** — TreeSitter.DotNet (28+ grammars) and TreeSitterLanguagePack (248+ parsers) offer massive multi-language coverage. Upgrade from TreeSitter.Bindings is straightforward.

6. **Roslyn is our unique advantage for C#** — No other knowledge graph tool has full Roslyn semantic model access. This gives type-safe extraction, call graph analysis, and DI registration detection that regex can never match.

7. **Highest priority items:** dotnet tool packaging (Easy), Azure OpenAI + Ollama support (Easy), watch mode (Medium), Roslyn AST for C# (Hard), coding agent skill packaging (Medium).

### 2026-04-06: Blog Post & Marketing Sprint (Neo + Morpheus Collaboration)

**What:** Created comprehensive marketing content package for graphify-dotnet: blog post, image generation prompts, and documented future plans.

**Artifacts Created:**
1. **docs/blog-post.md** — 800+ word dev blog post (first-person, Bruno's conversational style) with hook (Karpathy + @socialwithaayan), explanation, ASCII pipeline diagram, 4 code samples, features, architecture, image placeholders, Future Plans section, CTA, author sign-off.
2. **docs/image-prompts.md** — 8 AI image generation prompts (4×16:9, 4×1:1) for blog heroes and social media, covering pipeline architecture, visualization, knowledge concepts, developer experience, Karpathy-inspired aesthetics.
3. **docs/future-plans.md** — 18 proposed improvements, categorized short/medium/long-term, with ecosystem research and team discussion prompts.

**Key Integration Points:**
- Blog leverages image prompts with HTML comment references for full prompt text
- Future Plans section in blog links to detailed roadmap document
- Image placeholders ready for DALL-E/Midjourney/Midjourney generation
- Links verified: Karpathy tweet, @socialwithaayan tweet, graphify-dotnet repo
- Author sign-off matches elbruno.com style (blog, YouTube, LinkedIn, Twitter, Podcast)

**Decision Routing:**
- Future Plans documented as formal decision in `.squad/decisions.md` (status: Proposed, Research Only)
- Marketing artifacts ready for team review and external distribution
- Image prompts ready for designer/agency hand-off

### 2026-04-06: Azure OpenAI + Ollama Provider Factories (Features 2 & 3)

**What:** Implemented multi-provider IChatClient support in Graphify.Sdk — Azure OpenAI, Ollama/local models, and fixed the existing GitHub Models stub.

**Key Decisions:**
- **Azure OpenAI:** Uses `Azure.AI.OpenAI` (2.*) + `Microsoft.Extensions.AI.OpenAI` (10.*). Factory creates `AzureOpenAIClient` with API key credential, gets deployment-specific `ChatClient`, then calls `.AsIChatClient()` for the standard abstraction.
- **GitHub Models (fixed):** Uses `OpenAI` client library (pulled in by Microsoft.Extensions.AI.OpenAI) with custom endpoint pointing to `https://models.inference.ai.azure.com`. Same `.AsIChatClient()` pattern. Replaced the `NotImplementedException` stub.
- **Ollama:** Uses `OllamaSharp` (5.*). `OllamaApiClient` implements `IChatClient` natively — no `.AsIChatClient()` extension needed. Just construct and return.
- **Unified factory:** `ChatClientFactory.Create(AiProviderOptions)` dispatches to the correct provider factory via pattern match on `AiProvider` enum. Validates required fields per provider (e.g., AzureOpenAI needs Endpoint + ApiKey + DeploymentName).
- **Options as records:** `AzureOpenAIOptions` and `OllamaOptions` are immutable records. `AiProviderOptions` is a flat record covering all providers with nullable fields.

**Package Versions Added:**
- `Azure.AI.OpenAI` 2.*
- `Microsoft.Extensions.AI.OpenAI` 10.*
- `OllamaSharp` 5.*

**API Surface:**
- `OllamaSharp.OllamaApiClient` implements `IChatClient` directly (no wrapper needed)
- `OpenAI.OpenAIClient` + `Microsoft.Extensions.AI.OpenAI` provides `.AsIChatClient()` extension on `ChatClient`
- `Azure.AI.OpenAI.AzureOpenAIClient` inherits from `OpenAIClient`, so same extension chain works

**Files Created:**
- `AzureOpenAIOptions.cs` — Config record for Azure OpenAI
- `AzureOpenAIClientFactory.cs` — Factory using Azure.AI.OpenAI
- `OllamaOptions.cs` — Config record with sensible defaults (localhost:11434, llama3.2)
- `OllamaClientFactory.cs` — Factory using OllamaSharp
- `ChatClientFactory.cs` — Unified factory + AiProvider enum + AiProviderOptions record

**Files Modified:**
- `Graphify.Sdk.csproj` — Added 3 new package references
- `GitHubModelsClientFactory.cs` — Replaced NotImplementedException with working OpenAI client code

### 2026-04-06: Export Format Documentation (7 Guides + Overview)

**What:** Created comprehensive user-facing documentation for all 7 export formats supported by graphify-dotnet CLI.

**Artifacts Created:**
1. **docs/export-formats.md** — Overview with format comparison table, quick-start examples, and routing guide for choosing the right format
2. **docs/format-html.md** — Interactive vis-network viewer; click nodes, search, filter, zoom/pan; best for exploration and presentations
3. **docs/format-json.md** — Machine-readable graph data; JSON schema, loading examples (C#/JS/Python), jq piping, CI/CD integration
4. **docs/format-svg.md** — Static vector image; embed in docs, print to PDF, convert to PNG; for documentation and offline viewing
5. **docs/format-neo4j.md** — Cypher script for Neo4j import; advanced queries, shortest paths, cycle detection, bulk import examples
6. **docs/format-obsidian.md** — Personal knowledge vault; wikilinks, YAML frontmatter, Obsidian plugins, knowledge management workflows
7. **docs/format-wiki.md** — Team documentation site; agent-crawlable structure, community pages, god node analysis, GitHub Pages hosting
8. **docs/format-report.md** — Human-readable analysis; god nodes, surprising connections, communities, suggested questions, metrics

**Key Decisions:**
- **Consistent structure:** Each format guide follows: title, quick-start, what it produces, how to use, features, best for, examples, customization, size/limitations, cross-references
- **Audience-specific:** HTML for visual explorers; JSON for developers; SVG for documentarians; Neo4j for analysts; Obsidian for knowledge workers; Wiki for teams; Report for everyone
- **Practical examples:** Showed CLI commands, code samples (C#, JavaScript, Python, bash), workflow patterns
- **Cross-linking:** All guides link to overview and related formats; overview table shows format names, outputs, use cases, interactivity levels
- **Default formats:** Documented json, html, report as defaults; show multi-format combinations for different goals
- **Format comparison:** Overview table and "Choosing Multiple Formats" section help users select the right export strategy

**Rationale:**
The CLI supports 7 distinct export formats serving different use cases. Users needed clear guidance on:
- What each format produces
- When to use each one
- How to access and manipulate the output
- Real-world workflows and integrations
- Performance characteristics and limitations

Documentation reduces user friction and enables full feature adoption.

**Documentation Style:**
- Concise (100–200 lines per guide)
- Practical (CLI examples, code samples, real workflows)
- Inclusive ("Quick Start" top of each, "See Also" linking structure)
- User-centric (guides organized by use case, not just technical features)
- Markdown with code blocks, tables, and inline formatting for GitHub/web presentation


