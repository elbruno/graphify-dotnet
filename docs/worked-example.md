# Worked Example: Analyzing a C# Library

A deep walkthrough of running graphify-dotnet against a real project and interpreting every output file.

## What We're Analyzing

The `samples/mini-library/` directory contains a small C# library implementing the **repository pattern** — a common enterprise architecture. It has 5 source files:

| File | Purpose |
|------|---------|
| `IRepository.cs` | Generic repository interface with CRUD methods |
| `UserRepository.cs` | Concrete implementation with in-memory storage |
| `UserService.cs` | Business logic layer consuming the repository |
| `User.cs` | Domain entity with validation |
| `ServiceCollectionExtensions.cs` | DI registration helper |

This is a good test case because it has clear layering (entity → repository → service → DI), uses common patterns, and is small enough to fully inspect the output.

## Running the Analysis

```bash
# Using the global tool
graphify run samples/mini-library --format json,html,svg,neo4j,obsidian,wiki,report

# Or build from source
dotnet run --project src/Graphify.Cli -- run samples/mini-library --format json,html,svg,neo4j,obsidian,wiki,report -v
```

Expected terminal output:

```
graphify-dotnet v0.1.0
Detecting files in samples/mini-library...
Found 6 files (6 code)
Extracting features (AST mode)...
Building knowledge graph...
Clustering (Louvain)...
Analyzing graph...
Exporting: json, html, svg, neo4j, obsidian, wiki, report
Done. Output: samples/mini-library/graphify-out/
```

Pre-generated output is available at [`samples/mini-library/graphify-out/`](../samples/mini-library/graphify-out/).

## The Result: 47 Nodes, 79 Edges, 7 Communities

```
samples/mini-library/graphify-out/
├── GRAPH_REPORT.md    # Analysis report (god nodes, communities, insights)
├── graph.json         # Full graph data (47 nodes, 79 edges)
├── graph.html         # Interactive vis.js viewer — open in browser
├── graph.svg          # Static vector image
├── graph.cypher       # Neo4j import script
├── obsidian/          # Obsidian vault (30 .md files with wikilinks)
└── wiki/              # Agent-crawlable wiki (index + community pages)
```

This runs in **AST-only mode** (100% `EXTRACTED` confidence) — no AI provider needed. Let's walk through what the tool found.

## Understanding the Graph Report

Open `GRAPH_REPORT.md`. Here's the full summary:

```
47 nodes · 79 edges · 7 communities detected
Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
```

### God Nodes — Your Core Abstractions

The most connected elements in the graph:

| Node | Edges | What It Means |
|------|-------|---------------|
| `MiniLibrary` | 2 | The root namespace — imported by every file |
| `UserRepository` | 2 | Central data access class |
| `User` | 2 | Core domain entity |
| `IRepository` | 2 | The interface everything depends on |

Notice `MiniLibrary` appears 5 times in the god nodes list. That's because each source file has `using MiniLibrary;` — the AST extractor sees each import as a separate node referencing the namespace. In a larger project with AI enrichment, these would be merged into a single semantic node.

### Community Structure

Louvain clustering found 7 natural groupings:

| # | Key Members | Nodes | Cohesion | Interpretation |
|---|-------------|-------|----------|----------------|
| 0 | UserRepository + CRUD methods | 9 | 0.22 | Repository implementation layer |
| 1 | IRepository + interface methods | 8 | 0.46 | Repository contract |
| 2 | UserService + business methods | 8 | 0.25 | Service / business logic layer |
| 3 | ServiceCollectionExtensions + DI | 6 | 0.60 | Dependency injection wiring |
| 4 | UserRepository error handling | 6 | 0.33 | Exception handling in repository |
| 5 | User + Validate | 5 | 0.70 | Domain model |
| 6 | UserService constructor + errors | 5 | 0.40 | Service initialization |

**What cohesion tells you:** Community 5 (User model) has the highest cohesion at 0.70 — it's a well-encapsulated module. Community 0 (UserRepository implementation) has the lowest at 0.22 — it has more external connections, which makes sense since the repository talks to both the entity and the interface layers.

### Surprising Connections

