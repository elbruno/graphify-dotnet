# Export Formats Overview

> Understand graphify's output formats and choose the right one for your use case.

## All Formats at a Glance

| Format | Output File | Best For | Interactivity | User Type |
|--------|-------------|----------|----------------|-----------|
| **HTML** | `graph.html` | Visual exploration, presentations | Interactive | Developers, teams |
| **JSON** | `graph.json` | Custom tools, CI pipelines, APIs | Programmatic | Developers, tooling |
| **SVG** | `graph.svg` | Documentation, README embeds, print | Static | Everyone |
| **Neo4j** | `graph.cypher` | Advanced queries, large datasets, analysis | Database queries | Analysts, power users |
| **Obsidian** | `obsidian/` folder | Personal knowledge management | Note linking | Knowledge workers |
| **Wiki** | `wiki/` folder | Team documentation, AI agents, onboarding | Browser navigation | Everyone |
| **Report** | `GRAPH_REPORT.md` | Quick insights, architecture review | Reading | Everyone |

## Quick Start

### Default Formats (Recommended)

```bash
graphify run ./my-project
# Generates: graph.json, graph.html, GRAPH_REPORT.md
```

### All Formats

```bash
graphify run ./my-project --format json,html,svg,neo4j,obsidian,wiki,report
# Generates all 7 exports for comprehensive analysis
```

### Specific Format

```bash
graphify run ./my-project --format html
graphify run ./my-project --format json
```

## Format Comparison

### For Visual Exploration

**Want to click around and explore?** → Use **HTML**

- Interactive browser-based viewer
- Search nodes, zoom, pan, click for details
- No external tools needed
- Perfect for discovering architecture

### For Documentation

**Adding to README or docs?** → Use **SVG** or **Wiki**

- **SVG:** Static image, embed with `![Graph](graph.svg)`
- **Wiki:** Full-featured documentation site, serve on GitHub Pages
- **Report:** Summary document, git-friendly

### For Custom Analysis

**Building your own tools?** → Use **JSON**

- Machine-readable format
- Load into Python, JavaScript, or any language
- Pipe to jq, grep, or custom scripts
- Integrate with your CI/CD pipeline

### For Advanced Queries

**Need complex graph operations?** → Use **Neo4j**

- Cypher query language
- Pattern matching, shortest paths, cycles
- Combine with other graph data
- Real-time dashboards

### For Personal Knowledge Base

**Managing knowledge with notes?** → Use **Obsidian**

- Individual markdown notes per node
- Wikilinks between components
- Leverage Obsidian plugins and graph view
- Mix code knowledge with other notes

### For Team Communication

**Sharing with non-technical stakeholders?** → Use **Report** or **Wiki**

- **Report:** Human-readable summary, insights, recommendations
- **Wiki:** Navigable documentation, ideal for onboarding

## Common Workflows

### "I Want to Understand My Codebase Fast"

```bash
graphify run ./src --format html,report
# Step 1: Read GRAPH_REPORT.md (5-10 min)
# Step 2: Open graph.html in browser (explore visually)
```

### "I Need to Present Architecture to the Team"

```bash
graphify run ./src --format html,svg,report
# Step 1: Use graph.html for live exploration in meeting
# Step 2: Include graph.svg in slides/docs
# Step 3: Share GRAPH_REPORT.md as handout
```

### "I'm Building a Custom Analysis Tool"

```bash
graphify run ./src --format json
# Load graph.json in your language/framework
# Build visualizations, metrics, or analysis on top
```

### "I'm Setting Up a Knowledge Base"

```bash
graphify run ./src --format obsidian,wiki
# Choose one or both depending on your workflow
# Obsidian: personal knowledge management
# Wiki: team documentation site
```

### "I Need Advanced Graph Queries"

```bash
graphify run ./src --format neo4j
# Import into Neo4j
# Write Cypher queries for patterns, cycles, paths
# Run complex analysis
```

### "I Want Everything"

```bash
graphify run ./src --format json,html,svg,neo4j,obsidian,wiki,report
# Get all perspectives on your codebase
# Use different formats for different purposes
# No performance penalty — all generated in one run
```

## Format Details

### 1. HTML Interactive Viewer

**Produces:** `graph.html`

A single, self-contained HTML file with interactive visualization using vis-network.

- ✅ Click nodes for details
- ✅ Search and filter
- ✅ Zoom, pan, drag
- ✅ No external dependencies
- ✅ Works offline

[Learn more →](format-html.md)

### 2. JSON Graph Export

**Produces:** `graph.json`

