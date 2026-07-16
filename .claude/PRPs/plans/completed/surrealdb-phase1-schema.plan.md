# Plan: SurrealDB Phase 1 — Schema Design and Export Adapter

## Summary

Implement a SurrealDB export adapter (`SurrealDbExporter`) that creates a queryable code graph database in embedded file-based mode. This phase establishes the core schema (entities and relationships tables), implements data mapping from `KnowledgeGraph` to SurrealDB records, and integrates the exporter into the CLI pipeline. No external infrastructure required—database lives in a single `.db` file.

## User Story

As a developer analyzing a large codebase,  
I want to export my code graph to a portable, queryable SurrealDB database,  
So that I can query relationships programmatically without parsing JSON or requiring external database infrastructure.

## Problem → Solution

**Before**: Developers must parse JSON files or write Neo4j Cypher scripts to query code structure. Large graphs are slow to search, and sharing requires external hosting.

**After**: A single `codebase.db` file contains the full graph. Query via SurrealQL or integrate with MCP agents. Zero setup—embedded mode, file-based, portable across Windows/macOS/Linux.

## Metadata

- **Complexity**: Large
- **Source PRD**: `plan/graphify-dotnet-surrealdb-prd.md`
- **PRD Phase**: Phase 1 — Schema Design and SurrealDB Adapter
- **Estimated Files**: 4 new files (exporter, tests), 2 modified (CLI, test integration)
- **Estimated Tasks**: 7 tasks
- **Estimated Lines**: 600-800 new code, 100-150 test lines

---

## UX Design

**Internal change — no user-facing UX transformation.** Users invoke the same `dotnet run -- run` command with `--format surrealdb` flag. Output is a `codebase.db` file in the output directory.

---

## Mandatory Reading

| Priority | File | Lines | Why |
|---|---|---|---|
| **P0** | `src/Graphify/Export/JsonExporter.cs` | 1-94 | Exporter pattern: Format property, ExportAsync signature, DTO serialization model |
| **P0** | `src/Graphify/Models/GraphNode.cs` | 1-58 | Node model: all properties must map to SurrealDB records |
| **P0** | `src/Graphify/Models/GraphEdge.cs` | 1-50 | Edge model: Source/Target references, Relationship type, Weight, Confidence, Metadata |
| **P0** | `src/Graphify/Graph/KnowledgeGraph.cs` | 1-80 | Graph API: GetNodes(), GetEdges(), node/edge iteration |
| **P1** | `src/Graphify.Cli/PipelineRunner.cs` | 254-330 | CLI integration: export switch statement, format enumeration, error handling pattern |
| **P1** | `src/tests/Graphify.Tests/Export/JsonExporterTests.cs` | 1-100 | Test structure: Arrange/Act/Assert, temp dir setup, graph validation assertions |
| **P2** | `src/tests/Graphify.Integration.Tests/ExportIntegrationTests.cs` | 30-50 | Integration test pattern: BuildClusteredGraphAsync, export validation |

## External Documentation

| Topic | Source | Key Takeaway |
|---|---|---|
| **SurrealDB Embedded** | https://docs.surrealdb.com/docs/installation/package/dotnet | `surrealdb.net` NuGet package, embedded database initialization (`new Surreal().Connect("file:://path")`) |
| **SurrealDB Schemas** | https://docs.surrealdb.com/docs/surrealql/statements/define/table | DEFINE TABLE syntax, flexible schema, automatic schema creation on first insert |
| **SurrealDB Records** | https://docs.surrealdb.com/docs/surrealql/statements/create | Record creation via CREATE statement, structured fields, record ID format `table:uuid` |
| **SurrealQL Query** | https://docs.surrealdb.com/docs/surrealql/statements/select | SELECT syntax for agent consumption, filtering, record references |

---

## Patterns to Mirror

### NAMING_CONVENTION
// SOURCE: src/Graphify/Export/JsonExporter.cs:1-20
```csharp
namespace Graphify.Export;

public sealed class JsonExporter : IGraphExporter
{
    public string Format => "json";
    public async Task ExportAsync(KnowledgeGraph graph, string outputPath, CancellationToken cancellationToken = default)
}
```
**Pattern**: Sealed class in `Graphify.Export` namespace, implements `IGraphExporter`, lowercase Format, ExportAsync signature, stateless.

