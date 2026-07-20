# SurrealDB Export

> Export your knowledge graph as a SurrealDB database — queryable, embeddable, and real-time.

## Overview

The SurrealDB exporter creates a SurrealDB database with:

- **`entity` table** — each node in the graph becomes an entity record with typed metadata
- **`relationship` table** — each edge with source/target record links, relationship type, weight, and confidence

## Modes

### Embedded (RocksDB)

No server required. Produces a `codebase.db` file using SurrealDB's embedded RocksDB engine.

```bash
graphify run ./src --format surrealdb
# Creates: graphify-out/codebase.db
```

### Remote

Connect to any SurrealDB instance over HTTP or WebSocket.

**PowerShell:**

```powershell
graphify run ./src --format surrealdb `
    --surreal-endpoint http://localhost:8000 `
    --surreal-user root `
    --surreal-pass mypassword `
    --surreal-ns graphify `
    --surreal-db codebase
```

**Bash:**

```bash
graphify run ./src --format surrealdb \
    --surreal-endpoint http://localhost:8000 \
    --surreal-user root \
    --surreal-pass mypassword \
    --surreal-ns graphify \
    --surreal-db codebase
```

### CLI Options

| Option | Default | Description |
|--------|---------|-------------|
| `--surreal-endpoint` | *(none)* | Remote SurrealDB URL. When set, uses remote mode instead of embedded. |
| `--surreal-user` | `root` | SurrealDB username |
| `--surreal-pass` | *(none)* | SurrealDB password |
| `--surreal-ns` | `graphify` | SurrealDB namespace |
| `--surreal-db` | `codebase` | SurrealDB database name |

## Querying

### Embedded database

Use the SurrealDB CLI or any SurrealDB client to query:

```bash
# Start SurrealDB CLI against the embedded file
surreal sql --endpoint rocksdb://graphify-out/codebase.db --ns graphify --db codebase
```

```surql
-- Find all entities in a community
SELECT * FROM entity WHERE community = 3;

-- Find relationships with high confidence
SELECT * FROM relationship WHERE confidence = 'HIGH';

-- Count relationships per type
SELECT type, count() AS cnt FROM relationship GROUP BY type ORDER BY cnt DESC;
```

### Remote database

Connect your SurrealDB client to the endpoint:

```bash
surreal sql --endpoint http://localhost:8000 --ns graphify --db codebase --user root --pass mypassword
```

## Schema

```surql
DEFINE TABLE IF NOT EXISTS entity;
DEFINE TABLE IF NOT EXISTS relationship;
```

### Entity fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | `record<entity>` | Unique entity identifier |
| `label` | `string` | Human-readable name |
| `kind` | `string` | Node type (class, method, file, etc.) |
| `filePath` | `string` | Source file path |
| `language` | `string` | Programming language |
| `confidence` | `string` | Extraction confidence (LOW, MEDIUM, HIGH) |
| `community` | `int` | Community cluster ID |
| `metadata` | `object` | Additional key-value metadata |

### Relationship fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | `record<relationship>` | Unique relationship identifier |
| `source` | `record<entity>` | Source entity reference |
| `target` | `record<entity>` | Target entity reference |
| `type` | `string` | Relationship type (calls, extends, contains, etc.) |
| `weight` | `float` | Edge weight |
| `confidence` | `string` | Relationship confidence (LOW, MEDIUM, HIGH) |
| `metadata` | `object` | Additional key-value metadata |

## Use Cases

- **Real-time dashboards** — SurrealDB's live queries push graph changes to the UI
- **Multi-model queries** — combine graph traversal with document and relational queries in a single SurrealQL statement
- **Embedded analytics** — run on developers' machines without any infrastructure
- **CI/CD integration** — compare graph snapshots across builds

## See Also

- [Export Formats Overview](export-formats.md)
- [Neo4j Cypher Export](format-neo4j.md)
- [Ladybug Export](format-ladybug.md)
