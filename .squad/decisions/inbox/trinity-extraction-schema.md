# Decision: Extraction Schema Design

**Author:** Trinity (Core Developer)  
**Date:** 2026-04-06  
**Status:** Implemented

## Context

The extraction pipeline needs a schema for representing extracted nodes and edges from source code. The Python version uses dictionaries with specific required fields validated by validate.py.

## Decision

Created two distinct model layers:

### 1. Extraction Models (Raw Output)
- **ExtractedNode**: Contains `Id`, `Label`, `FileType`, `SourceFile`, optional `SourceLocation` and `Metadata`
- **ExtractedEdge**: Contains `Source`, `Target` (as string IDs), `Relation`, `Confidence`, `SourceFile`, `SourceLocation`, `Weight`
- **ExtractionResult**: Aggregates lists of ExtractedNode and ExtractedEdge, plus metadata (raw text, source file path, extraction method, timestamp, confidence scores)

These use string IDs for node references because they represent raw extraction output before graph assembly.

### 2. Graph Models (QuikGraph Integration)
- **GraphNode**: Simple node with `Id` and `Type`
- **GraphEdge**: Implements `IEdge<GraphNode>` with actual object references to source/target nodes
- Used by `KnowledgeGraph` which wraps QuikGraph's `BidirectionalGraph`

### Supporting Types
- **Confidence** enum: Extracted, Inferred, Ambiguous (matching Python)
- **FileType** enum: Code, Document, Paper, Image (matching Python)
- **ExtractionMethod** enum: Ast, Semantic, Hybrid
- **ValidationResult** record: Non-throwing validation with success flag and error list

### Validation
- **ExtractionValidator**: Ports Python's validate.py logic
  - Validates all nodes have non-empty Id, Label, SourceFile
  - Validates all edges have valid Source/Target IDs that match existing nodes
  - Validates all edges have non-empty Relation and SourceFile
  - Returns ValidationResult instead of throwing exceptions

## Alternatives Considered

1. **Single unified model**: Would require mixing string IDs with object references, making it unclear when nodes are resolved
2. **Throwing validator**: Rejected in favor of returning ValidationResult for better error handling

## Impact

- Clear separation between extraction (string IDs) and graph assembly (object references)
- Both AST and semantic extractors can output ExtractionResult
- Validation logic matches Python implementation ensuring schema compatibility
- Non-throwing validation enables better error reporting and batch validation
