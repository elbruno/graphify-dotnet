# Ladybug Export

> Embedded graph database export for local OLAP queries using Ladybug.

## Quick Start

Install Ladybug: https://docs.ladybugdb.com/installation/

```bash
# 1. Generate the Cypher export script
graphify run ./my-project --format ladybug
# or 
dotnet run --project src/Graphify.Cli -- run . --format ladybug -o graphify-out

# 2. Import into a Ladybug database (creates a 'graph.db' directory)
lbug graphify-out/graph.db < graphify-out/graph.ladybug.cypher

# 3. Visualize using Ladybug Explorer (use '--user 0:0' if rootless docker)
docker run -p 8000:8000 \
           -v $(pwd)/graphify-out:/database \
           -e LBUG_FILE=graph.db \
           --rm ghcr.io/ladybugdb/explorer:latest
```

Open [http://localhost:8000](http://localhost:8000) to explore your graph.

## What it Produces

The **Ladybug format** generates a single `graph.ladybug.cypher` file containing:
- **DDL** — `CREATE NODE TABLE GraphNode` and `CREATE REL TABLE GraphEdge` with Ladybug's structured schema.
- **Node `CREATE` statements** — One per node with all properties, including metadata as a native `MAP(STRING, STRING)`.
- **Edge `MATCH/CREATE` statements** — Using `MATCH` to link existing nodes for referential integrity.
- **Query examples** — Annotated Cypher snippets to get you started.

## Visualization with Ladybug Explorer

Ladybug Explorer provides a rich web interface to query and visualize your graph.

### 1. Create the Database
Ladybug is an embedded database. Before you can visualize, you must execute the generated script to build the database files:

```bash
lbug my_project.db < graphify-out/graph.ladybug.cypher
```

### 2. Launch Explorer
Run the Explorer via Docker, mapping your database directory into the container:

```bash
docker run -p 8000:8000 \
           -v $(pwd)/my_project.db:/database \
           -e LBUG_FILE=my_project.db \
           --rm ghcr.io/ladybugdb/explorer:latest
```

- **`-p 8000:8000`**: Exposes the web interface on port 8000.
- **`-v $(pwd)/my_project.db:/database`**: Mounts your local database directory to the container's `/database` path.
- **`-e LBUG_FILE=my_project.db`**: Tells the Explorer which database directory to load from the `/database` volume.

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
| `metadata` | `MAP(STRING, STRING)` | Variable key-value metadata from extraction |

## Querying Your Graph

After importing, run Cypher queries optimized for Ladybug's structured model. Here are some useful examples for analyzing codebases:

### 1. Architectural Insights
```cypher
// Find high-degree "God Nodes" (potential refactoring targets)
MATCH (n:GraphNode)-[e:GraphEdge]->()
RETURN n.label, n.nodeType, COUNT(e) AS outgoing_degree
ORDER BY outgoing_degree DESC LIMIT 10;

// Find nodes that are highly depended upon (critical components)
MATCH ()-[e:GraphEdge]->(n:GraphNode)
RETURN n.label, n.nodeType, COUNT(e) AS incoming_degree
ORDER BY incoming_degree DESC LIMIT 10;

// Identify circular dependencies (A -> B -> A)
MATCH (a:GraphNode)-[:GraphEdge]->(b:GraphNode)-[:GraphEdge]->(a)
WHERE a.id < b.id
RETURN a.label, b.label;
```

### 2. Dependency & Impact Analysis
```cypher
// Impact Analysis: What depends on 'MyService' (up to 3 levels deep)?
MATCH (target:GraphNode {label: 'MyService'})<-[:GraphEdge*1..3]-(dependent)
RETURN DISTINCT dependent.label, dependent.nodeType, dependent.community;

// Find all external dependencies (nodes without a filePath)
MATCH (n:GraphNode)
WHERE n.filePath IS NULL OR n.filePath = ''
RETURN n.label, n.nodeType;

// Shortest path between two distant components
MATCH (a:GraphNode {label: 'AuthService'}), (b:GraphNode {label: 'PaymentGateway'})
MATCH p = (a)-[:GraphEdge* SHORTEST]->(b)
RETURN p;
```

### 3. Community & Structure Analysis
```cypher
// Analyze community sizes (detect logical clusters)
MATCH (n:GraphNode)
RETURN n.community, COUNT(*) AS size
ORDER BY size DESC;

// Find "Bridge" nodes that connect different communities
MATCH (a:GraphNode)-[e:GraphEdge]->(b:GraphNode)
WHERE a.community <> b.community
RETURN a.label, a.community, b.label, b.community, e.relationship
LIMIT 20;

// Find God Nodes within a specific community
MATCH (n:GraphNode)
WHERE n.community = 5
MATCH (n)-[e:GraphEdge]->()
RETURN n.label, COUNT(e) AS score
ORDER BY score DESC LIMIT 5;
```

### 4. Quality & Metadata
```cypher
// Find "Orphan" nodes (nodes with no connections)
MATCH (n:GraphNode)
WHERE NOT (n)-[:GraphEdge]-()
RETURN n.label, n.filePath;

// Access native node metadata map
MATCH (n:GraphNode)
WHERE size(map_extract(n.metadata, 'source_location')) > 0
RETURN n.label, map_extract(n.metadata, 'source_location')[1] AS line;

// Access native edge metadata map (e.g., find merging source)
MATCH (s)-[e:GraphEdge]->(t)
WHERE size(map_extract(e.metadata, 'source_file')) > 0
RETURN s.label, e.relationship, t.label, map_extract(e.metadata, 'source_file')[1] AS origin;
```

## Best For

- **Local analytics** — No server required; run queries on your machine.
- **Interactive Exploration** — Best visual experience when paired with Ladybug Explorer.
- **Large graphs** — Ladybug's columnar storage scales to billions of nodes.

## See Also

- [Export Formats Overview](export-formats.md)
- [Neo4j Cypher Export](format-neo4j.md) — Server-based graph database alternative.
- [Ladybug Documentation](https://docs.ladybugdb.com/)
