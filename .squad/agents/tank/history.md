# Project Context

- **Owner:** Bruno Capuano
- **Project:** graphify-dotnet — A .NET 10 port of safishamsi/graphify, a Python AI coding assistant skill that reads files, builds a knowledge graph, and surfaces hidden structure. Uses GitHub Copilot SDK and Microsoft.Extensions.AI for semantic extraction.
- **Stack:** .NET 10, C#, GitHub Copilot SDK, Microsoft.Extensions.AI, Roslyn (AST parsing), xUnit, NuGet
- **Source:** https://github.com/safishamsi/graphify — Python pipeline: detect → extract → build_graph → cluster → analyze → report → export. Uses NetworkX, tree-sitter, Leiden community detection, vis.js.
- **Created:** 2026-04-06

## Learnings

### 2026-04-06: Phase 1 Unit Test Coverage

Created comprehensive xUnit test suite for Phase 1 infrastructure modules:

**Cache Tests (`SemanticCacheTests.cs`)**: 
- Hash computation (same content → same hash, different content → different hash)
- Round-trip cache operations (set/get)
- IsChanged detection (unchanged/modified/deleted files)  
- Cache miss handling
- Corrupt cache recovery
- ICacheProvider contract implementation (SetAsync, GetAsync, ExistsAsync, InvalidateAsync, ClearAsync)
- Cache persistence (index survives across instances)

**Security Tests (`InputValidatorTests.cs`)**:
- URL validation (allowed schemes, blocked private IPs/localhost)
- Path validation (traversal prevention, null byte detection, base directory containment)
- Label sanitization (HTML/script stripping, control char removal, length limiting, HTML encoding)
- Input validation (length checks, null byte detection, control char detection, injection pattern detection)

**Validation Tests (`ExtractionValidatorTests.cs`)**:
- Valid extraction results pass
- Empty results pass
- Node validation (Id, Label, SourceFile presence)
- Edge validation (Source, Target, Relation presence, node ID matching)
- Null collection handling  
- Multiple error aggregation

**Graph Tests (`KnowledgeGraphTests.cs`)**:
- Node operations (Add, Get, duplicate handling)
- Edge operations (Add with node existence checks, Get)
- Neighbor queries
- Degree calculations
- Highest degree node rankings
- Community assignment
- Graph merging (with overwrite semantics)
- QuikGraph integration

**Pipeline Tests (`FileDetectorTests.cs`)**:
- File discovery in directory trees
- Extension filtering
- Max file size enforcement
- File categorization (Code, Documentation, Media)
- Language detection from extensions
- Skip directories (node_modules, bin, obj, .git, etc.)
- Hidden file/directory exclusion
- Pattern-based exclusion
- Relative path calculation
- Results sorted by path

**Key decisions**:
- Used `IDisposable` pattern with `Path.GetTempPath() + Path.GetRandomFileName()` for filesystem isolation
- Split Theory test for localhost URLs into separate Facts to handle different error messages (localhost string vs private IP message)
- Avoided `\x00` in control character test due to string handling quirks
- All tests use concrete assertions—no snapshot testing
- Temp directory cleanup is best-effort (ignores exceptions)

<!-- Append new learnings below. Each entry is something lasting about the project. -->
