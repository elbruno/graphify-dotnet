# Graph Analysis Report

> Human-readable markdown summary of your knowledge graph with insights and recommendations.

## Quick Start

```bash
graphify run ./my-project --format report
# Generates GRAPH_REPORT.md with analysis summary
```

## What it Produces

The **Report format** generates `GRAPH_REPORT.md` containing:
- **Summary** — Node/edge counts, community breakdown, average metrics
- **God Nodes** — High-degree nodes requiring attention
- **Surprising Connections** — Unexpected dependencies between components
- **Communities** — Detailed analysis of detected clusters
- **Suggested Questions** — Prompts for architecture discussion
- **Knowledge Gaps** — Areas needing clarification
- **Generated Metadata** — Timestamp, extraction methods, confidence scores

## Report Contents

```markdown
# Knowledge Graph Analysis Report

**Generated:** 2025-04-06  
**Source:** ./my-project  
**Extraction Methods:** AST, Semantic Extraction, Copilot

---

## Summary

| Metric | Value |
|--------|-------|
| Total Nodes | 42 |
| Total Edges | 156 |
| Communities | 5 |
| Average Degree | 3.7 |
| Highest Degree | 8 (AuthService) |
| Density | 0.09 |

### Nodes by Type

- Class: 20
- Function: 12
- Module: 6
- Concept: 4

### Extraction Summary

- AST-detected: 28 (66%)
- Semantic-detected: 10 (24%)
- Inferred: 4 (10%)
- Average confidence: 0.87

---

## God Nodes (High Degree)

Nodes with many connections that may need refactoring.

### 1. AuthService (Degree: 8)
- **Type:** Class
- **Community:** Auth
- **Calls:** JwtTokenizer, PasswordHasher, SessionManager, CredentialValidator (3 more)
- **Called By:** UserController, LoginHandler, AdminPanel
- **Risk:** High coupling; single point of failure for authentication
- **Recommendation:** Consider splitting into separate services (JWT handler, credential validator)

### 2. DatabaseManager (Degree: 6)
- **Type:** Class
- **Community:** Database
- **Description:** Central hub for all database operations
- **Risk:** Moderate coupling; architecture bottleneck
- **Recommendation:** Introduce repository pattern per domain

---

## Surprising Connections

Unexpected dependencies between seemingly unrelated components.

### Cross-Community Edges

1. **API → Utils** (4 edges)
   - Tight coupling to utility functions
   - Consider decoupling or moving utilities to shared layer

2. **Auth → Database** (2 edges)
   - Auth should use high-level repository interface
   - Currently tightly coupled to implementation

---

## Communities

### Auth Community (3 nodes)
- **Nodes:** AuthService, JwtTokenizer, CredentialValidator
- **Cohesion:** 80% (edges within community)
- **Size:** 3 nodes, 8 edges
- **Role:** User authentication and credential handling
- **Stability:** Stable; no circular dependencies

### API Community (5 nodes)
- **Nodes:** UserController, ProductController, OrderController, AuthMiddleware, ErrorHandler
- **Cohesion:** 75% (edges within community)
- **External Dependencies:** 5 edges to Auth, 3 to Database
- **Role:** REST endpoint definitions
- **Status:** Well-defined, appropriate dependencies

...

---

## Suggested Questions for Architects

1. **AuthService god node:** Should we split authentication into separate JWT and OAuth services?
2. **Cross-community edges:** Why does API layer call Database directly instead of through repository?
3. **Isolated nodes:** Are "Utils" and "Constants" truly needed, or can they be inlined?
4. **Community size:** Do our communities align with team boundaries?
5. **Circular dependencies:** Found 0 cycles — good! But should we enforce this in CI?

---

## Knowledge Gaps

Areas where the graph lacks clarity or needs investigation.

- **Missing:** How does the caching layer fit in? Not detected in code.
- **Unclear:** Configuration module — appears in 3 communities. Should be consolidated?
- **Underspecified:** External API calls — marked as "external" but destination unclear.
- **Needs investigation:** "LegacyAuth" module — 0 recent changes but still coupled to main Auth.

---

## Metrics & Health Indicators

| Indicator | Value | Status |
|-----------|-------|--------|
| Circular Dependencies | 0 | ✅ Good |
| God Nodes | 2 | ⚠️ Moderate |
| Isolated Nodes | 1 | ⚠️ Review needed |
| Community Balance | 0.8 | ✅ Good |
| Extraction Confidence | 0.87 | ✅ High |

---

## Extraction Metadata

- **AST Parser:** Tree-sitter (C#, TypeScript, Python, Go)
- **Semantic Extractor:** Microsoft.Extensions.AI (LLM-based)
- **Community Detection:** Leiden algorithm
- **Confidence Threshold:** >= 0.6 (reporting higher confidence relationships)
- **Extraction Time:** 2.3 seconds

```

