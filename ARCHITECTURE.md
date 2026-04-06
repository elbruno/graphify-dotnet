# ARCHITECTURE.md

## Overview

graphify-dotnet is a .NET 10 port of safishamsi/graphify, implementing an AI-powered knowledge graph builder for codebases. This document describes the system architecture, design decisions, and mappings from the Python source to .NET implementations.

## Core Philosophy

- **Composition over inheritance**: Use interfaces and dependency injection instead of deep class hierarchies
- **Immutable data structures**: Records for nodes/edges ensure thread-safety
- **.NET idioms**: `IOptions<T>`, `ILogger<T>`, `async/await`, `CancellationToken`
- **Type safety**: Strong typing with nullable reference types enabled
- **Pipeline pattern**: Each stage is a discrete `IPipelineStage<TIn, TOut>` implementation

## System Architecture

### Pipeline Overview

```
FileDetector -> Extractor -> GraphBuilder -> ClusterEngine -> Analyzer -> ReportGenerator -> IGraphExporter[]
     |             |              |               |              |             |                 |
  Detect       Extract         Build          Cluster       Analyze       Report           Export
  Files        Features        Graph         (Louvain)      Metrics      Summary          Formats
```

Each stage is decoupled and testable. Output from one stage feeds into the next via well-defined data models.

## Project Structure

```
graphify-dotnet/
├── src/Graphify/                   # Core library
│   ├── Models/                     # Data models
│   │   ├── DetectedFile.cs         # File detection result
│   │   ├── ExtractedNode.cs        # Raw extracted node (string IDs)
│   │   ├── ExtractedEdge.cs        # Raw extracted edge (string IDs)
│   │   ├── ExtractionResult.cs     # Extraction output container
│   │   ├── GraphNode.cs            # Graph node (object references)
│   │   ├── GraphEdge.cs            # Graph edge (IEdge<GraphNode>)
│   │   ├── AnalysisResult.cs       # Analysis metrics
│   │   ├── GraphReport.cs          # Human-readable report
│   │   ├── Confidence.cs           # EXTRACTED | INFERRED | AMBIGUOUS
│   │   └── FileType.cs             # Code | Document | Paper | Image
│   ├── Pipeline/                   # Pipeline stages
│   │   ├── IPipelineStage.cs       # Base interface
│   │   ├── FileDetector.cs         # Stage 1: Detect files
│   │   ├── Extractor.cs            # Stage 2: Extract (hybrid)
│   │   ├── GraphBuilder.cs         # Stage 3: Build graph
│   │   ├── ClusterEngine.cs        # Stage 4: Community detection
│   │   ├── Analyzer.cs             # Stage 5: Metrics & analysis
│   │   ├── ReportGenerator.cs      # Stage 6: Generate report
│   │   ├── SemanticExtractor.cs    # AI semantic extraction
│   │   ├── ExtractionPrompts.cs    # Prompt templates
│   │   └── BenchmarkRunner.cs      # Token reduction benchmarks
│   ├── Graph/                      # Graph data structures
│   │   └── KnowledgeGraph.cs       # QuikGraph wrapper
│   ├── Export/                     # Export implementations
│   │   ├── IGraphExporter.cs       # Base exporter interface
│   │   ├── JsonExporter.cs         # graph.json
│   │   ├── HtmlExporter.cs         # graph.html (vis.js)
│   │   ├── SvgExporter.cs          # graph.svg
│   │   ├── WikiExporter.cs         # wiki/ markdown articles
│   │   ├── ObsidianExporter.cs     # obsidian-vault/
│   │   └── Neo4jExporter.cs        # cypher.txt or direct push
│   ├── Cache/                      # SHA256 caching
│   │   ├── ICacheProvider.cs       # Cache interface
│   │   ├── SemanticCache.cs        # File hash cache
│   │   └── CacheEntry.cs           # Cache entry model
│   ├── Validation/                 # Schema validation
│   │   ├── IGraphValidator.cs      # Validator interface
│   │   ├── ExtractionValidator.cs  # Node/edge validation
│   │   └── ValidationResult.cs     # Validation result
│   ├── Security/                   # Input sanitization
│   │   ├── ISecurityValidator.cs   # Security interface
│   │   └── InputValidator.cs       # Path traversal checks
│   └── Ingest/                     # URL ingestion
│       ├── IDataIngester.cs        # Ingester interface
│       └── UrlIngester.cs          # Fetch papers/tweets
├── src/Graphify.Cli/               # Console application
│   ├── Program.cs                  # CLI entry point
│   └── PipelineRunner.cs           # Pipeline orchestration
├── src/Graphify.Sdk/               # GitHub Copilot SDK integration
│   └── CopilotExtractor.cs         # Copilot-specific extractors
├── src/Graphify.Mcp/               # MCP stdio server
│   └── McpServer.cs                # ModelContextProtocol server
└── src/tests/                      # Test projects
    ├── Graphify.Tests/             # Unit tests
    └── Graphify.Integration.Tests/ # Integration tests
```

