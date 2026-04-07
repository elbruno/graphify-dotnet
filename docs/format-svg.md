# SVG Graph Export

> Static vector image of your knowledge graph, perfect for documentation and presentations.

## Quick Start

```bash
graphify run ./my-project --format svg
# Generates graph.svg with static visualization
```

## What it Produces

The **SVG format** generates `graph.svg` containing:
- **Force-directed layout** — Spatially organized graph with physics-based positioning
- **Community colors** — Nodes colored by cluster
- **Node labels** — Text labels for easy reading
- **Edge lines** — Connections between nodes with relationship type indicators
- **Legend** — Reference for node types and community colors
- **Vector format** — Infinitely scalable, no quality loss when resized

## Features

- **Community coloring** — Each community has a distinct color for easy visual separation
- **Node sizing** — Node size proportional to degree (more connections = larger)
- **Edge weights** — Thicker edges represent stronger relationships (more calls, closer coupling)
- **Relationship labels** — Edge labels indicate type (calls, extends, imports, semantic, etc.)
- **Legend panel** — Color key and type reference in corner

## How to Use

### Embed in Documentation

```markdown
![Graph](graph.svg)

## Architecture Overview

This is our system graph showing modules (blue), services (green), and utilities (yellow).
```

### Embed in GitHub README

```markdown
<img src="docs/graph.svg" alt="Project Graph" width="800">
```

### Include in Pull Requests

Attach `graph.svg` to code review PRs to show how your changes affect the dependency graph:

```bash
graphify run ./src --format svg
git add graph.svg
git commit -m "docs: add dependency graph"
git push -u origin feature-branch
```

### Print for Physical Analysis

SVG exports perfectly to PDF and paper. Zoom before printing:

```bash
# Print to PDF in Firefox
File > Print > Save as PDF
# Or use a tool like wkhtmltopdf
wkhtmltopdf graph.svg graph.pdf
```

### Edit in Design Tools

Open SVG in Inkscape, Adobe Illustrator, or Figma for further customization:
- Adjust colors or layout
- Add annotations
- Combine with other diagrams
- Create presentations

### Convert to Other Formats

```bash
# Convert to PNG (requires ImageMagick)
convert graph.svg graph.png

# Convert to PDF (requires Inkscape or similar)
inkscape graph.svg --export-pdf=graph.pdf

# Convert to high-res PNG
convert -density 300 graph.svg graph.png
```

## Best For

- **Documentation** — Architecture diagrams in README files
- **Presentations** — Clean, professional visuals for talks and demos
- **Design reviews** — Share dependency structure with architects
- **Offline viewing** — No internet or software required
- **Publications** — Embed in papers, blogs, or reports
- **Historical records** — Git-friendly format for version control

## Example Workflows

### Document Your Architecture

1. Run `graphify run ./src --format svg`
2. Add to your README or docs folder
3. Add caption: "Detected architectural clusters in project"
4. Commit and push
5. Team members see dependency structure immediately when viewing repo

### Monitor Evolution Over Time

1. Generate SVG after each major milestone
2. Store versions in git (SVG diffs show structural changes)
3. Create a "graph evolution" gallery in your wiki
4. Track how architecture changes with refactoring

### Communicate with Non-Developers

1. Export SVG of key subsystem
2. Add in presentation/whitepaper
3. Use color coding and sizing to tell the story without technical details

## Customization

The SVG generator respects these settings:
- **Layout algorithm** — Force-directed (physics-based positioning)
- **Colors** — Community assignment determines node colors
- **Size** — Responsive to canvas dimensions
- **Scale** — Automatically fits content to viewBox

## File Size

SVG files are typically 50 KB – 5 MB depending on node count:
- 100 nodes → ~50 KB
- 1000 nodes → ~500 KB
- 5000 nodes → ~5 MB

For very large graphs, consider exporting only a subset or using the HTML viewer for interactive exploration.

## Limitations

- **Static** — No interactivity (use HTML viewer instead)
- **Large graphs** — Rendering can be slow for 5000+ nodes in some viewers
- **Real-time updates** — Not suitable for live dashboards (regenerate on each run)

## See Also

- [Export Formats Overview](export-formats.md)
- [HTML Interactive Viewer](format-html.md) — Interactive version of the same visualization
- [JSON Graph Export](format-json.md) — Raw data for custom visualizations
