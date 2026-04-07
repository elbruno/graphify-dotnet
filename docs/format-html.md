# HTML Interactive Viewer

> Standalone HTML file with an interactive, searchable, zoomable visualization of your knowledge graph.

## Quick Start

```bash
graphify run ./my-project --format html
# Opens graph.html in your browser
```

## What it Produces

The **HTML format** generates a single `graph.html` file containing:
- **Vis-network visualization** — Interactive force-directed graph layout
- **Node search/filter** — Type ahead search by node name or ID
- **Click inspection** — Select nodes to see metadata (type, community, connections)
- **Community coloring** — Nodes colored by detected community cluster
- **Node sizing** — Node size proportional to degree (connectivity)
- **Zoom and pan** — Full mouse and touch navigation
- **Legend** — Node type and community color reference

## How to Use

### Option 1: Open in Browser
Simply double-click `graph.html` or drag it into your browser. Works on any modern browser (Chrome, Firefox, Safari, Edge).

### Option 2: Serve Locally
For better interactivity and to avoid CORS issues with large graphs:

```bash
# Using dotnet
dotnet serve

# Using npm (if installed)
npx serve

# Using Python
python -m http.server 8000
```

Then open `http://localhost:8000/graph.html` in your browser.

## Features

- **Search nodes** — Type in the search box to highlight nodes matching name or ID
- **Filter by type** — Checkbox filters to show only certain node types
- **View details** — Click a node to see its:
  - Label and type
  - Community assignment
  - Incoming/outgoing edges
  - Connected nodes
- **Drag to move** — Reposition nodes to improve readability
- **Zoom** — Scroll wheel zooms; double-click to reset view
- **Export** — Right-click graph to save as PNG (some browsers)

## Best For

- **Visual exploration** — Understand code structure at a glance
- **Team presentations** — Share insights in meetings without complex tooling
- **Interactive analysis** — Explore connections and communities dynamically
- **Knowledge gaps** — Identify isolated nodes and weak communities
- **Non-technical stakeholders** — Easy to understand without special tools

## Example Workflows

### Find Hidden Dependencies
1. Search for a module name (e.g., "Auth")
2. Click to expand connections
3. Discover unexpectedly coupled components

### Identify God Nodes
1. Graph defaults to sorting by degree (connection count)
2. Largest nodes are your god nodes/bottlenecks
3. Right-click to drill into their dependencies

### Validate Architecture
1. Use type filters to toggle different architectures (classes, modules, APIs)
2. Verify cross-cutting concerns don't cross community boundaries
3. Spot circular dependencies visually

## Configuration

The HTML viewer respects these graph settings from `graph.json`:
- Node degree and community assignment
- Edge weight (edge thickness in visualization)
- Node type and label

## File Size

Large graphs (>1000 nodes) may take a few seconds to load and render. For graphs with 5000+ nodes, consider using the JSON export with a custom visualization tool, or the Neo4j export for interactive querying.

## See Also

- [Export Formats Overview](export-formats.md)
- [JSON Graph Export](format-json.md) — Programmatic access to the same data
- [SVG Graph Export](format-svg.md) — Static image for embedding in docs