### ERROR_HANDLING
// SOURCE: src/Graphify.Cli/PipelineRunner.cs:326-329
```csharp
catch (Exception ex)
{
    await WriteLineAsync($"      Error exporting {format}: {ex.Message}");
}
```
**Pattern**: Log exception message; throw ArgumentException for validation failures; ArgumentNullException.ThrowIfNull for parameters.

### GRAPH_ITERATION
// SOURCE: src/Graphify/Export/JsonExporter.cs:38-62
```csharp
var nodes = graph.GetNodes().Select(n => new NodeDto { /* */ }).ToList();
var edges = graph.GetEdges().Select(e => new EdgeDto { /* */ }).ToList();
```
**Pattern**: Use `graph.GetNodes()` and `graph.GetEdges()` with LINQ Select transformation and ToList().

### DTO_SERIALIZATION
// SOURCE: src/Graphify/Export/JsonExporter.cs:97-107
```csharp
private sealed record GraphExportDto
{
    [JsonPropertyName("nodes")]
    public required List<NodeDto> Nodes { get; init; }
}
```
**Pattern**: Private sealed records for DTOs, required properties, init-only setters.

### TEST_STRUCTURE
// SOURCE: src/tests/Graphify.Tests/Export/JsonExporterTests.cs:9-44
```csharp
[Trait("Category", "Export")]
public sealed class JsonExporterTests : IDisposable
{
    private readonly string _testRoot;
    public JsonExporterTests() { _testRoot = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()); }
    public void Dispose() { try { Directory.Delete(_testRoot, recursive: true); } catch { } }
}
```
**Pattern**: Sealed test class, [Trait], IDisposable for cleanup, Arrange/Act/Assert, ExportAsync_[Scenario]_[Expected] naming.

### VALIDATION_PATTERN
// SOURCE: src/Graphify.Cli/PipelineRunner.cs:244-250
```csharp
var validator = new Graphify.Security.InputValidator();
var outputValidation = validator.ValidatePath(outputDir);
if (!outputValidation.IsValid) throw new ArgumentException($"Invalid output directory: ...");
```
**Pattern**: Use InputValidator for path security; throw ArgumentException with errors.

---

## Files to Change

| File | Action | Justification |
|---|---|---|
| `src/Graphify/Export/SurrealDbExporter.cs` | **CREATE** | Main exporter implementing IGraphExporter |
| `src/tests/Graphify.Tests/Export/SurrealDbExporterTests.cs` | **CREATE** | Unit tests for exporter |
| `src/Graphify.Cli/PipelineRunner.cs` | **UPDATE** | Add "surrealdb" case to export switch (line ~311) |
| `src/Graphify/Graphify.csproj` | **UPDATE** | Add `surrealdb.net` NuGet dependency |
| `src/Graphify.Cli/Program.cs` | **UPDATE** | Update `--format` option description to include "surrealdb" |

## NOT Building

- SurrealDB server deployment or hosting
- Authentication/authorization for remote access
- Performance tuning beyond basic schema
- Migration tools from JSON to SurrealDB
- MCP server integration (Phase 2)
- CLI query interface (Phase 2)

---

## Step-by-Step Tasks

### Task 1: Add surrealdb.net NuGet Dependency

- **ACTION**: Add the `surrealdb.net` NuGet package to the project
- **IMPLEMENT**: Add package reference to `src/Graphify/Graphify.csproj`:
  ```xml
  <PackageReference Include="surrealdb.net" Version="1.0.0" />
  ```
  Run `dotnet restore` to verify resolution.
- **MIRROR**: VALIDATION_PATTERN (ensure package resolves)
- **IMPORTS**: Will be used in SurrealDbExporter.cs
- **GOTCHA**: surrealdb.net may be pre-1.0; monitor for breaking changes. Check latest version on NuGet.org.
- **VALIDATE**: `dotnet list package --outdated` shows surrealdb.net with correct version

### Task 2: Create SurrealDbExporter Class Structure

