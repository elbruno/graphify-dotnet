# Obsidian Vault Export

> Personal knowledge management vault with your graph as interconnected markdown notes.

## Quick Start

```bash
graphify run ./my-project --format obsidian
# Generates obsidian/ folder with one note per node
```

## What it Produces

The **Obsidian format** generates an `obsidian/` directory containing:
- **Individual notes** — One `.md` file per node with metadata and links
- **Index** — `_Index.md` with navigable overview and community breakdown
- **Wikilinks** — Bidirectional connections using `[[node-name]]` syntax
- **Frontmatter** — YAML metadata (type, community, degree, references)
- **Tags** — Community tags for filtering and navigation
- **Backlinks** — Automatic inbound reference tracking

## Directory Structure

```
obsidian/
  _Index.md                 # Overview and community index
  _Community-Auth.md        # Summary of Auth community
  _Community-API.md         # Summary of API community
  AuthService.md            # Individual node: AuthService
  JwtTokenizer.md           # Individual node: JwtTokenizer
  UserController.md         # Individual node: UserController
  ...
```

## Note Format

Each node becomes a markdown file with:

```markdown
---
id: AuthService
type: Class
community: 2
degree: 8
---

# AuthService

Handles user authentication and JWT token management.

## Type
Class

## Community
Auth (2)

## Connections

### Outgoing (8 calls)
- [[JwtTokenizer]]
- [[CredentialValidator]]
- [[SessionManager]]
- [[PasswordHasher]]

### Incoming (3 calls)
- [[UserController]]
- [[LoginHandler]]

## Degree
8 connections total

## Tags
#Auth #Security #Service
```

## How to Use

### Option 1: Open as Vault

1. In Obsidian, click "Open vault"
2. Select the `obsidian/` folder
3. Explore! Click links to navigate between notes

### Option 2: Link to Existing Vault

Copy `obsidian/` contents into your existing Obsidian vault:

```bash
cp -r obsidian/* ~/ObsidianVault/projects/graphify-dotnet/
```

Then access from your main vault.

### Option 3: Embed Subgraphs

Export specific communities or subsystems:

```bash
# Export and import just Auth community
graphify run ./src --format obsidian --filter "community:Auth"
```

## Features

- **Graph view** — Obsidian's built-in graph view shows your architecture visually
- **Wikilinks** — Click any `[[node-name]]` to jump to that note
- **Backlinks panel** — See all notes linking to current note
- **Search** — Full-text search across all notes
- **Tags** — Filter by community tags (`#Auth`, `#API`)
- **Daily notes** — Add findings/refactoring decisions with timestamps
- **Queries** — Use Dataview plugin to extract metrics

## Obsidian Plugins

Enhance your graph exploration with plugins:

### Recommended Plugins

1. **Graph Analysis** — Visualize degree distribution, community sizes
2. **Dataview** — Query notes as a database:
   ```
   TABLE type, community, degree
   FROM "obsidian"
   WHERE type = "Class"
   SORT BY degree DESC
   ```

3. **Local Graph** — Focus on neighborhood around current note
4. **Excalibrain** — Alternative graph visualization
5. **Query Language** — Advanced filtering and navigation

## Best For

- **Personal knowledge base** — Knowledge management beyond code
- **Obsidian power users** — Leverage plugins and backlinks
- **Long-form exploration** — Add research, decisions, refactoring plans per node
- **Team wikis** — Share vault over git, comment together
- **Learning** — Understand codebase by reading and annotating
- **Connecting ideas** — Link code concepts to external knowledge

## Example Workflows

### Build a Personal Codebook

1. Export to Obsidian vault
2. For each critical module, add a note with design decisions
3. Link to related architecture patterns
4. Add images and diagrams
5. Your personal knowledge base for future reference

### Collaborate on Architecture

1. Share Obsidian vault via git
2. Team members clone and open vault
3. Add comments via Obsidian Comments plugin
4. Discuss refactoring directly in notes
5. Commit changes together

### Map Learning Path

1. Start at highest-degree nodes (god nodes)
2. Add tag `#understand-first` to prioritize
3. Add notes about each component's role
4. Build dependency chain for learning
5. Export as guide for new team members

### Refactoring Decisions

Add metadata per node:

```markdown
---
id: AuthService
type: Class
community: 2
refactoring_status: planned
refactoring_notes: Split into separate services for JWT vs OAuth
refactoring_priority: high
---

# AuthService
...
```

Then query:

```
TABLE id, refactoring_status, refactoring_notes
FROM "obsidian"
WHERE refactoring_status = "planned"
SORT BY refactoring_priority DESC
```

## File Size

Obsidian vaults scale well:
- 100 nodes → ~200 KB total
- 1000 nodes → ~2 MB total
- 5000 nodes → ~10 MB total

Git-friendly: obsidian folder compresses to <20% with git's delta encoding.

## Integration with Git

Store vault in git repo:

```bash
git add obsidian/
git commit -m "docs: update knowledge graph"
git push
```

Team members pull changes and open in Obsidian. Use `.obsidian/` folder for local settings (add to `.gitignore`).

## Sync with Source

Periodically regenerate to stay current with code:

```bash
graphify run ./src --format obsidian
git diff obsidian/  # Review changes
git add obsidian/
git commit -m "chore: sync knowledge graph with latest code"
```

## See Also

- [Export Formats Overview](export-formats.md)
- [HTML Interactive Viewer](format-html.md) — Browser-based exploration
- [Wiki Export](format-wiki.md) — Agent-crawlable documentation