## How to Use

### Option 1: Read Directly

Open `GRAPH_REPORT.md` in any text editor or GitHub. Great for quick understanding of your codebase structure.

### Option 2: Share with Team

Post report in:
- **Slack:** Paste or attach GRAPH_REPORT.md to channel
- **Pull Requests:** Reference report in PR description
- **Email:** Send to stakeholders for architecture reviews
- **Wiki:** Add to team documentation

### Option 3: Use in Discussions

In architecture meetings, use report to:
- Identify god nodes for refactoring discussion
- Explore surprising connections
- Validate team structure against detected communities
- Discuss architectural debt

### Option 4: Feed to AI Agents

Point Copilot or Claude at the report:

```
Here's my code's architecture report:
[paste GRAPH_REPORT.md]

What are the biggest risks? What should I refactor first?
```

Agents understand the report format and give actionable recommendations.

### Option 5: Version Control

Commit report to track architecture changes:

```bash
graphify run ./src --format report
git add GRAPH_REPORT.md
git commit -m "chore: update architecture report"
git push
```

Over time, diffs show architectural evolution.

## Sections Explained

**Summary** — High-level metrics: node count, community count, density

**God Nodes** — Nodes with too many connections; candidates for refactoring

**Surprising Connections** — Dependencies that seem unusual or violate expected architecture

**Communities** — Clusters of related nodes with analysis of each

**Suggested Questions** — Talking points for architects and team leads

**Knowledge Gaps** — Areas where the extraction needs clarification

**Metrics** — Quantitative health indicators for architecture

## Best For

- **Quick onboarding** — New team members understand architecture in minutes
- **Architecture reviews** — Data-driven discussion points
- **Refactoring decisions** — Evidence of god nodes and hot spots
- **CI/CD integration** — Track architecture metrics over time
- **AI agent context** — Agents understand structure from natural language summary
- **Documentation** — Living document that updates with code

## Example Workflows

### Validate Architecture Against Design

1. Generate report
2. Compare god nodes to expected critical components
3. Investigate surprising connections
4. Discuss with team if they match architectural intent

### Justify Refactoring Budget

1. Show god nodes and circular dependencies to product/management
2. Quantify risk: "AuthService has degree 8; if it breaks, 8 other components fail"
3. Request time for refactoring
4. Re-generate report after refactoring to show improvement

### Monitor Codebase Health

1. Generate report each milestone
2. Track god node count: increasing = growing complexity
3. Track circular dependencies: should stay at 0
4. Track community balance: uneven = potential team bottleneck

### Onboard New Developers

1. Run graphify on codebase
2. Print report or add to wiki
3. New hires read report first, understand high-level structure
4. Then read code with better mental model

## Integration with Other Formats

The report is included **by default** in every graphify run:

```bash
# Report is generated by default
graphify run ./src

# Explicitly include it with other formats
graphify run ./src --format json,html,report

# Or generate just the report
graphify run ./src --format report
```

All other formats (JSON, HTML, SVG, Neo4j, Obsidian, Wiki) contain the same underlying data; report is the human-readable summary.

## Customization

Report sections respect:
- **Confidence threshold** — Only include edges above threshold
- **Community detection algorithm** — Leiden, Louvain, or manual assignment
- **Node type filters** — Include/exclude certain types
- **Report style** — Verbose (detailed) or concise

## See Also

- [Export Formats Overview](export-formats.md)
- [JSON Graph Export](format-json.md) — Raw data
- [Wiki Export](format-wiki.md) — Detailed community pages
- [HTML Interactive Viewer](format-html.md) — Visual exploration