Standard JSON with nodes, edges, communities, and metadata.

- ✅ Load into any programming language
- ✅ Pipe to jq or other CLI tools
- ✅ Build custom visualizations
- ✅ Integrate with CI/CD

[Learn more →](format-json.md)

### 3. SVG Graph Export

**Produces:** `graph.svg`

Static vector image of the graph.

- ✅ Embed in docs with `![](graph.svg)`
- ✅ Include in README, PRs, slides
- ✅ Print to PDF
- ✅ Edit in Inkscape or Illustrator

[Learn more →](format-svg.md)

### 4. Neo4j Cypher Export

**Produces:** `graph.cypher`

Cypher script for importing into Neo4j database.

- ✅ Advanced graph queries
- ✅ Pattern matching and path finding
- ✅ Cycle detection
- ✅ Combine with other data

[Learn more →](format-neo4j.md)

### 5. Obsidian Vault Export

**Produces:** `obsidian/` folder

Personal knowledge management vault with interconnected notes.

- ✅ One markdown note per node
- ✅ Wikilinks between nodes
- ✅ Leverage Obsidian plugins
- ✅ Mix with other knowledge

[Learn more →](format-obsidian.md)

### 6. Wiki Export

**Produces:** `wiki/` folder

Documentation site structure, agent-crawlable.

- ✅ Serve as documentation site
- ✅ AI agents can navigate
- ✅ Community pages and god node analysis
- ✅ Search-friendly flat structure

[Learn more →](format-wiki.md)

### 7. Graph Analysis Report

**Produces:** `GRAPH_REPORT.md`

Human-readable analysis with insights and recommendations.

- ✅ Summary metrics
- ✅ God nodes and surprising connections
- ✅ Community analysis
- ✅ Suggested questions for architects
- ✅ **Included by default** in every run

[Learn more →](format-report.md)

## Choosing Multiple Formats

There's no performance penalty for generating multiple formats in one run:

```bash
# Fast and efficient — all generated in ~2 seconds
graphify run ./src --format json,html,svg,neo4j,obsidian,wiki,report
```

**Recommended combinations:**

| Goal | Formats |
|------|---------|
| Quick start | `html, report` |
| Documentation | `html, svg, report` |
| Knowledge base | `obsidian, wiki, report` |
| Analysis | `json, neo4j` |
| Everything | `json, html, svg, neo4j, obsidian, wiki, report` |

## Format Sizes

Approximate file/folder sizes for 1000-node graphs:

| Format | Size |
|--------|------|
| JSON | ~500 KB |
| HTML | ~1 MB (self-contained) |
| SVG | ~1 MB |
| Neo4j Cypher | ~200 KB |
| Obsidian | ~2 MB (many files) |
| Wiki | ~3 MB (many files) |
| Report | ~100 KB |

## Integration with Git

All formats are git-friendly:

```bash
# Commit reports and SVG for version control
git add GRAPH_REPORT.md graph.svg

# Store JSON for machine consumption
git add graph.json

# Store folders for team collaboration
git add obsidian/ wiki/

# Ignore HTML (it's static, can be regenerated)
echo "graph.html" >> .gitignore
```

## Regenerating Formats

Update formats whenever code changes significantly:

```bash
# Quick update (default + report)
graphify run ./src

# Full refresh (all formats)
graphify run ./src --format json,html,svg,neo4j,obsidian,wiki,report

# In CI/CD: commit updated exports
graphify run ./src
git add graph.* GRAPH_REPORT.md obsidian/ wiki/
git commit -m "chore: update architecture exports"
git push
```

## Format Limitations

| Format | Limitation | Workaround |
|--------|-----------|-----------|
| HTML | Large graphs (10K+ nodes) slow to render | Use JSON or Neo4j for analysis |
| SVG | Static (no interactivity) | Use HTML for exploration |
| JSON | Requires programming knowledge | Use HTML or Report for browsing |
| Neo4j | Requires Neo4j setup | Use HTML for quick exploration |
| Obsidian | Requires Obsidian app | Use Wiki for browser access |
| Wiki | Manual navigation (no DB queries) | Use Neo4j for complex analysis |
| Report | Text only (no interaction) | Use HTML for exploration |

## See Also

- [HTML Interactive Viewer](format-html.md)
- [JSON Graph Export](format-json.md)
- [SVG Graph Export](format-svg.md)
- [Neo4j Cypher Export](format-neo4j.md)
- [Obsidian Vault Export](format-obsidian.md)
- [Wiki Export](format-wiki.md)
- [Graph Analysis Report](format-report.md)
