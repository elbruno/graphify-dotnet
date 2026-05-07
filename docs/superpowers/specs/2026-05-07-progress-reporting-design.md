# Progress Reporting for Pipeline Loops

**Date:** 2026-05-07  
**Status:** Approved  
**Scope:** `src/Graphify.Cli/PipelineRunner.cs`

## Problem

When running `graphify run . --provider ollama`, the CLI hangs silently at:

```
[2b/6] Running AI-enhanced semantic extraction...
```

No feedback is shown while iterating 2800+ files with per-file LLM calls. User cannot distinguish a hang from slow progress.

Same issue exists at Stage 2 (AST extraction, less critical — completes quickly but still silent).

## Solution

Add `\r`-overwrite inline counter to both loops in `PipelineRunner.cs`. Display format: `[current/total]`.

**Terminal behavior during Stage 2b:**
```
[2b/6] Running AI-enhanced semantic extraction...
      AI extracting... [45/2827]    ← overwrites in-place via \r
```

**After loop completes:**
```
[2b/6] Running AI-enhanced semantic extraction...
      AI extracting... [2827/2827]
      AI extracted from 312 files
      Total: 51044 nodes, 67211 edges (AST + AI)
```

## Design

### New method

```csharp
// In PipelineRunner — sync Write (not async) to avoid interleaving
private void WriteProgress(string label, int current, int total)
{
    _output.Write($"\r      {label} [{current}/{total}]");
}
```

### Stage 2 — AST loop (lines ~69–94)

```csharp
await WriteLineAsync("[2/6] Extracting code structure...");
int index = 0;
foreach (var file in detectedFiles)
{
    index++;
    WriteProgress("Extracting...", index, detectedFiles.Count);
    // existing try/catch unchanged
}
await WriteLineAsync(); // flush \r to newline
await WriteLineAsync($"      Processed {processed} files, skipped {skipped}");
// ... rest unchanged
```

### Stage 2b — semantic loop (lines ~109–128)

```csharp
await WriteLineAsync("[2b/6] Running AI-enhanced semantic extraction...");
int index = 0;
foreach (var file in detectedFiles)
{
    index++;
    WriteProgress("AI extracting...", index, detectedFiles.Count);
    // existing try/catch unchanged
}
await WriteLineAsync(); // flush \r to newline
await WriteLineAsync($"      AI extracted from {semanticProcessed} files");
// ... rest unchanged
```

## Edge Cases

| Context | Behavior | Acceptable |
|---|---|---|
| xUnit `StringWriter` | `\r` stays as char in output string | Yes — progress lines not asserted |
| CI stdout redirect | `\r` appears as `^M` in logs | Yes — not critical |
| `--verbose` mode | Progress counter + verbose file list both work | Yes — `\r` clears before `\n` |
| Empty `detectedFiles` | Loop body never executes, `WriteLineAsync()` still called | Yes |

No `Console.IsOutputRedirected` check — adds complexity without meaningful benefit.

## Implementation Notes (for writing-plans)

- Single file changed: `src/Graphify.Cli/PipelineRunner.cs`
- Use **MCP Serena** `find_symbol` / `get_code_range` to locate exact line ranges of AST-loop and semantic-loop before editing — do not read the full file
- Use symbol-level edit (not full-file rewrite)
- `WriteProgress` is sync (`_output.Write`, not `WriteAsync`) — both loops are single-threaded at their level
- After each loop, call `await WriteLineAsync()` to emit `\n` and move cursor past the progress line

## Out of Scope

- Spectre.Console ProgressBar (chosen: `\r` overwrite)
- Filename display alongside counter (chosen: counter only)
- `IProgress<T>` on pipeline stage interfaces
- Stage 3–6 progress (single-call stages, no loops)