- **ACTION**: Create base `SurrealDbExporter` class implementing IGraphExporter
- **IMPLEMENT**: Create `src/Graphify/Export/SurrealDbExporter.cs`:
  ```csharp
  using Graphify.Graph;
  
  namespace Graphify.Export;
  
  public sealed class SurrealDbExporter : IGraphExporter
  {
      public string Format => "surrealdb";
  
      public async Task ExportAsync(KnowledgeGraph graph, string outputPath, 
          CancellationToken cancellationToken = default)
      {
          ArgumentNullException.ThrowIfNull(graph);
          ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
          
          // Implementation in Tasks 3-6
      }
  }
  ```
- **MIRROR**: NAMING_CONVENTION (sealed class, namespace, Format property, ExportAsync signature)
- **IMPORTS**: `using Graphify.Graph;`, `using SurrealDb.Net;` (or appropriate namespace)
- **GOTCHA**: Method signature must match IGraphExporter exactly; outputPath is file path not directory
- **VALIDATE**: Code compiles; Format returns "surrealdb"; signature matches interface

### Task 3: Initialize SurrealDB Connection in Embedded Mode

- **ACTION**: Implement embedded database initialization
- **IMPLEMENT**: In ExportAsync:
  ```csharp
  var directory = Path.GetDirectoryName(outputPath);
  if (!string.IsNullOrEmpty(directory))
  {
      Directory.CreateDirectory(directory);
  }
  
  var db = new Surreal();
  await db.Connect($"file://{Path.GetFullPath(outputPath)}", cancellationToken);
  await db.Use("codebase", "graph", cancellationToken);
  ```
- **MIRROR**: VALIDATION_PATTERN (validate path, create directory)
- **IMPORTS**: `using SurrealDb.Net;`
- **GOTCHA**: Connection string format may vary; test local file path format. Ensure connection is disposed or used in using block.
- **VALIDATE**: Database file created at outputPath; connection completes without errors

### Task 4: Define SurrealDB Schema (Tables and Fields)

- **ACTION**: Create SurrealQL DEFINE TABLE statements
- **IMPLEMENT**: After connection, in ExportAsync:
  ```csharp
  const string entityTableDef = """
      DEFINE TABLE entities SCHEMALESS;
      DEFINE FIELD id ON entities TYPE string;
      DEFINE FIELD label ON entities TYPE string;
      DEFINE FIELD kind ON entities TYPE string;
      DEFINE FIELD filePath ON entities TYPE string;
      DEFINE FIELD language ON entities TYPE string;
      DEFINE FIELD confidence ON entities TYPE string;
      DEFINE FIELD community ON entities TYPE int;
      DEFINE FIELD metadata ON entities TYPE object;
      """;
  
  const string relationshipTableDef = """
      DEFINE TABLE relationships SCHEMALESS;
      DEFINE FIELD source ON relationships TYPE record(entities);
      DEFINE FIELD target ON relationships TYPE record(entities);
      DEFINE FIELD type ON relationships TYPE string;
      DEFINE FIELD weight ON relationships TYPE float;
      DEFINE FIELD confidence ON relationships TYPE string;
      DEFINE FIELD metadata ON relationships TYPE object;
      """;
  
  await db.RawQuery(entityTableDef, cancellationToken);
  await db.RawQuery(relationshipTableDef, cancellationToken);
  ```
- **MIRROR**: Exporter pattern (idempotent setup)
- **IMPORTS**: No new imports; schema is string DDL
- **GOTCHA**: Use SCHEMALESS for flexibility (GraphNode has optional properties). Line number is optional; omit or make nullable.
- **VALIDATE**: Schema created without errors; subsequent CREATE statements work

### Task 5: Map KnowledgeGraph Nodes to SurrealDB Records

- **ACTION**: Transform GraphNode objects into SurrealQL CREATE statements
- **IMPLEMENT**: After schema, in ExportAsync:
  ```csharp
  var nodes = graph.GetNodes().ToList();
  
  foreach (var node in nodes)
  {
      var record = new SurrealDbNodeRecord
      {
          Id = node.Id,
          Label = node.Label,
          Kind = node.Type,
          FilePath = node.FilePath,
          Language = node.Language,
          Confidence = node.Confidence.ToString().ToUpperInvariant(),
          Community = node.Community,
          Metadata = node.Metadata
      };
      
      await db.Create($"entities:{Uri.EscapeDataString(node.Id)}", record, cancellationToken);
  }
  ```
  With record type:
  ```csharp
  private sealed record SurrealDbNodeRecord
  {
      public required string Id { get; init; }
      public required string Label { get; init; }
      public string? Kind { get; init; }
      public string? FilePath { get; init; }
      public string? Language { get; init; }
      public string? Confidence { get; init; }
      public int? Community { get; init; }
      public IReadOnlyDictionary<string, string>? Metadata { get; init; }
  }
  ```
