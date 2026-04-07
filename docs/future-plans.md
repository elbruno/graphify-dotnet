# graphify-dotnet — Future Plans & Roadmap

> **Author:** Morpheus (SDK Dev)
> **Date:** 2026-04-06
> **Status:** Research Document — No implementations. Proposals only.

---

## 1. Karpathy's Vision: The Origin

[Andrej Karpathy's tweet](https://x.com/karpathy/status/2039805659525644595) outlined a paradigm shift: using LLMs not just for Q&A, but as **knowledge compilers** — tireless librarians that ingest raw data and produce structured, navigable knowledge.

### The PKB (Personal Knowledge Base) Workflow

```
raw/ folder        →  LLM "compilation"  →  Markdown wiki  →  Obsidian as IDE
(papers, code,        (summarize, link,      (structured,      (graph view,
 images, tweets)       backlink, index)       interlinked)      search, navigate)
                                                    ↓
                                              Q&A & search  →  New outputs
                                              (LLM queries      (slides, reports,
                                               its own wiki)     new articles)
```

### Key Insights from Karpathy

1. **LLM as compiler, not assistant** — Raw data goes in, structured knowledge comes out. The LLM does the filing, linking, and organizing autonomously.
2. **Obsidian as the IDE for knowledge** — Just as VS Code is the IDE for code, Obsidian (with its graph view and backlinks) becomes the IDE for navigating compiled knowledge.
3. **No RAG needed at PKB scale** — At ~100 articles / 400K words, structured Markdown with indexes is sufficient. RAG and vector DBs are only needed at much larger scale.
4. **Health checks for consistency** — LLMs run periodic "lint" passes over the wiki to fix broken links, merge duplicates, and ensure consistency.
5. **Knowledge manipulation > code manipulation** — The fundamental shift: we're moving from manipulating instructions (code) to manipulating understanding (knowledge graphs).
6. **Minimal human intervention** — Humans steer direction; LLMs handle the tedium of organizing, linking, and maintaining.

---

## 2. Ecosystem Analysis

### 2.1 Python graphify (safishamsi/graphify) — Current State

The original Python project has evolved significantly since inspiring this .NET port:

- **Two-pass architecture**: Deterministic AST extraction (tree-sitter) + parallel LLM subagents (Claude) for semantic relationships
- **Auto-rebuild / watch mode**: File watcher that rebuilds the graph on code changes without requiring an LLM
- **Token efficiency**: 70x fewer tokens per query vs. reading raw files — critical for cost-effective LLM usage
- **Vis.js interactive HTML**: Click-through graph visualization with community filtering and search
- **Coding agent skill**: Available as a drop-in skill for Claude Code, Codex, OpenCode, and OpenClaw
- **PyPI distribution**: `pip install graphifyy` (namespace reclaim in progress)
- **Confidence tagging**: EXTRACTED / INFERRED / AMBIGUOUS on every relationship (we already have this in our schema)

**What we can learn:** Watch mode, agent skill packaging, and token efficiency benchmarks are features we should prioritize.

### 2.2 Emerging PKB/Knowledge Base Ecosystem

The Karpathy tweet spawned a wave of tools and curated resources:

| Project | Description | Relevance to graphify-dotnet |
|---------|-------------|------------------------------|
| [awesome-llm-knowledge-bases](https://github.com/SingggggYee/awesome-llm-knowledge-bases) | Curated list: ingestion, wiki compilation, linting, RAG, agents | Reference for ecosystem positioning |
| [awesome-llm-knowledge-systems](https://github.com/kennethlaw325/awesome-llm-knowledge-systems) | RAG, context engineering, agent memory, MCP | Architecture inspiration for MCP integration |
| [AnythingLLM](https://github.com/Mintplex-Labs/anything-llm) | Local/private ChatGPT with personal KB, workspace-based RAG | Shows demand for local-first, private knowledge systems |
| [knowledge-base-builder](https://github.com/kostadindev/knowledge-base-builder) | Transform docs to structured Markdown via LLM summarization | Validates the "LLM as compiler" pattern |
| [cognee](https://www.cognee.ai/) | Python repo → knowledge graph pipeline | Direct competitor in the code-to-graph space |

**Key trend:** The community is converging on **graph-structured knowledge + LLM compilation + local-first privacy**. graphify-dotnet is well-positioned in this space as the .NET native option.

### 2.3 .NET AI Ecosystem Evolution

The .NET AI landscape has matured rapidly:

- **Microsoft.Extensions.AI** is now the foundation layer (`IChatClient`, `IEmbeddingGenerator`) — we already use this
- **Semantic Kernel** sits above ME.AI as an orchestration layer with planning, memory, and multi-agent workflows
- **Microsoft Agent Framework (MAF)** is the newest unified framework, built directly on `IChatClient`
- **OllamaSharp** is the recommended .NET Ollama client (replacing `Microsoft.Extensions.AI.Ollama` preview)
- **.NET Aspire 9.4** offers interactive dashboards with custom commands, OTEL metrics, and AI integration
- **Tree-sitter .NET bindings** have matured: `TreeSitter.DotNet` (28+ grammars) and `TreeSitterLanguagePack` (248+ languages)

---

## 3. Proposed Improvements

### Short-Term (1-3 months) — Quick Wins

#### 3.1 `dotnet tool` Global Install

- **Description:** Package Graphify.Cli as a global .NET tool so users can install with `dotnet tool install -g graphify` and run with just `graphify run .`
- **Rationale:** Eliminates the clone-and-build workflow. The Python version is on PyPI (`pip install graphifyy`); we need parity. Global tools are the standard .NET distribution mechanism for CLI apps.
- **How:** Add `<PackAsTool>true</PackAsTool>` and `<ToolCommandName>graphify</ToolCommandName>` to Graphify.Cli.csproj. Consider Native AOT for self-contained packaging.
- **Difficulty:** Easy
- **Priority:** High

#### 3.2 Azure OpenAI Support

- **Description:** Add IChatClient configuration for Azure OpenAI endpoints alongside the existing AI provider support.
- **Rationale:** Enterprise users often have Azure OpenAI deployments with managed access, private endpoints, and compliance guarantees. This is table-stakes for enterprise adoption.
- **How:** Use `Microsoft.Extensions.AI.OpenAI` with Azure OpenAI endpoint configuration. The `IChatClient` abstraction means this is mostly configuration, not new extraction logic.
- **Difficulty:** Easy
- **Priority:** High

#### 3.3 Ollama / Local Model Support

- **Description:** Support local LLM inference via Ollama for offline, private, and cost-free semantic extraction.
- **Rationale:** Aligns with Karpathy's vision of local-first knowledge systems. Privacy-sensitive codebases (healthcare, finance, defense) cannot send code to cloud APIs. OllamaSharp is now the recommended .NET Ollama client and implements `IChatClient` natively.
- **How:** Add `OllamaSharp` NuGet package. Configure `IChatClient` with `new OllamaApiClient(new Uri("http://localhost:11434/"), modelId)`. Same extraction pipeline, different provider.
- **Difficulty:** Easy
- **Priority:** High

#### 3.4 Incremental / Watch Mode

- **Description:** File watcher that monitors the target directory and re-processes only changed files, updating the knowledge graph incrementally.
- **Rationale:** The Python version already has this. For large codebases, full re-extraction is expensive (time + tokens). SHA256 caching infrastructure already exists in our pipeline — this builds on it.
- **How:** Use `FileSystemWatcher` to detect changes. Leverage existing SHA256 cache to identify changed files. Re-run extraction only for modified files, then merge into existing graph.
- **Difficulty:** Medium
- **Priority:** High

---

### Medium-Term (3-6 months) — Structural Improvements

#### 3.5 Roslyn-Based AST Extraction for C#

- **Description:** Replace regex-based C# code extractors with proper Roslyn syntax/semantic analysis for type-safe, accurate AST extraction.
- **Rationale:** Regex is brittle with C# syntax (generics, nested types, attributes, LINQ expressions). Roslyn provides the full compilation model — type resolution, symbol binding, call graph analysis. This is a unique advantage the .NET port has over the Python version for C# codebases.
- **How:** Use `Microsoft.CodeAnalysis.CSharp` to parse syntax trees. Walk the semantic model to extract classes, methods, interfaces, inheritance, dependency injection registrations, and call graphs. Output as `ExtractionResult` using existing schema.
- **Difficulty:** Hard
- **Priority:** High

#### 3.6 Tree-sitter Native Bindings (Multi-Language)

- **Description:** Upgrade from `TreeSitter.Bindings` to `TreeSitter.DotNet` or `TreeSitterLanguagePack` for robust multi-language AST parsing with 28-248+ language grammars.
- **Rationale:** Current tree-sitter integration is basic. `TreeSitter.DotNet` (by mariusgreuel) ships with 28+ pre-compiled grammars, cross-platform native libraries, and predicate support. `TreeSitterLanguagePack` bundles 248+ parsers. This would give us best-in-class polyglot parsing.
- **How:** Evaluate `TreeSitter.DotNet` (NuGet, v1.3.0) vs `TreeSitterLanguagePack`. Replace current bindings while maintaining the same `ExtractionResult` output contract.
- **Difficulty:** Medium
- **Priority:** Medium

#### 3.7 Semantic Kernel / Agent Framework Integration

- **Description:** Offer Semantic Kernel or Microsoft Agent Framework (MAF) as an alternative orchestration layer for extraction, enabling multi-step reasoning, planning, and tool use during graph building.
- **Rationale:** Current extraction uses single-shot `IChatClient` calls. For complex codebases, multi-step agent reasoning could improve extraction quality — e.g., an agent that reads a class, follows its dependencies, and builds a richer subgraph. SK's plugin system and MAF's agent-first design both support this pattern.
- **How:** Create an `AgentExtractor` that wraps SK Kernel or MAF agent with extraction-specific tools (read file, query existing graph, resolve type). Register as an alternative `IPipelineStage`.
- **Difficulty:** Hard
- **Priority:** Medium

#### 3.8 RAG Integration

- **Description:** Use the built knowledge graph as a retrieval source for Retrieval-Augmented Generation pipelines, enabling LLM-powered Q&A grounded in graph structure.
- **Rationale:** This is the natural next step after building a knowledge graph. Instead of flat document search, RAG queries traverse the graph to find relevant nodes, their connections, and community context. This aligns directly with Karpathy's "Q&A and search" phase.
- **How:** Implement an `IEmbeddingGenerator`-powered index over graph nodes. Query combines graph traversal (BFS/DFS from relevant nodes) with semantic similarity. Return structured context (node + edges + community) to the LLM.
- **Difficulty:** Hard
- **Priority:** Medium

#### 3.9 GitHub Actions Integration

- **Description:** Provide a GitHub Action that runs graphify in CI pipelines to detect architecture drift, report new dependencies, and flag structural changes between commits.
- **Rationale:** Knowledge graphs are most valuable when kept current. Running in CI ensures the graph stays fresh and can detect when architectural decisions are violated. Teams can set policies like "no new god nodes" or "all services must belong to a community."
- **How:** Create a Docker-based or composite GitHub Action. Compare graph snapshots between commits. Output a diff report as a PR comment or check annotation.
- **Difficulty:** Medium
- **Priority:** Medium

---

### Long-Term (6-12 months) — Vision Features

#### 3.10 VS Code Extension

- **Description:** Interactive knowledge graph explorer inside VS Code — click a node to navigate to its source file, visualize communities, search by concept, and see architecture at a glance.
- **Rationale:** Developers live in VS Code. Bringing the graph into the editor eliminates context switching. The MCP server already exposes the right operations (query, explain, path, communities, analyze) — the extension would be a visual frontend.
- **How:** Build a VS Code webview extension that loads graph.json (or connects to the MCP server). Use vis.js or D3.js for visualization. Implement go-to-definition for graph nodes.
- **Difficulty:** Hard
- **Priority:** Medium

#### 3.11 .NET Aspire Integration

- **Description:** Aspire AppHost integration that provides a dashboard for monitoring graph build pipelines — live progress, extraction metrics, token usage, and error rates.
- **Rationale:** Aspire 9.4 supports custom commands, OTEL metrics, and interactive dashboards. For large codebase graph builds (which can take minutes and thousands of LLM calls), real-time visibility into the pipeline is essential. Custom business metrics (nodes extracted, edges inferred, communities detected) would surface in the Aspire dashboard.
- **How:** Create a `Graphify.Aspire` hosting package. Emit OTEL metrics from pipeline stages. Register custom dashboard commands for triggering rebuilds, querying nodes, and viewing stats.
- **Difficulty:** Medium
- **Priority:** Low

#### 3.12 Multi-Repository Support

- **Description:** Build knowledge graphs that span multiple repositories, connecting cross-repo dependencies, shared libraries, and API contracts into a unified graph.
- **Rationale:** Real-world systems are rarely single-repo. Microservice architectures, monorepo-to-polyrepo migrations, and shared library ecosystems all benefit from cross-repo structural understanding. This is where graphify-dotnet could differentiate significantly from the Python version.
- **How:** Accept multiple repo paths or GitHub org + repo list. Build per-repo subgraphs, then merge with cross-repo edge detection (shared NuGet packages, API contracts, proto files, shared types).
- **Difficulty:** Hard
- **Priority:** Low

#### 3.13 Knowledge Graph Health Checks (Lint Mode)

- **Description:** Implement Karpathy's "health check" concept for code knowledge — periodic LLM-driven consistency checks that identify stale relationships, orphaned nodes, conflicting concepts, and documentation drift.
- **Rationale:** Directly implements Karpathy's insight that LLMs should maintain knowledge bases, not just build them. A graph that was accurate last month may have drifted as code evolved. Automated lint catches this.
- **How:** Schedule or trigger a "lint" pass that walks the graph, samples nodes, and asks the LLM to verify relationships against current source. Flag stale or contradicted edges. Generate a health report.
- **Difficulty:** Medium
- **Priority:** Low

#### 3.14 Obsidian Vault as Live Knowledge IDE

- **Description:** Enhance the Obsidian export to create a fully bidirectional workflow — changes in Obsidian (annotations, new links, corrections) feed back into the knowledge graph, and graph updates push new pages to the vault.
- **Rationale:** Karpathy's vision positions Obsidian as the IDE for knowledge. Our current Obsidian export is one-way. A bidirectional sync would enable developers to annotate, correct, and extend the graph using Obsidian's excellent editing UX, then have those changes persist in the structured graph.
- **How:** Watch the Obsidian vault directory for changes. Parse Markdown frontmatter and wikilinks to detect new/modified relationships. Merge back into KnowledgeGraph. Use Obsidian's graph view plugin for visualization.
- **Difficulty:** Hard
- **Priority:** Low

---

### Ecosystem-Inspired Ideas

#### 3.15 Coding Agent Skill Packaging

- **Description:** Package graphify-dotnet as a drop-in skill for coding agents (GitHub Copilot, Claude Code, Cursor) — similar to how the Python version works as a Claude Code skill.
- **Rationale:** The Python graphify's biggest adoption vector is its coding agent integration. Our MCP server already provides the protocol; packaging it as a discoverable skill lowers the adoption barrier.
- **Difficulty:** Medium
- **Priority:** High

#### 3.16 Token Efficiency Benchmarks

- **Description:** Implement and publish benchmarks showing token usage per query vs. reading raw source files, following the Python version's "70x fewer tokens" methodology.
- **Rationale:** Token cost is a key decision factor for LLM-powered tools. Demonstrating efficiency builds trust and justifies the graph-building investment.
- **Difficulty:** Easy
- **Priority:** Medium

#### 3.17 Neo4j Live Push

- **Description:** Direct Neo4j database integration for pushing graphs to a live Neo4j instance (beyond the current Cypher export file).
- **Rationale:** Neo4j's visualization and query capabilities (Cypher) are industry-standard for graph exploration. Live push enables real-time graph querying without file intermediaries.
- **Difficulty:** Medium
- **Priority:** Low

#### 3.18 Microsoft Agent Framework (MAF) Native Agent

- **Description:** Build a graphify agent using MAF that can autonomously explore, explain, and maintain codebases as a conversational AI agent — not just a pipeline tool.
- **Rationale:** MAF is Microsoft's latest unified agent framework built on `IChatClient`. A graphify MAF agent would be a natural evolution: from "build a graph" to "be a persistent, queryable codebase expert."
- **Difficulty:** Hard
- **Priority:** Low

---

## 4. How graphify-dotnet Connects to Karpathy's Vision

Karpathy described a workflow for **knowledge manipulation** — ingesting raw data, compiling it into structured wikis, and navigating knowledge through graph views. graphify-dotnet is the **code-specific implementation** of this vision:

| Karpathy's PKB Workflow | graphify-dotnet Equivalent |
|-------------------------|---------------------------|
| Raw data ingestion (`raw/` folder) | File detection pipeline (code, docs, images) |
| LLM "compilation" to Markdown wiki | Semantic extraction → knowledge graph → Wiki/Obsidian export |
| Obsidian as the knowledge IDE | Obsidian vault export with backlinks and graph view |
| Graph view for navigation | Interactive HTML vis.js graph, MCP query tools |
| Q&A and search over the wiki | MCP server (query, explain, path, communities, analyze) |
| Health checks for consistency | *Proposed:* Knowledge graph lint mode (§3.13) |
| Output as new Markdown/slides | Wiki export, report generation |

### The Bridge

graphify-dotnet bridges **code understanding** and **personal knowledge bases** by treating a codebase as a knowledge domain:

1. **Code is knowledge** — Classes, functions, and their relationships are concepts and connections in a knowledge graph, just like topics in a wiki.
2. **The graph is the compiled wiki** — Where Karpathy's LLM compiles articles into interlinked Markdown, graphify compiles code into an interlinked knowledge graph.
3. **MCP is the query layer** — Where Karpathy uses LLM search over the wiki, graphify uses MCP tools to let AI assistants query the graph.
4. **Obsidian is the shared IDE** — Both workflows use Obsidian as the human-navigable frontend for graph-structured knowledge.

The long-term vision: a developer's **personal knowledge base that spans code, documentation, architecture decisions, and runtime behavior** — all compiled, linked, and maintained by LLMs, navigated through graph views, and queryable by AI assistants.

---

## 5. Priority Matrix

| Priority | Items |
|----------|-------|
| **High** | dotnet tool (§3.1), Azure OpenAI (§3.2), Ollama (§3.3), Watch mode (§3.4), Roslyn AST (§3.5), Agent skill (§3.15) |
| **Medium** | Tree-sitter native (§3.6), SK/MAF integration (§3.7), RAG (§3.8), GitHub Actions (§3.9), VS Code extension (§3.10), Token benchmarks (§3.16) |
| **Low** | Aspire (§3.11), Multi-repo (§3.12), Health checks (§3.13), Obsidian bidirectional (§3.14), Neo4j live (§3.17), MAF agent (§3.18) |

---

## 6. References

- [Karpathy's original tweet](https://x.com/karpathy/status/2039805659525644595)
- [safishamsi/graphify (Python)](https://github.com/safishamsi/graphify)
- [awesome-llm-knowledge-bases](https://github.com/SingggggYee/awesome-llm-knowledge-bases)
- [awesome-llm-knowledge-systems](https://github.com/kennethlaw325/awesome-llm-knowledge-systems)
- [Microsoft.Extensions.AI documentation](https://learn.microsoft.com/en-us/dotnet/ai/)
- [Semantic Kernel + ME.AI integration](https://devblogs.microsoft.com/agent-framework/semantic-kernel-and-microsoft-extensions-ai-better-together-part-2/)
- [Microsoft Agent Framework migration guide](https://www.devleader.ca/2026/04/03/migrating-from-semantic-kernel-to-microsoft-agent-framework-in-c)
- [TreeSitter.DotNet](https://github.com/mariusgreuel/tree-sitter-dotnet-bindings)
- [OllamaSharp (.NET Ollama client)](https://www.nuget.org/packages/OllamaSharp/)
- [.NET Aspire 9.4 announcement](https://devblogs.microsoft.com/dotnet/announcing-aspire-9-4/)
- [Roslyn Source Generator Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md)
- [Building PKBs with LLMs: The Karpathy Method](https://howaiworks.ai/blog/andrej-karpathy-llm-knowledge-bases)
- [VentureBeat: Karpathy's LLM Knowledge Base Architecture](https://venturebeat.com/data/karpathy-shares-llm-knowledge-base-architecture-that-bypasses-rag-with-an)
- [DAIR.AI: LLM Knowledge Bases](https://academy.dair.ai/blog/llm-knowledge-bases-karpathy)
