# Decision: Folder Config as Top-Level Settings

**Author:** Trinity (Core Developer)
**Date:** 2026-04-08
**Status:** Implemented

## Context

The interactive config menu only supported AI provider setup. Users had no way to persist default project folder, output directory, or export format preferences — they had to pass CLI flags every run.

## Decision

Added `WorkingFolder`, `OutputFolder`, and `ExportFormats` as **top-level** properties on `GraphifyConfig` (not nested under a provider). These are persisted independently of provider selection in `appsettings.local.json`.

The `run` command applies saved defaults only when CLI arguments are at their default values (`.`, `graphify-out`, `json,html,report`), so explicit CLI flags always take priority.

## Alternatives Considered

1. **Nested under a "Project" config section**: Would require a new config class and complicate binding. Top-level is simpler since there are only 3 properties.
2. **Separate config file**: Rejected — keeping everything in `appsettings.local.json` is consistent with the existing config strategy.

## Impact

- Config menu now has 3 options instead of 2
- `graphify config folder` is a new subcommand
- `graphify run` respects saved folder/output/format defaults
- `graphify config show` displays project settings
- No breaking changes to existing CLI behavior
