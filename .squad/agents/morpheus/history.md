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

