# Ladybug Export

> Embedded graph database export for localOLAP queries using Ladybug (Kuzu).

## Quick Start

```bash
graphify run ./my-project --format ladybug
# Generates graph.ladybug.cypher with Ladybug-compatible DDL and data
```

## What it Produces

The **Ladybug format** generates a single `graph.ladybug.cypher` file containing:
- **DDL** — `CREATE NODE TABLE GraphNode` and `CREATE REL TABLE GraphEdge` with Ladybug's structured schema
- **Node `CREATE` statements** — One per node with all properties, including metadata as a native `MAP(STRING, STRING)`
- **Edge `CREATE` statements** — One per relationship with type, weight, and confidence
- **Query examples** — Annotated Cypher snippets to get you started

### Why Ladybug?

Ladybug is an embedded graph database (like SQLite, but for graphs). Unlike Neo4j, it:
- Requires **no server setup** — runs in-process
- Uses **columnar storage** — optimized for analytical workloads
- Supports **structured schemas** — node/relationship tables with typed columns
- Is **fully open-source** (MIT) and built on peer-reviewed research

## Schema Design

### Node Table: `GraphNode`

| Property | Type | Description |
|----------|------|-------------|
| `id` | `STRING PRIMARY KEY` | Unique node identifier |
| `label` | `STRING` | Human-readable name |
| `nodeType` | `STRING` | Type: Class, Function, Module, Concept, etc. |
| `filePath` | `STRING` | Absolute source file path (if any) |
| `relativePath` | `STRING` | Path relative to project root |
| `language` | `STRING` | Programming language: CSharp, Python, etc. |
| `community` | `INT64` | Community ID from clustering (nullable) |
| `confidence` | `STRING` | Extraction confidence: Extracted, Inferred, Ambiguous |
| `metadata` | `MAP(STRING, STRING)` | Variable key-value metadata from extraction |

### Relationship Table: `GraphEdge`

| Property | Type | Description |
|----------|------|-------------|
| `_SRC` / `_DST` | internal | Source and target node references (auto-managed) |
| `relationship` | `STRING` | Relation type: calls, imports, contains, etc. |
| `weight` | `DOUBLE` | Edge weight (default 1.0) |
| `confidence` | `STRING` | Confidence level |

## How to Use

### Option 1: Ladybug CLI

Install the Ladybug CLI, then run the generated script:

```bash
# Execute the Cypher script in Ladybug
lbug -i graph.ladybug.cypher
```

### Option 2: Programmatic (C#)

Use the [Ladybug .NET bindings](https://www.nuget.org/packages/Ladybug) to load and query:

```csharp
using Ladybug;

await using var client = new LadybugClient(nativeLibrary);
await client.ExecuteAsync(File.ReadAllText("graph.ladybug.cypher"));

var result = await client.ExecuteAsync(
    "MATCH (n:GraphNode)-[e:GraphEdge]->(m:GraphNode) " +
    "RETURN n.label, e.relationship, m.label LIMIT 10");
```

### Option 3: In-Memory Pipeline

```bash
# Generate ladybug script alongside other formats
graphify run ./src --format json,html,ladybug,report
```

## Querying Your Graph

After importing, run Cypher queries optimized for Ladybug's structured model:

```cypher
// Find all classes in a community
MATCH (n:GraphNode)
WHERE n.nodeType = 'Class' AND n.community = 2
RETURN n.id, n.label;

// Find high-degree nodes (god nodes)
MATCH (n:GraphNode)-[e:GraphEdge]->()
RETURN n.id, n.label, COUNT(e) AS degree
ORDER BY degree DESC LIMIT 10;

// Shortest path between two nodes
MATCH p = shortestPath(
    (a:GraphNode {id: 'AuthService'})-[:GraphEdge*]-(b:GraphNode {id: 'UserController'})
)
RETURN p;

// Find circular dependencies
MATCH (a:GraphNode)-[e1:GraphEdge]->(b:GraphNode)-[e2:GraphEdge]->(a)
RETURN a.label, b.label, e1.relationship;

// Analyze communities
MATCH (n:GraphNode)
RETURN n.community, COUNT(*) AS size
ORDER BY size DESC;

// Access metadata map directly
MATCH (n:GraphNode)
RETURN n.id, n.metadata['source_file'] AS file
WHERE n.metadata IS NOT NULL;

// Find nodes with specific metadata key
MATCH (n:GraphNode)
WHERE n.metadata['line'] IS NOT NULL
RETURN n.id, n.metadata['line'] AS line;
```

## Metadata as MAP(STRING, STRING)

Ladybug supports a native `MAP` type — a dictionary where all keys share one type and all values share another. graphify uses `MAP(STRING, STRING)` to store variable extraction metadata without JSON serialization.

Example:
```cypher
// Metadata is stored as a native map
CREATE (n:GraphNode {
    id: "AuthService",
    metadata: map(["source_file", "line"], ["src/Auth.cs", "42"])
});

// Query individual keys
MATCH (n:GraphNode) RETURN n.metadata['source_file'];
```

This is more efficient and type-safe than storing metadata as a JSON string.

## Best For

- **Local analytics** — No server required; run queries on your machine
- **Large graphs** — Ladybug's columnar storage scales to billions of nodes
- **Structured schemas** — Strongly typed properties with fast lookups
- **Research & benchmarking** — Reproducible, in-process graph queries
- **CI/CD pipelines** — Lightweight, no external DB dependencies

## Comparison with Neo4j Export

| Feature | Ladybug | Neo4j |
|---------|---------|-------|
| Server required | No | Yes (or Aura cloud) |
| Storage | Columnar, embedded | Native graph store |
| Schema | Structured (tables) | Schema-free (labels) |
| Metadata type | `MAP(STRING, STRING)` | JSON string or flat properties |
| Best for | Local analytics, embedded apps | Production graph apps, visualizations |

## Limitations

- **Cypher dialect differences** — Ladybug Cypher is close to openCypher but has differences documented [here](https://docs.ladybugdb.com/cypher/difference)
- **One-way import** — Changes in Ladybug don't sync back to source code
- **Requires Ladybug runtime** — Need `lbug` CLI or Ladybug library to execute the script
- **Structured schema** — All nodes must fit the `GraphNode` table schema; arbitrary properties require `MAP` keys

## Next Steps

1. **Install Ladybug** — `pip install ladybug` or grab a release from [GitHub](https://github.com/LadybugDB/ladybug)
2. **Import** — `lbug -i graph.ladybug.cypher`
3. **Query** — Use the example queries above or write your own
4. **Analyze** — Run graph algorithms (PageRank, Louvain, WCC) via Ladybug's built-in extensions

## See Also

- [Worked Example](worked-example.md) — Real output from a C# project walkthrough
- [Export Formats Overview](export-formats.md)
- [Neo4j Cypher Export](format-neo4j.md) — Server-based graph database alternative
- [JSON Graph Export](format-json.md) — Raw data in standard format
- [Ladybug Documentation](https://docs.ladybugdb.com/)