## Data Model

### Extraction Layer (String IDs)

**ExtractedNode**
- `Id`: Unique node identifier (string)
- `Label`: Display name
- `FileType`: Code | Document | Paper | Image
- `SourceFile`: Origin file path
- `SourceLocation`: Optional line/column
- `Metadata`: Dictionary<string, string>

**ExtractedEdge**
- `Source`: Source node ID (string)
- `Target`: Target node ID (string)
- `Relation`: Relationship type (e.g., "calls", "imports", "semantically_similar_to")
- `Confidence`: EXTRACTED | INFERRED | AMBIGUOUS
- `SourceFile`: Origin file
- `SourceLocation`: Optional line/column
- `Weight`: Edge weight (default 1.0)

**ExtractionResult**
- `Nodes`: List<ExtractedNode>
- `Edges`: List<ExtractedEdge>
- `RawText`: Original text content
- `SourceFile`: File path
- `ExtractionMethod`: Ast | Semantic | Hybrid
- `Timestamp`: Extraction time
- `ConfidenceScores`: Dictionary<string, double>

### Graph Layer (Object References)

**GraphNode**
- `Id`: Unique identifier (string)
- `Type`: Node type
- `Community`: Optional community ID (assigned by clustering)
- `Metadata`: IReadOnlyDictionary<string, string>

**GraphEdge : IEdge<GraphNode>**
- `Source`: Source GraphNode reference
- `Target`: Target GraphNode reference
- `Relationship`: Relation type
- `Confidence`: Confidence enum
- `Weight`: Edge weight

**KnowledgeGraph**
Wraps QuikGraph's `BidirectionalGraph<GraphNode, GraphEdge>` with domain-specific methods:
- `AddNode(GraphNode node)`
- `AddEdge(GraphEdge edge)`
- `GetNodeById(string id)`
- `GetNodesByCommunity(int communityId)`
- `AssignCommunities(Dictionary<string, int> assignments)`
- `MergeGraph(KnowledgeGraph other)`

## Pipeline Stages

### 1. FileDetector

**Responsibility**: Scan directories and categorize files

**Inputs**: Root directory path, options (ignore patterns, max file size)

**Outputs**: `List<DetectedFile>`

**Implementation**:
- Recursive directory traversal with configurable ignore patterns
- File type detection based on extension
- Size filtering (skip files > max size)
- Git-aware (respects .gitignore if present)

**Python Mapping**: `detect.py`

### 2. Extractor (Hybrid)

**Responsibility**: Extract nodes and edges from files

**Inputs**: `List<DetectedFile>`

**Outputs**: `List<ExtractionResult>`

**Implementation**:
Two extraction paths:

#### AST Extraction (Code Files)
- Uses `TreeSitter.Bindings` for multi-language AST parsing
- Extracts:
  - Classes, functions, methods
  - Import/require statements
  - Call graphs (function → function edges)
  - Docstrings and rationale comments (`// NOTE:`, `// WHY:`, etc.)
- All edges marked `Confidence.Extracted`

#### Semantic Extraction (Docs/Images)
- Uses `Microsoft.Extensions.AI` with `IChatClient` abstraction
- Supports any compatible provider (OpenAI, Anthropic, Azure OpenAI)
- Extracts:
  - Concepts and entities
  - Relationships between concepts
  - Design rationale
  - Confidence scores for inferences
- Images processed via vision models
- PDF text extraction via standard libraries

**Python Mapping**: `extract.py`, `semantic_extractor.py`

### 3. GraphBuilder

**Responsibility**: Assemble nodes and edges into a graph

**Inputs**: `List<ExtractionResult>`

**Outputs**: `KnowledgeGraph`

**Implementation**:
- Validate all extraction results via `ExtractionValidator`
- Deduplicate nodes by ID (merge metadata)
- Resolve edge string IDs to GraphNode references
- Build `BidirectionalGraph<GraphNode, GraphEdge>`
- Wrap in `KnowledgeGraph` domain model

**Python Mapping**: `build_graph.py`

### 4. ClusterEngine

**Responsibility**: Community detection via Louvain algorithm

**Inputs**: `KnowledgeGraph`

**Outputs**: `KnowledgeGraph` (with community assignments)

**Implementation**:
- Convert graph to adjacency format required by clustering library
- Apply Louvain community detection
- Assign community IDs to nodes via `AssignCommunities()`
- Calculate modularity score