- **MIRROR**: GRAPH_ITERATION pattern (GetNodes().ToList()), DTO_SERIALIZATION pattern
- **IMPORTS**: No new imports beyond Task 3
- **GOTCHA**: Use `Uri.EscapeDataString()` for URL-safe node IDs. Record ID: `table:id` format. Re-running export creates duplicates (handle with DELETE-then-CREATE or UPSERT).
- **VALIDATE**: All nodes inserted; record IDs unique and URL-escaped; SELECT * FROM entities returns all nodes

### Task 6: Map KnowledgeGraph Edges to SurrealDB Records

- **ACTION**: Transform GraphEdge objects into SurrealQL CREATE statements
- **IMPLEMENT**: After nodes, in ExportAsync:
  ```csharp
  var edges = graph.GetEdges().ToList();
  
  foreach (var edge in edges)
  {
      var record = new SurrealDbEdgeRecord
      {
          Source = $"entities:{Uri.EscapeDataString(edge.Source.Id)}",
          Target = $"entities:{Uri.EscapeDataString(edge.Target.Id)}",
          Type = edge.Relationship,
          Weight = edge.Weight,
          Confidence = edge.Confidence.ToString().ToUpperInvariant(),
          Metadata = edge.Metadata
      };
      
      await db.Create($"relationships:{Guid.NewGuid()}", record, cancellationToken);
  }
  ```
  With record type:
  ```csharp
  private sealed record SurrealDbEdgeRecord
  {
      public required string Source { get; init; }
      public required string Target { get; init; }
      public required string Type { get; init; }
      public double Weight { get; init; } = 1.0;
      public string? Confidence { get; init; }
      public IReadOnlyDictionary<string, string>? Metadata { get; init; }
  }
  ```
- **MIRROR**: GRAPH_ITERATION pattern, DTO_SERIALIZATION pattern
- **IMPORTS**: No new imports
- **GOTCHA**: Source/Target must reference existing entities (insert nodes first). Use Guid for unique relationship IDs. Weight defaults to 1.0.
- **VALIDATE**: All edges inserted; Source/Target references valid; SELECT * FROM relationships shows all edges

### Task 7: Integrate SurrealDB into CLI Export Switch

- **ACTION**: Add "surrealdb" case to PipelineRunner export switch
- **IMPLEMENT**: In `src/Graphify.Cli/PipelineRunner.cs` line ~311, add before `default`:
  ```csharp
  case "surrealdb":
      var surrealDbExporter = new SurrealDbExporter();
      var surrealDbPath = Path.Combine(outputDir, "codebase.db");
      await surrealDbExporter.ExportAsync(graph, surrealDbPath, cancellationToken);
      await WriteLineAsync($"      Exported SurrealDB: {surrealDbPath}{FormatWithElapsed(formatStopwatch.Elapsed)}");
      break;
  ```
  Also update `Program.cs` line 33 to add "surrealdb" to format description:
  ```csharp
  Description = "Export formats (comma-separated): json, html, svg, neo4j, ladybug, obsidian, wiki, surrealdb, report",
  ```
- **MIRROR**: CLI integration pattern (instantiate, construct path, call ExportAsync, log)
- **IMPORTS**: Add `using Graphify.Export;` if not present
- **GOTCHA**: Output filename hardcoded as `codebase.db` (follows pattern of graph.json, graph.html). Format string in help text must match.
- **VALIDATE**: CLI accepts `--format surrealdb`; help text lists it; PipelineRunner compiles and exports

---

## Testing Strategy

### Unit Tests (`SurrealDbExporterTests.cs`)

