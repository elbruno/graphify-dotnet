# Neo4j Cypher Export

> Cypher script for importing your knowledge graph into Neo4j for advanced querying and analysis.

## Quick Start

```bash
graphify run ./my-project --format neo4j
# Generates graph.cypher with Neo4j import script
```

## What it Produces

The **Neo4j format** generates `graph.cypher` containing:
- **Node CREATE statements** — One Cypher statement per node with properties
- **Edge CREATE statements** — Relationship statements between nodes
- **Indexes** — Performance-critical indexes on node IDs and types
- **Constraints** — Uniqueness constraints to prevent duplicates
- **Comments** — Community assignment and metadata preserved

## Cypher Script Contents

```cypher
// Example output structure
CREATE (n:Node {id: "AuthService", label: "AuthService", type: "Class", community: 2})
CREATE (n:Node {id: "JwtTokenizer", label: "JwtTokenizer", type: "Class", community: 2})

CREATE (a:Node {id: "AuthService"})-[:calls {confidence: 0.95, weight: 3}]->(b:Node {id: "JwtTokenizer"})

CREATE INDEX ON :Node(id)
CREATE INDEX ON :Node(type)
CREATE CONSTRAINT ON (n:Node) ASSERT n.id IS UNIQUE
```

## How to Use

### Option 1: Neo4j Browser

1. Go to [Neo4j Aura](https://aura.neo4j.io) or launch local Neo4j Desktop
2. Open Neo4j Browser (usually `localhost:7474`)
3. Copy-paste entire `graph.cypher` file into the query editor
4. Click play (▶) to execute all statements

### Option 2: cypher-shell

For command-line import:

```bash
cypher-shell -u neo4j -p password < graph.cypher
```

### Option 3: neo4j-admin

For server-side bulk import:

```bash
neo4j-admin database import full \
  --nodes=graph-nodes.csv \
  --relationships=graph-relationships.csv \
  neo4j
```

### Option 4: Docker

Run Neo4j in Docker and import:

```bash
docker run --name neo4j -d -p 7474:7474 -p 7687:7687 neo4j:latest
docker exec neo4j cypher-shell -u neo4j -p neo4j < graph.cypher
# Update password when first prompted
```

## Querying Your Graph

After import, run Cypher queries to explore:

```cypher
// Find all classes in a community
MATCH (n:Node) WHERE n.community = 2 RETURN n.label, n.type

// Find high-degree nodes (god nodes)
MATCH (n:Node)-[r]->() 
RETURN n.label, count(r) as degree 
ORDER BY degree DESC LIMIT 10

// Shortest path between two nodes
MATCH p=shortestPath((a:Node {id: "AuthService"})-[*]->(b:Node {id: "UserController"}))
RETURN p

// Find circular dependencies
MATCH (a:Node)-[:calls*]->(b:Node)-[:calls*]->(a)
RETURN a.label, b.label

// Analyze communities
MATCH (n:Node) RETURN n.community, count(*) as size ORDER BY size DESC

// Find isolated nodes
MATCH (n:Node) WHERE NOT (n)--() RETURN n.label, n.type

// Traverse connections
MATCH (a:Node {id: "AuthService"})-[r]->(b:Node)
RETURN a.label, type(r), b.label, r.confidence
```

## Best For

- **Advanced graph queries** — Cypher is powerful for traversal and pattern matching
- **Large datasets** — Neo4j handles millions of nodes efficiently
- **Complex analysis** — Find cycles, paths, and patterns in graph structure
- **Integration** — Combine with other graph data in Neo4j
- **Real-time dashboards** — Query live metrics from browser
- **Machine learning** — Export subgraphs for ML pipelines

## Example Workflows

### Detect Circular Dependencies

```cypher
MATCH (a:Node)-[:calls*]->(b:Node)-[:calls*]->(a)
RETURN DISTINCT a.label as circular_cycle
```

Use results to identify tightly coupled modules needing refactoring.

### Analyze Community Structure

```cypher
MATCH (n:Node)
WITH n.community as community, collect(n.label) as members, count(*) as size
RETURN community, size, members
ORDER BY size DESC
```

Understand how communities break down and if they're balanced.

### Find God Nodes

```cypher
MATCH (n:Node)-[]-()
RETURN n.label, count(*) as degree
ORDER BY degree DESC LIMIT 20
```

Identify high-degree nodes that may need refactoring.

## Node Properties

Nodes include these queryable properties:
- `id` — Unique identifier
- `label` — Display name
- `type` — Node type (Class, Function, Module, etc.)
- `community` — Community cluster ID
- `degree` — Number of connections
- `description` — Optional summary

## Relationship Properties

Edges include:
- `confidence` — 0–1 score indicating extraction confidence
- `weight` — Count of connections between these nodes
- `extractionMethod` — How detected (ast, semantic, copilot)

## Limitations

- **Neo4j setup required** — Need a running Neo4j instance
- **Learning curve** — Cypher syntax takes practice
- **Performance** — Very large graphs (10K+ nodes) need tuning
- **One-way import** — Changes in Neo4j don't sync back to source

## Next Steps

1. **Explore data** — Use Neo4j Browser's built-in visualizer
2. **Write queries** — Test hypotheses about your architecture
3. **Export results** — Download query results as CSV
4. **Combine datasets** — Merge with other Cypher sources
5. **Visualize** — Use Neo4j's native graph view or export to visualization tools

## See Also

- [Export Formats Overview](export-formats.md)
- [JSON Graph Export](format-json.md) — Raw data in standard format
- [HTML Interactive Viewer](format-html.md) — Quick visual exploration without Neo4j
