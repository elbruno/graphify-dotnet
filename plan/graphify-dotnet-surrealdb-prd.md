# graphify-dotnet SurrealDB Output Support

## Overview
Add SurrealDB as an output backend to graphify-dotnet, enabling developers to store code graphs in a portable, serverless database that integrates seamlessly with coding agents (Claude Code, OpenCode, Copilot).

## Problem Statement
Currently, graphify-dotnet outputs only to JSON files. For large codebases, JSON becomes difficult to query and share with agents. Developers need a portable, zero-infrastructure solution that works on any machine without requiring VPS hosting or complex setup.

## Goals
- Add SurrealDB as a first-class output option alongside JSON
- Maintain backward compatibility with existing JSON output
- Enable querying code graphs via agents using SurrealDB's queryable interface
- Require zero external infrastructure (single binary, file-based or in-process)
- Keep the solution portable across Windows, macOS, and Linux

## Requirements

### Functional
1. **Output Type Flag**: Add `--output-type surrealdb` CLI argument to graphify-dotnet
2. **Database Schema**: Design SurrealDB tables/records for:
   - Code entities (classes, methods, properties, interfaces)
   - Relationships (calls, inherits, implements, references)
   - Metadata (file path, line number, semantic embeddings if applicable)
3. **Data Mapping**: Map existing Roslyn AST extraction to SurrealDB records
4. **Connection Modes**:
   - Embedded mode: File-based database (default: `./codebase.db`)
   - Server mode: HTTP endpoint for remote agents (optional, for future use)
5. **Query Interface**: Support basic SurrealQL queries for agent consumption via MCP server
6. **Idempotency**: Support re-running extraction without duplicate records

### Non-Functional
- Performance: Handle codebases with 10k+ entities without degradation
- Portability: Single executable, no external dependencies beyond NuGet
- Compatibility: .NET 6+
- Documentation: Clear examples of querying the graph from agents

## Scope

### In Scope
- SurrealDB output backend implementation
- CLI flag for output type selection
- Schema design and record insertion logic
- Basic integration tests
- README section documenting SurrealDB output usage

### Out of Scope
- SurrealDB server deployment/hosting guidance
- Authentication/authorization for remote access
- Performance optimization beyond basic indexing
- Migration tools from JSON to SurrealDB

## Technical Approach

### Architecture
```
Roslyn AST Extraction
         ↓
   Graph Model (existing)
         ↓
   Output Adapter (new)
      ↙      ↘
   JSON      SurrealDB
  (existing)  (new)
```

### SurrealDB Schema (Example)
```surql
-- Entities table
DEFINE TABLE entities AS SELECT * FROM entities;
DEFINE FIELD id ON entities TYPE string;
DEFINE FIELD name ON entities TYPE string;
DEFINE FIELD kind ON entities TYPE string; -- class, method, interface, etc.
DEFINE FIELD filePath ON entities TYPE string;
DEFINE FIELD lineNumber ON entities TYPE int;
DEFINE FIELD namespace ON entities TYPE string;

-- Relationships table
DEFINE TABLE relationships AS SELECT * FROM relationships;
DEFINE FIELD source ON relationships TYPE record(entities);
DEFINE FIELD target ON relationships TYPE record(entities);
DEFINE FIELD type ON relationships TYPE string; -- calls, inherits, implements, references
```

### Implementation Steps
1. Create `SurrealDbOutputAdapter` class implementing output interface
2. Add dependency: `surrealdb.net` NuGet package
3. Initialize SurrealDB connection in embedded mode
4. Implement entity and relationship insertion logic
5. Add CLI option parsing for `--output-type`
6. Add integration tests
7. Update README with usage examples

### Configuration
```json
{
  "outputType": "surrealdb",
  "surrealdb": {
    "databasePath": "./codebase.db",
    "namespace": "codebase",
    "database": "graph"
  }
}
```

## Success Criteria
- ✅ SurrealDB output produces queryable code graph
- ✅ File-based database works on Windows, macOS, Linux without setup
- ✅ Existing JSON output continues to work
- ✅ Graph can be queried via simple SurrealQL statements for agent use
- ✅ Re-running extraction does not create duplicates
- ✅ Documentation includes agent integration examples

## Timeline
- Phase 1: Schema design and SurrealDB adapter (2-3 days)
- Phase 2: CLI integration and testing (1-2 days)
- Phase 3: Documentation and examples (1 day)

## Dependencies
- `surrealdb.net` NuGet package
- No breaking changes to existing graphify-dotnet API

## Rollout
1. Merge to feature branch
2. Test with sample codebases
3. Merge to main with both JSON and SurrealDB tests passing
4. Release as minor version bump