| Test | Input | Expected | Edge Case? |
|---|---|---|---|
| Format | N/A | "surrealdb" | No |
| ValidGraph_ProducesDatabase | 3 nodes, 2 edges | File exists; can query | No |
| NodeCounts_Match | 10 nodes | 10 entities records | No |
| EdgeCounts_Match | 5 edges | 5 relationships records | No |
| EmptyGraph_CreatesDatabase | Empty KnowledgeGraph | Database, empty tables | Yes |
| CreatesDirectory_IfNotExists | Nested path | Directories created | No |
| SpecialCharacters_InNodeId | ID with `::`, `\`, `.` | URL-escaped in record ID | Yes |
| Metadata_Preserved | Custom metadata dict | Fields persisted | No |
| Confidence_Exported | Confidence.Inferred | "INFERRED" string | No |
| Community_Assignments_Exported | Community IDs | Community field populated | No |
| LargeGraph_Completes | 500 nodes, 300 edges | All inserted, no timeout | Yes |

### Edge Cases
- [ ] Empty graph
- [ ] Special characters in node ID
- [ ] Missing optional properties (null FilePath, Metadata, Community)
- [ ] Large graphs (500+ nodes)
- [ ] Re-running export (duplicate handling)
- [ ] Invalid output path
- [ ] Concurrent exports
- [ ] Unicode in labels

---

## Validation Commands

### Build
```bash
dotnet build
```
EXPECT: Zero errors; SurrealDbExporter, PipelineRunner, tests compile.

### Unit Tests
```bash
dotnet test src/tests/Graphify.Tests/Export/SurrealDbExporterTests.cs -v
```
EXPECT: All tests pass.

### Integration Tests
```bash
dotnet test src/tests/Graphify.Integration.Tests/ -v --filter "SurrealDb"
```
EXPECT: Integration tests pass.

### Full Test Suite
```bash
dotnet test
```
EXPECT: All tests pass; no regressions.

### CLI
```bash
cd src/Graphify.Cli
dotnet run -- run --format surrealdb --output test-out .
ls test-out/codebase.db
```
EXPECT: File created; export successful.

### Manual
- [ ] Export real codebase to SurrealDB
- [ ] Verify `codebase.db` exists and > 0 bytes
- [ ] Query database (if SurrealDB CLI available)
- [ ] Verify node/edge counts match JSON export

---

## Acceptance Criteria

- [ ] SurrealDbExporter created in `src/Graphify/Export/`
- [ ] Implements IGraphExporter with Format="surrealdb"
- [ ] Database file created in embedded mode
- [ ] Schema with entities and relationships tables defined
- [ ] All nodes inserted as entities records
- [ ] All edges inserted as relationships records
- [ ] CLI integration: `--format surrealdb` works
- [ ] Unit tests cover all scenarios
- [ ] Integration tests verify queryable output
- [ ] No type errors; all tests pass
- [ ] No regressions in existing exporters
- [ ] Help text updated

## Completion Checklist

- [ ] Code follows discovered patterns
- [ ] Error handling matches codebase style
- [ ] No hardcoded values except Format, filename
- [ ] Tests follow established patterns
- [ ] XML docs on public methods
- [ ] No unnecessary scope additions
- [ ] Self-contained; all context captured

---

## Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| **surrealdb.net API instability** | Medium | Pre-1.0; API may break | Pin version; monitor releases |
| **Duplicate records on re-run** | High | Duplicates without idempotency | Implement DELETE-then-CREATE or UPSERT |
| **Large graph performance** | Medium | 10k+ nodes slow to insert | Batch inserts if supported; perf test |
| **Record ID encoding** | Medium | Special chars break SurrealQL | Uri.EscapeDataString; test with special chars |
| **Connection lifecycle** | Low | Database file locked | Use using statement or try/finally |

---

## Notes

- **Phase 1 scope**: Schema and data adapter only. Query interface and MCP integration are Phase 2.
- **Idempotency**: Plan for re-running extraction. Use DELETE-then-CREATE or UPSERT logic.
- **SurrealDB NuGet**: Check latest version and breaking changes before adding.
- **Performance**: Sequential insertion is simple; optimize in later phase if needed.
- **Portability**: Embedded file-based mode ensures no external infrastructure.

---

## Implementation Success Criteria

A developer unfamiliar with graphify-dotnet should be able to implement this plan using ONLY this document, without searching the codebase or asking questions. If any context is missing, it's a sign the plan is incomplete.
