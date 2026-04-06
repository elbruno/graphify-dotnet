# Squad Decisions

## Active Decisions

### Decision: Copilot Instructions Convention File

**Author:** Neo (Lead/Architect)  
**Date:** 2026-04-06  
**Status:** Implemented

#### Context

The project needed a `.github/copilot-instructions.md` to codify repo conventions for Copilot and the squad team. Used ElBruno's convention style (from ElBruno.MarkItDotNet) as the starting template.

#### Decision

Created `.github/copilot-instructions.md` with the following key architectural choices:

1. **Not a NuGet package** — all NuGet publishing sections, trusted publishing, pack workflows, nuget_logo.png references, and multi-target framework guidance removed entirely.
2. **Single target: `net10.0`** — no multi-targeting. This simplifies CI and avoids preview SDK issues.
3. **Project structure under `src/`**: Graphify (core), Graphify.Cli, Graphify.Sdk, Graphify.Mcp, with tests under `src/tests/`.
4. **Solution format: `.slnx`** (XML-based).
5. **CI only** — `build.yml` with restore/build/test. No publish workflow.
6. **Pipeline architecture** documented: detect → extract → build → cluster → analyze → report → export.
7. **Key dependencies** listed as consumed packages (not published): Microsoft.Extensions.AI, GitHub.Copilot.SDK, TreeSitter.DotNet, QuikGraph, System.CommandLine, ModelContextProtocol, xUnit.

#### Impact

All team members and Copilot agents now have a single source of truth for project conventions, eliminating ambiguity about structure, naming, and CI setup.

## Governance

- All meaningful changes require team consensus
- Document architectural decisions here
- Keep history focused on work, decisions focused on direction