**Library**: [Microsoft.Research.GraphCluster](https://www.nuget.org/packages/Microsoft.Research.GraphCluster) or [Accord.NET](https://www.nuget.org/packages/Accord.MachineLearning/)

**Python Mapping**: `cluster.py` (uses `graspologic` for Leiden)

### 5. Analyzer

**Responsibility**: Calculate graph metrics and identify key nodes

**Inputs**: `KnowledgeGraph` (clustered)

**Outputs**: `AnalysisResult`

**Implementation**:
Uses QuikGraph algorithm library:
- **Degree centrality**: Identify "god nodes" (highest degree)
- **Betweenness centrality**: Nodes on shortest paths
- **Surprising connections**: Cross-community edges weighted by rarity
- **Community statistics**: Size, density, inter-community edges

**Python Mapping**: `analyze.py`

### 6. ReportGenerator

**Responsibility**: Generate human-readable summary

**Inputs**: `AnalysisResult`, `KnowledgeGraph`

**Outputs**: `GraphReport` (GRAPH_REPORT.md)

**Implementation**:
- Format top god nodes with descriptions
- List surprising connections with "why" explanations
- Generate suggested questions based on graph structure
- Token reduction benchmark vs reading raw files

**Python Mapping**: `report.py`

### 7. Export (Multiple Implementations)

**Responsibility**: Serialize graph to various formats

**Interface**: `IGraphExporter`

```csharp
public interface IGraphExporter
{
    Task ExportAsync(KnowledgeGraph graph, string outputPath, CancellationToken cancellationToken = default);
}
```

**Implementations**:

#### JsonExporter
- Serialize entire graph to `graph.json`
- Include nodes, edges, communities, metadata, analysis results

#### HtmlExporter
- Generate interactive vis.js visualization
- Embed HtmlTemplate resource
- Color nodes by community
- Support click, zoom, search, filter

#### SvgExporter
- Render static SVG using force-directed layout
- Suitable for documentation embedding

#### WikiExporter
- Generate `wiki/` directory with markdown articles
- One article per community
- `index.md` entry point with navigation

#### ObsidianExporter
- Create Obsidian vault with backlinks
- Node files use `[[wikilinks]]` syntax
- Graph view compatible

#### Neo4jExporter
- Generate `cypher.txt` with CREATE statements
- Optional direct push via Bolt protocol

**Python Mapping**: `export.py`

## AI Integration

### Microsoft.Extensions.AI Abstraction

graphify-dotnet uses the `Microsoft.Extensions.AI` abstraction layer for LLM interactions:

```csharp
public interface IChatClient
{
    Task<ChatCompletion> GetChatCompletionAsync(ChatMessage[] messages, CancellationToken cancellationToken = default);
}
```

**Benefits**:
- Provider-agnostic (OpenAI, Anthropic, Azure OpenAI, local models)
- Testable via mock implementations
- Consistent API across all semantic extraction stages

**Configuration**:
- Inject `IChatClient` via DI
- Configure provider in `appsettings.json` or environment variables
- Supports Azure OpenAI managed identity

### Semantic Extraction Prompts

Located in `Pipeline/ExtractionPrompts.cs`:
- Document concept extraction
- Image/diagram analysis
- Design rationale extraction
- Confidence scoring guidelines

## Graph Data Structure

### QuikGraph Integration

**Why QuikGraph?**
- Mature, stable library for .NET graph algorithms
- Generic design: works with any `IEdge<TVertex>`
- Bidirectional edges: O(1) access to in/out edges
- Algorithm library: betweenness centrality, shortest paths, topological sort

**BidirectionalGraph<GraphNode, GraphEdge>**:
- Vertices: `GraphNode` instances
- Edges: `GraphEdge` implementing `IEdge<GraphNode>`
- Parallel edges allowed (same source/target, different relation)

### KnowledgeGraph Wrapper

**Why wrap QuikGraph?**
- Domain-specific API (`GetNodesByCommunity()` vs raw QuikGraph)
- Node indexing by string ID (QuikGraph only indexes by object reference)
- Node replacement semantics (remove + add cycle hidden from caller)
- Future-proofing: swap graph library without changing pipeline code

**Trade-offs**:
- Immutability cost: Updating node properties requires remove+add for all edges
- Acceptable because clustering happens once per pipeline run
- Deduplication is caller's responsibility for parallel edges

## Caching

### SHA256 File Hashing

**SemanticCache** tracks file changes:
- Hash each file's content with SHA256
- Store hash → ExtractionResult mapping
- On `--update`, only re-extract files with changed hashes
- AST extraction is fast enough to skip cache (no LLM cost)

**Python Mapping**: `cache/` directory with SHA256 entries

## Security & Validation

### Input Validation

**InputValidator**:
- Path traversal prevention (reject `..`)
- Maximum file size limits
- Allowed file extension whitelist
- Sanitize user-provided identifiers

### Extraction Validation

**ExtractionValidator**:
- All nodes have non-empty `Id`, `Label`, `SourceFile`
- All edges reference valid node IDs
- All edges have non-empty `Relation` and `SourceFile`
- Returns `ValidationResult` (non-throwing)

**Python Mapping**: `validate.py`

## Testing Strategy

### Unit Tests (Graphify.Tests)
- Pipeline stage isolation
- Mock `IChatClient` for semantic extraction
- QuikGraph integration tests
- Validation logic tests

### Integration Tests (Graphify.Integration.Tests)
- End-to-end pipeline on sample codebases
- Export format validation
- Cache correctness
- CLI command execution

## Python to .NET Mappings

| Python Module | .NET Implementation | Notes |
|---------------|---------------------|-------|
| `detect.py` | `Pipeline/FileDetector.cs` | Uses .NET FileSystemWatcher patterns |
| `extract.py` | `Pipeline/Extractor.cs` | Hybrid AST + semantic |
| `semantic_extractor.py` | `Pipeline/SemanticExtractor.cs` | Uses IChatClient abstraction |
| `build_graph.py` | `Pipeline/GraphBuilder.cs` | Builds KnowledgeGraph |
| `cluster.py` | `Pipeline/ClusterEngine.cs` | Louvain instead of Leiden |
| `analyze.py` | `Pipeline/Analyzer.cs` | Uses QuikGraph algorithms |
| `report.py` | `Pipeline/ReportGenerator.cs` | Markdown generation |
| `export.py` | `Export/IGraphExporter` implementations | Multiple exporters |
| `validate.py` | `Validation/ExtractionValidator.cs` | Non-throwing validation |
| `cache/` | `Cache/SemanticCache.cs` | SHA256 hashing |
| NetworkX | QuikGraph | .NET graph library |
| graspologic (Leiden) | Louvain | Community detection |
| tree-sitter (Python bindings) | TreeSitter.Bindings | Multi-language AST |
| Claude API | Microsoft.Extensions.AI | Provider-agnostic |

## Dependency Overview

| Package | Purpose |
|---------|---------|
| Microsoft.Extensions.AI | LLM abstraction (IChatClient) |
| QuikGraph | Graph data structures and algorithms |
| TreeSitter.Bindings | Multi-language AST parsing |
| System.CommandLine | CLI framework |
| ModelContextProtocol | MCP stdio server |
| Microsoft.Extensions.DependencyInjection | DI container |
| Microsoft.Extensions.Configuration | Config management |
| Microsoft.Extensions.Logging | Logging abstraction |
| xUnit | Testing framework |
| coverlet.collector | Code coverage |

## Open Questions & Future Work

### Hyperedges
Python graphify has a `hyperedges` list for N-to-M relationships (e.g., all classes implementing a protocol). QuikGraph doesn't support hyperedges natively. Current approach: store as metadata or separate list.

### Graph Serialization
Should we serialize the entire QuikGraph or just nodes+edges as JSON? Current approach: JSON export serializes nodes+edges only (QuikGraph is runtime structure).

### Community Assignment Mutability
Should community assignments be stored in a separate `Dictionary<string, int>` instead of mutating nodes? Current approach mutates nodes (requires remove+add cycle) for simplicity.

### Clustering Algorithm
Python uses Leiden via graspologic. .NET uses Louvain. Leiden typically finds higher-quality communities but is newer. May revisit if Leiden becomes available in .NET.

## Performance Characteristics

- **File detection**: O(n) files, limited by filesystem
- **AST extraction**: O(n) files × O(m) AST nodes per file
- **Semantic extraction**: O(n) docs × LLM latency (parallelizable)
- **Graph building**: O(n) nodes + O(e) edges
- **Clustering**: O(n log n) for Louvain
- **Analysis**: O(n + e) for most metrics, O(n²) for betweenness
- **Export**: O(n + e) serialization

**Scaling**: Tested with graphs up to 10,000 nodes. Beyond 100k nodes, consider graph database (Neo4j) instead of in-memory QuikGraph.

## Contributing

See root README.md for contribution guidelines. Key extension points:

1. **Add a language**: Implement tree-sitter grammar support in `Extractor.cs`
2. **Add an exporter**: Implement `IGraphExporter` for new format
3. **Add a pipeline stage**: Implement `IPipelineStage<TIn, TOut>`
4. **Add validation rules**: Extend `ExtractionValidator` or create custom validator

## References

- Python source: [safishamsi/graphify](https://github.com/safishamsi/graphify)
- QuikGraph: [GitHub](https://github.com/KeRNeLith/QuikGraph)
- Microsoft.Extensions.AI: [NuGet](https://www.nuget.org/packages/Microsoft.Extensions.AI)
- TreeSitter: [tree-sitter.github.io](https://tree-sitter.github.io/tree-sitter/)
