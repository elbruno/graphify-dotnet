# Decision: Export Format Architecture + Default Formats

**Author:** Trinity (Core Dev)  
**Date:** 2026-04-07  
**Status:** Proposed for team review

## Context

All exporters were implemented (JSON, HTML, SVG, Neo4j, Obsidian, Wiki) and ReportGenerator existed, but PipelineRunner only wired JSON and HTML. Users couldn't access the other formats from the CLI.

## Decision

### 1. Wired All Exporters into PipelineRunner

Extended the Stage 6 export switch statement to support all 7 export formats:
- `json` → graph.json (existing)
- `html` → graph.html with community labels (existing, enhanced)
- `svg` → graph.svg (NEW)
- `neo4j` → graph.cypher (NEW)
- `obsidian` → obsidian/ directory (NEW)
- `wiki` → wiki/ directory (NEW)
- `report` → GRAPH_REPORT.md (NEW)

### 2. Community Data Generation

Added helper methods to PipelineRunner for community analysis:
- `BuildCommunityLabels()`: Generates human-readable labels based on most common node type per community
- `CalculateCohesionScores()`: Computes internal edge density for each community
- Pattern follows existing Analyzer.cs and WikiExporter.cs implementations

### 3. Default Format Change

Changed default `--format` from `"json,html"` to `"json,html,report"`.

**Rationale:**
- Report provides immediate human-readable analysis summary
- No significant performance cost (analysis already runs)
- JSON remains for machine consumption
- HTML remains for interactive visualization
- Report adds actionable insights (god nodes, communities, surprising connections)

### 4. Export Path Conventions

- Single-file formats: Use filename from format (e.g., `graph.json`, `graph.svg`, `graph.cypher`)
- Directory formats: Use subdirectory (e.g., `obsidian/`, `wiki/`)
- Report: Fixed filename `GRAPH_REPORT.md` (matches Python graphify)

## Alternatives Considered

1. **Keep default as json,html only**: Rejected. Report is valuable and cheap to generate.
2. **Use .neo4j extension**: Rejected. Community convention is `.cypher` for Cypher scripts.
3. **Put community helpers in Analyzer**: Rejected. These are export-specific computations, not analysis insights.

## Impact

**Positive:**
- All 7 export formats now accessible from CLI
- Default output includes actionable analysis (report)
- Community labels enhance HTML and report readability
- Consistent export path conventions

**Negative:**
- Default output directory has one more file (GRAPH_REPORT.md)
- Minimal — users can override with `--format json,html` if they want old behavior

## Open Questions

1. Should we add a `--format all` shorthand for all 7 formats?
2. Should report format support custom templates or always use fixed structure?
3. Do we need a `--report-title` option, or is deriving from input path sufficient?

## Recommendation

Accept this as the standard export architecture. If team agrees, move this decision to `.squad/decisions.md` under "Active Decisions".
