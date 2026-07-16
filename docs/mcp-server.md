# MCP Server

> Query your knowledge graph through AI assistants — Claude, Copilot, VS Code, and any MCP-compatible client.

## Overview

The MCP (Model Context Protocol) server loads a pre-built graph from its JSON export and exposes it as 5 tools. AI assistants connect over stdio and query the graph as if it were a live database — no HTTP server, no separate daemon.

```
┌─────────────────┐     ┌──────────────────────────┐
│  Pipeline        │     │  MCP Server              │
│  run → export    │────▶│  loads graph.json        │
│  (graph.json)    │     │  exposes 5 tools         │
└─────────────────┘     │  Claude / Copilot / IDE   │
                        └──────────────────────────┘
```

## Prerequisites

- A graph JSON file from a previous run: `graphify run . --format json`
- An MCP-compatible client (Claude Desktop, VS Code with Copilot, etc.)

## Quick Start

### Step 1: Build the graph

```bash
graphify run ./my-project --format json
# Produces: graphify-out/graph.json
```

### Step 2: Run the MCP server

```bash
dotnet run --project src/Graphify.Mcp -- graphify-out/graph.json
```

Or build once and run the binary directly:

```bash
dotnet publish src/Graphify.Mcp -o ./mcp-server
./mcp-server/Graphify.Mcp graphify-out/graph.json
```

The server no output on success — all logging goes to stderr, stdout is reserved for JSON-RPC protocol messages.

### Step 3: Connect your AI assistant

**Claude Desktop** — add to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "graphify": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/graphify-dotnet/src/Graphify.Mcp",
        "--",
        "/path/to/your/graph.json"
      ]
    }
  }
}
```

**VS Code** — add to `.vscode/mcp.json`:

```json
{
  "servers": {
    "graphify": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/graphify-dotnet/src/Graphify.Mcp",
        "--",
        "/path/to/your/graph.json"
      ],
      "description": "Knowledge graph for my codebase"
    }
  }
}
```

### Step 4: Ask questions

Once connected, your AI assistant can use these tools naturally:

- *"Find all nodes related to authentication."*
- *"What's the shortest path between UserService and DatabaseContext?"*
- *"Explain the UserRepository node."*
- *"List the communities and their top members."*
- *"Analyze the overall graph structure."*

## Tools

### Query — Search nodes and edges

Search nodes by name, label, or type.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `searchTerm` | string | — | Match against node ID, label, or type |
| `limit` | int | 10 | Max results to return |

Returns matching nodes with degree, community, and up to 5 connections each.

### Path — Shortest path between two nodes

Find the shortest path between any two nodes using BFS.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `sourceId` | string | — | Start node ID |
| `targetId` | string | — | End node ID |

Returns the path as an ordered list of nodes and the path length (number of edges).

### Explain — Node details with all connections

Get full details for a single node including incoming and outgoing edges.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `nodeId` | string | — | Node ID to explain |

Returns the node's metadata, total degree, and separate lists of incoming and outgoing connections.

### Communities — List communities and their members

Browse community clusters detected by the Louvain algorithm.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `communityId` | int? | null | Specific community (omit for all) |

Without `communityId`, returns every community with its top 5 members. With a specific ID, returns all members sorted by degree.

### Analyze — Graph-wide statistics

Get a high-level summary of the entire graph.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `topN` | int | 10 | Top nodes to include |

Returns node/edge counts, community count, average degree, isolated nodes, top-N nodes by degree, type distribution, and relationship distribution.

## CLI Options

```bash
Graphify.Mcp <graph-path> [--verbose]
```

| Argument | Description |
|----------|-------------|
| `graph-path` | Path to the `graph.json` file (default: `graph.json`) |
| `--verbose`, `-v` | Enable detailed logging (node/edge counts, load progress) |

## Architecture

The server uses the `ModelContextProtocol` SDK with stdio transport. All JSON-RPC messages flow over stdout; logging goes exclusively to stderr. Tools are auto-discovered via `[McpServerTool]` attributes on the `GraphTools` class.

Sequence on startup:

1. Parse CLI arguments
2. Load and deserialize `graph.json` into a `KnowledgeGraph`
3. Register the graph and tools as DI singletons
4. Start the MCP server with stdio transport
5. Wait for client connections and tool invocations

The server is read-only — it does not run pipeline stages, trigger re-extraction, or write to disk.

## Integration with CI/CD

Regenerate the graph and restart the server on each build:

```bash
graphify run ./src --format json
dotnet run --project src/Graphify.Mcp -- graphify-out/graph.json
```

## See Also

- [Getting Started](getting-started.md)
- [CLI Reference](cli-reference.md)
- [Export Formats Overview](export-formats.md)
- [JSON Graph Export](format-json.md)