For this small project: *"None detected — all connections are within the same source files."* In larger codebases, this section reveals unexpected cross-module dependencies — the hidden coupling that causes bugs.

### Suggested Questions

The tool reports: *"Not enough signal to generate questions."* With AI enrichment enabled, this section surfaces architectural questions like "Should AuthService be split into JWT and OAuth concerns?"

## Exploring graph.json

The JSON export contains the complete graph data. Here's the schema:

```json
{
  "nodes": [ ... ],
  "edges": [ ... ]
}
```

### Node Structure

Each node represents a code element:

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

| Field | Description |
|-------|-------------|
| `id` | Stable identifier (filename + entity name, lowercased) |
| `label` | Display name |
| `type` | `Entity` or `File` |
| `community` | Cluster ID from Louvain algorithm |
| `confidence` | `EXTRACTED` (AST), `INFERRED` (AI), or `AMBIGUOUS` |
| `metadata.source_location` | Line number in source file |
| `metadata.merge_count` | How many duplicate detections were merged |

### Edge Structure

Each edge represents a relationship:

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

Relationship types in this graph:
- **`contains`** — A file or class contains a method/function
- **`imports`** — A file imports a namespace or module

With AI enrichment, you'd also see: `calls`, `inherits`, `implements`, `depends_on`.

## Interactive HTML Viewer

Open `graph.html` in your browser. You'll see a force-directed network diagram powered by vis.js.

**What to look for:**
- **Color clusters** — Each community gets a distinct color. You should see the UserRepository cluster, UserService cluster, and User model cluster as separate color groups.
- **Central nodes** — The `MiniLibrary` namespace nodes sit at connection points between clusters.
- **Hover** over any node to see its label, type, and community.
- **Click** a node to highlight its direct connections.
- **Drag** nodes to rearrange. **Scroll** to zoom.
- **Search** in the control panel to find specific nodes.

For large graphs (1000+ nodes), the HTML viewer takes a few seconds to render. Consider using Neo4j for querying at that scale.

## Other Export Formats

### SVG (`graph.svg`)

A static vector image of the same graph. Embed in README files, presentations, or wikis. Community colors and layout match the HTML viewer.

### Neo4j Cypher (`graph.cypher`)

Import script for Neo4j graph database. Run it in Neo4j Browser:

```cypher
// Paste the contents of graph.cypher into Neo4j Browser
// Then query:
MATCH (n) RETURN n LIMIT 50
```

### Obsidian Vault (`obsidian/`)

30 markdown files with YAML frontmatter and `[[wikilinks]]`. Open the `obsidian/` folder as a vault in Obsidian to get the built-in graph view, backlinks, and search.

### Wiki (`wiki/`)

Agent-crawlable documentation with an index page and community breakdowns. Designed for LLM navigation and automated documentation pipelines.

### Report (`GRAPH_REPORT.md`)

The analysis report we walked through above — god nodes, communities, surprising connections, and suggested questions.

## Querying with the MCP Server

The MCP server loads `graph.json` and exposes it as tools for AI assistants. Start it:

```bash
dotnet run --project src/Graphify.Mcp -- samples/mini-library/graphify-out/graph.json
```

Then connect Claude Desktop or VS Code and ask questions:

- *"Find all nodes related to UserRepository."*
- *"What's the shortest path between User and UserService?"*
- *"List the communities with their top members."*
- *"Analyze the graph structure."*

The server exposes 5 tools — `query`, `path`, `explain`, `communities`, `analyze`. Configuration examples and a full reference are in the [MCP Server](mcp-server.md) guide.

For details on each format, see:

- [Export Formats Overview](export-formats.md)
- [HTML Interactive Viewer](format-html.md)
- [JSON Graph Export](format-json.md)
- [SVG Graph Export](format-svg.md)
- [Neo4j Cypher Export](format-neo4j.md)
- [Obsidian Vault Export](format-obsidian.md)
- [Wiki Export](format-wiki.md)
- [Graph Analysis Report](format-report.md)
- [MCP Server](mcp-server.md)

## Try It Yourself

```bash
# Analyze your own project
graphify run .

# Or a specific module
graphify run ./src/MyService --format json,html,report -v
```

For a guided first-run walkthrough, see [Getting Started](getting-started.md).
