# Wiki Export

> Agent-crawlable documentation site structure designed for LLM navigation and knowledge base integration.

## Quick Start

```bash
graphify run ./my-project --format wiki
# Generates wiki/ folder with index and searchable pages
```

## What it Produces

The **Wiki format** generates a `wiki/` directory containing:
- **index.md** — Entry point with overview, navigation, and search index
- **Community pages** — One page per detected community (e.g., `Auth.md`, `API.md`)
- **God node articles** — Deep dives on high-degree nodes
- **Relationship pages** — Connections and dependencies between components
- **Audit trail** — Metadata on extraction confidence and methods
- **Optimized structure** — Flat hierarchy friendly to agents and crawlers

## Directory Structure

```
wiki/
  index.md                  # Overview, search index, navigation
  Auth.md                   # Community: Authentication cluster
  API.md                    # Community: REST API cluster
  Database.md               # Community: Data persistence
  _godNodes.md              # High-degree nodes requiring refactoring
  _circularDeps.md          # Detected cycles and circular references
  _suggestions.md           # Recommended refactoring priorities
```

## File Contents

### index.md (Entry Point)

```markdown
# Knowledge Graph: My Project

**Generated:** 2025-04-06  
**Nodes:** 42 | **Edges:** 156 | **Communities:** 5

## Overview

This is a structured representation of your codebase's architecture and dependencies.

## Quick Navigation

- **Communities** → [Auth](#auth) | [API](#api) | [Database](#database)
- **God Nodes** → [[AuthService]] (degree 8) | [[UserController]] (degree 7)
- **Search** → See search index below

## Communities

### Auth
- Description: Authentication and authorization
- Nodes: 3 | Edges: 8
- [View Community](Auth.md)

### API
- Description: REST endpoints and controllers
- Nodes: 5 | Edges: 12
- [View Community](API.md)

...

## God Nodes (High Degree)
1. AuthService — 8 connections
2. UserController — 7 connections
3. DatabaseManager — 6 connections

See [[_godNodes.md]] for detailed analysis.
```

### Community Page (e.g., Auth.md)

```markdown
# Auth Community

A cluster of 3 nodes focused on authentication and security.

## Nodes in This Community

| Node | Type | Degree | Role |
|------|------|--------|------|
| AuthService | Class | 8 | Main auth handler |
| JwtTokenizer | Class | 4 | Token generation |
| CredentialValidator | Function | 2 | Credential checking |

## Internal Connections

AuthService → JwtTokenizer → CredentialValidator

## External Dependencies

- → UserController (3 calls)
- → SessionManager (1 call)

## Insights

- Tight cohesion: 80% of edges are within-community
- No circular dependencies
- AuthService is the god node; consider splitting

[Back to Index](index.md)
```

### God Nodes Page

```markdown
# God Nodes Analysis

High-degree nodes that may need refactoring.

## Nodes

1. **AuthService** (degree: 8)
   - Called by: UserController, LoginHandler, ...
   - Calls: JwtTokenizer, PasswordHasher, ...
   - Community: Auth
   - Status: ⚠️ High coupling, refactor candidate

2. **DatabaseManager** (degree: 6)
   - Tightly coupled to multiple services
   - Consider dependency injection

[Back to Index](index.md)
```

## How to Use

### Option 1: Serve as Documentation Site

```bash
# Using any static site server
npx serve wiki/

# Or use docsify for better formatting
npm install -g docsify-cli
docsify serve wiki/
```

Then open `http://localhost:3000` in your browser.

### Option 2: Host on GitHub Pages

1. Push wiki folder to `docs/` in your repo
2. Enable GitHub Pages in repository settings
3. Set source to `docs/` branch
4. Wiki is live at `https://yourname.github.io/project/`

### Option 3: Feed to AI Agents

Point Claude, ChatGPT, or Copilot at `wiki/index.md`:

```
I'm working on refactoring my codebase. Here's the architecture graph:
[attach wiki/index.md and relevant community pages]

What are your suggestions for reducing coupling?
```

Agents can navigate wikilinks and understand structure without needing a database.

### Option 4: Import into Wiki Platforms

Copy to Confluence, GitBook, ReadTheDocs, or Notion:

```bash
# Export to CommonMark (works on most platforms)
pandoc wiki/*.md --to markdown_github > combined.md
# Then paste into your wiki platform
```

## Features

- **Flat structure** — No nested folders; agents prefer flat hierarchies
- **Search index** — `index.md` includes searchable node list
- **Breadcrumbs** — Links back to index for navigation
- **Metadata** — Confidence scores, extraction methods, audit trail
- **Relationship focus** — Emphasizes connections, not just definitions
- **Suggested actions** — Refactoring priorities and hotspots highlighted

## Best For

- **Team documentation** — Living docs that stay in sync with code
- **AI agent context** — Agents can crawl and understand structure
- **Onboarding** — New team members read communities, understand layers
- **Audit trail** — See how each node was detected (AST, semantic, user input)
- **Knowledge base** — Foundation for knowledge management systems
- **Presentations** — Community pages can be slides or handouts

## Example Workflows

### Onboard New Team Members

1. Deploy wiki to GitHub Pages
2. Send link to new hires
3. They explore communities, read god node analyses
4. Understand architecture faster than reading code directly

### Code Review Context

When reviewing a PR:

1. Check wiki for affected community
2. Understand community's role and god nodes
3. Better context for review decisions
4. Link to relevant wiki pages in PR comments

### Refactoring Guidance

1. Wiki shows god nodes needing refactoring
2. Extract god nodes' responsibilities
3. Create new communities for split services
4. Regenerate wiki to validate results

### AI-Assisted Refactoring

1. Export wiki
2. Feed to Claude with prompt: "Propose refactoring to reduce god node coupling"
3. Claude understands structure from wiki layout
4. Gets actionable recommendations based on connections

## Customization

Wiki generation respects:
- **Community detection** — Automatic clustering algorithms or manual community assignment
- **Min confidence** — Only include high-confidence edges (configurable threshold)
- **Filtering** — Include/exclude node types (e.g., skip internal implementation details)

## Performance

Wiki folders are lightweight:
- 100 nodes → ~300 KB
- 1000 nodes → ~3 MB
- 5000 nodes → ~30 MB

Markdown is highly compressible; git diffs show only changed sections.

## Update Frequency

Regenerate wiki after:
- Major refactoring or restructuring
- Adding new modules/services
- Quarterly architecture reviews
- Before significant architectural decisions

```bash
# Add to git workflow
graphify run ./src --format wiki
git add wiki/
git commit -m "docs: sync architecture wiki with codebase"
```

## See Also

- [Export Formats Overview](export-formats.md)
- [HTML Interactive Viewer](format-html.md) — Interactive exploration
- [Obsidian Vault Export](format-obsidian.md) — Personal knowledge management
- [Graph Analysis Report](format-report.md) — Summary insights
