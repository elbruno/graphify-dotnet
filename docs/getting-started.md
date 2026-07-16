# Getting Started with graphify-dotnet

A step-by-step guide from zero to your first knowledge graph. Takes about 5 minutes.

## Prerequisites

- **[.NET 10 SDK](https://dotnet.microsoft.com/download)** — verify with `dotnet --version`
- **A browser** — to view the interactive graph
- **Optional:** [Ollama](https://ollama.com) or Azure OpenAI for AI-powered semantic extraction (not needed for your first run)

## Step 1: Install

```bash
dotnet tool install -g graphify-dotnet
```

Verify the install:

```bash
graphify --version
```

> **Trouble?** If you see `graphify: command not found`, restart your terminal — the PATH update takes effect in a new session. See [Troubleshooting](troubleshooting.md) for more.

## Step 2: Your First Analysis

Let's analyze the included sample project — a small C# library with 6 files implementing the repository pattern. No AI provider needed; AST-only extraction works out of the box.

Clone the repo and run:

```bash
git clone https://github.com/elbruno/graphify-dotnet.git
cd graphify-dotnet
graphify run samples/mini-library
```

You'll see output like:

```
graphify-dotnet v0.1.0
Detecting files in samples/mini-library...
Found 6 files (6 code)
Extracting features (AST mode)...
Building knowledge graph...
Clustering (Louvain)...
Analyzing graph...
Exporting: json, html, report
Done. Output: samples/mini-library/graphify-out/
```

That's it. The pipeline ran all 7 stages: detect → extract → build → cluster → analyze → report → export.

## Step 3: Explore the Results

Three files just appeared in `graphify-out/`:

```
graphify-out/
├── graph.html         # Interactive visualization — open in browser
├── graph.json         # Machine-readable graph data
└── GRAPH_REPORT.md    # Analysis report with insights
```

Let's walk through each one.

### graph.html — Interactive Graph Viewer

Open `graphify-out/graph.html` in your browser. You'll see an interactive network diagram:

- **Nodes** are code elements (classes, methods, files). Each one is labeled.
- **Edges** are relationships — `contains` (a class contains a method), `imports` (a file uses a namespace).
- **Colors** represent communities — groups of nodes that are tightly connected. Nodes in the same community share a color.
- **Size** reflects connectivity — larger nodes have more connections.

Try it:
- **Click a node** to see its details (type, file, community)
- **Drag nodes** to rearrange the layout
- **Scroll** to zoom in/out
- **Search** for a specific node by name

You should see clusters forming around the main files: `UserRepository`, `UserService`, `IRepository`, `User`, and `ServiceCollectionExtensions`.

### GRAPH_REPORT.md — Analysis Report

Open `graphify-out/GRAPH_REPORT.md`. This is the human-readable summary:

```markdown
# Graph Report - mini-library  (2026-04-06)

## Summary
- 47 nodes · 79 edges · 7 communities detected
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
```

Key sections:

**God Nodes** — The most connected elements. These are your core abstractions:

```
1. MiniLibrary - 2 edges
2. UserRepository - 2 edges
3. User - 2 edges
4. IRepository - 2 edges
```

> `MiniLibrary` appears multiple times because each file imports the namespace — the tool sees one node per file's `using MiniLibrary` statement. This is expected for AST-only extraction.

**Communities** — The tool detected 7 natural groupings using Louvain clustering:

| Community | Nodes | Key Members | Cohesion |
|-----------|-------|-------------|----------|
| 0 | 9 | UserRepository, UpdateAsync, GetAllAsync | 0.22 |
| 1 | 8 | IRepository, AddAsync, DeleteAsync | 0.46 |
| 2 | 8 | UserService, CreateUserAsync, GetActiveUsersAsync | 0.25 |
| 3 | 6 | ServiceCollectionExtensions, AddMiniLibrary | 0.60 |
| 4 | 6 | UserRepository error handling (lock, exceptions) | 0.33 |
| 5 | 5 | User, Validate | 0.70 |
| 6 | 5 | UserService constructor, exception handling | 0.40 |

Higher cohesion means tighter coupling within the community. Community 5 (User model) has the highest cohesion at 0.70 — that's a well-encapsulated module.

**Surprising Connections** — For this small project: "None detected — all connections are within the same source files." In larger projects, this section reveals unexpected cross-module dependencies.

### graph.json — Raw Graph Data

Open `graphify-out/graph.json` for the machine-readable graph. It contains two arrays: `nodes` and `edges`.

A sample node:

```json
{
  "id": "userrepository_userrepository",
  "label": "UserRepository",
  "type": "Entity",
  "community": 0,
  "file_path": "samples/mini-library/src/UserRepository.cs",
  "confidence": "EXTRACTED",
  "metadata": {
    "source_location": "L7"
  }
}
```

A sample edge:

```json
{
  "source": "userrepository",
  "target": "userrepository_addasync",
  "relationship": "contains",
  "weight": 1,
  "confidence": "EXTRACTED",
  "metadata": {
    "source_file": "samples/mini-library/src/UserRepository.cs",
    "source_location": "L42"
  }
}
```

Every node has a confidence level: `EXTRACTED` (from AST parsing), `INFERRED` (from AI analysis), or `AMBIGUOUS`. Since we ran AST-only, everything is `EXTRACTED`.

## Step 4: Add AI Enrichment (Optional)

AST extraction finds structural relationships — classes, methods, imports. AI semantic extraction goes further: it reads code meaning, finds conceptual connections, and infers relationships that don't appear in syntax.

To enable AI extraction, run the config wizard:

```bash
graphify config
```

Select **🔧 Set up AI provider** and choose one:

| Provider | Setup |
|----------|-------|
| **Ollama** (local, free) | Install [Ollama](https://ollama.com), pull a model: `ollama pull llama3.2` |
| **Azure OpenAI** (cloud) | Need endpoint URL, API key, deployment name |
| **GitHub Copilot SDK** | Zero config for Copilot subscribers |

After configuring, re-run the analysis:

```bash
graphify run samples/mini-library
```

Compare the results: you'll see new `INFERRED` nodes from AI analysis — conceptual relationships that the AST parser can't detect. The graph becomes richer with semantic connections between components.

## Step 5: Try It on Your Own Code

Point graphify at any project:

```bash
graphify run .
```

Or a specific directory:

```bash
graphify run ./src --output my-graph
```

graphify supports **25+ file types** including C#, Python, TypeScript, JavaScript, Go, Rust, Java, C/C++, Ruby, Kotlin, Scala, PHP, Swift, and more. It also processes Markdown, YAML, JSON, and media files.

## Step 6: Export All Formats

Want more than the default? Export to all formats:

```bash
graphify run . --format json,html,svg,neo4j,obsidian,wiki,report,surrealdb
```

This generates:
- `graph.html` — Interactive vis.js viewer
- `graph.json` — Machine-readable data
- `graph.svg` — Static vector image for docs
- `graph.cypher` — Neo4j import script
- `codebase.db` — SurrealDB database (embedded or remote)
- `obsidian/` — Obsidian vault with wikilinks
- `wiki/` — Agent-crawlable documentation
- `GRAPH_REPORT.md` — Analysis report

## Step 7: Query with AI Assistants

Once you have a `graph.json`, run the MCP server to let Claude, Copilot, or any MCP client query your knowledge graph.

```bash
dotnet run --project src/Graphify.Mcp -- graphify-out/graph.json
```

Then connect your AI assistant — see the [MCP Server](mcp-server.md) guide for client configuration. You can ask questions like "Find all authentication-related nodes" or "What's the shortest path between UserService and DatabaseContext?"

## Next Steps

- **[CLI Reference](cli-reference.md)** — All commands and options
- **[Configuration](configuration.md)** — Layered config system, environment variables
- **[Worked Example](worked-example.md)** — Deep dive into the mini-library analysis
- **[Watch Mode](watch-mode.md)** — Live graph updates as you edit code
- **[MCP Server](mcp-server.md)** — Query your graph through AI assistants
- **[Export Formats](export-formats.md)** — Details on all output formats
- **[Troubleshooting](troubleshooting.md)** — Common issues and solutions
