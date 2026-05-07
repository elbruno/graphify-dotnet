# Progress Reporting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `[current/total]` inline progress counter (overwritten via `\r`) to Stage 2 (AST extraction) and Stage 2b (semantic extraction) loops in `PipelineRunner`.

**Architecture:** Add a sync `WriteProgress(string label, int current, int total)` private method to `PipelineRunner` that writes `\r      {label} [{current}/{total}]` to `_output`. Call it inside each file-iteration loop. After each loop, emit `await WriteLineAsync()` to advance past the `\r` line. No changes to pipeline stage interfaces.

**Tech Stack:** C# 13 / .NET 10, xUnit, `System.IO.TextWriter`

---

## File Map

| File | Action |
|---|---|
| `src/Graphify.Cli/PipelineRunner.cs` | Modify — add `WriteProgress`, counters in Stage 2 and 2b loops |
| `src/tests/Graphify.Tests/Cli/PipelineRunnerTests.cs` | Modify — add two progress-output tests |

---

### Task 1: Write failing tests for progress output

**Files:**
- Modify: `src/tests/Graphify.Tests/Cli/PipelineRunnerTests.cs`

- [ ] **Step 1: Add two failing tests**

Append these two test methods to the existing `PipelineRunnerTests` class (after the last existing test, before the closing `}`):

```csharp
[Fact]
[Trait("Category", "Cli")]
public async Task RunAsync_AstOnlyMode_WritesProgressCounter()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"graphify-progress-{Guid.NewGuid():N}");
    var outputDir = Path.Combine(Path.GetTempPath(), $"graphify-out-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    await File.WriteAllTextAsync(Path.Combine(tempDir, "Sample.cs"), "public class Sample { }");

    var output = new StringWriter();
    var runner = new PipelineRunner(output, verbose: false, chatClient: null);

    try
    {
        await runner.RunAsync(tempDir, outputDir, ["json"], useCache: false, CancellationToken.None);
        var text = output.ToString();
        Assert.Contains("[1/", text);
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
    }
}

[Fact]
[Trait("Category", "Cli")]
public async Task RunAsync_AstOnlyMode_ProgressCounterReachesTotal()
{
    var tempDir = Path.Combine(Path.GetTempPath(), $"graphify-progress-{Guid.NewGuid():N}");
    var outputDir = Path.Combine(Path.GetTempPath(), $"graphify-out-{Guid.NewGuid():N}");
    Directory.CreateDirectory(tempDir);
    await File.WriteAllTextAsync(Path.Combine(tempDir, "A.cs"), "public class A { }");
    await File.WriteAllTextAsync(Path.Combine(tempDir, "B.cs"), "public class B { }");

    var output = new StringWriter();
    var runner = new PipelineRunner(output, verbose: false, chatClient: null);

    try
    {
        await runner.RunAsync(tempDir, outputDir, ["json"], useCache: false, CancellationToken.None);
        var text = output.ToString();
        // The final counter written must reach [N/N]
        Assert.Matches(@"\[2/2\]", text);
    }
    finally
    {
        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        if (Directory.Exists(outputDir)) Directory.Delete(outputDir, recursive: true);
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```
dotnet test src/tests/Graphify.Tests/Graphify.Tests.csproj --filter "DisplayName~ProgressCounter" --no-build
```

Expected: FAIL — output does not contain `[1/` or `[2/2]` yet.

---

### Task 2: Add `WriteProgress` helper to `PipelineRunner`

**Files:**
- Modify: `src/Graphify.Cli/PipelineRunner.cs`

**Context:** Use MCP Serena to navigate to `PipelineRunner`. Call `find_symbol("WriteLineAsync")` to locate the existing helper method at the bottom of the file (~line 314). The new `WriteProgress` goes immediately after it.

- [ ] **Step 1: Locate insertion point with Serena**

In MCP Serena, run:
```
find_symbol("WriteLineAsync", file="src/Graphify.Cli/PipelineRunner.cs")
```
Note the line number of the `WriteLineAsync` method definition.

- [ ] **Step 2: Add `WriteProgress` method**

After the `WriteLineAsync` method body, insert:

```csharp
private void WriteProgress(string label, int current, int total)
{
    _output.Write($"\r      {label} [{current}/{total}]");
}
```

Full context around insertion (after `WriteLineAsync`):

```csharp
    private async Task WriteLineAsync(string message = "")
    {
        await _output.WriteLineAsync(message);
    }

    private void WriteProgress(string label, int current, int total)
    {
        _output.Write($"\r      {label} [{current}/{total}]");
    }

    private static Dictionary<int, string> BuildCommunityLabels(KnowledgeGraph graph)
```

- [ ] **Step 3: Build to confirm no errors**

```
dotnet build src/Graphify.Cli/Graphify.Cli.csproj --no-restore
```

Expected: Build succeeded, 0 errors.

---

### Task 3: Add progress counter to Stage 2 (AST loop)

**Files:**
- Modify: `src/Graphify.Cli/PipelineRunner.cs`

**Context:** Use MCP Serena `find_symbol` or `get_code_range` to navigate to the Stage 2 loop. The loop starts after `await WriteLineAsync("[2/6] Extracting code structure...");` (~line 63) and iterates `detectedFiles`. Current loop has no index counter.

- [ ] **Step 1: Locate Stage 2 loop with Serena**

```
find_symbol("Extracting code structure", file="src/Graphify.Cli/PipelineRunner.cs")
```

- [ ] **Step 2: Replace Stage 2 loop**

Replace the existing `foreach` block (starting at `foreach (var file in detectedFiles)` inside Stage 2, before Stage 2b) with:

```csharp
            var extractor = new Extractor();
            var extractionResults = new List<ExtractionResult>();
            int processed = 0;
            int skipped = 0;
            int astIndex = 0;

            foreach (var file in detectedFiles)
            {
                astIndex++;
                WriteProgress("Extracting...", astIndex, detectedFiles.Count);
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var result = await extractor.ExecuteAsync(file, cancellationToken);
                    if (result.Nodes.Count > 0 || result.Edges.Count > 0)
                    {
                        extractionResults.Add(result);
                        processed++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (Exception ex)
                {
                    if (_verbose)
                    {
                        await WriteLineAsync($"      Warning: Failed to extract {file.RelativePath}: {ex.Message}");
                    }
                    skipped++;
                }
            }

            await WriteLineAsync();
            await WriteLineAsync($"      Processed {processed} files, skipped {skipped}");
```

Note: `await WriteLineAsync()` (empty) after the loop flushes the `\r` line to a newline before printing the summary.

- [ ] **Step 3: Build**

```
dotnet build src/Graphify.Cli/Graphify.Cli.csproj --no-restore
```

Expected: Build succeeded, 0 errors.

---

### Task 4: Add progress counter to Stage 2b (semantic loop)

**Files:**
- Modify: `src/Graphify.Cli/PipelineRunner.cs`

**Context:** Use MCP Serena to navigate to Stage 2b. The loop starts after `await WriteLineAsync("[2b/6] Running AI-enhanced semantic extraction...");` (~line 105). This is the critical loop — it calls the LLM per file and was previously silent.

- [ ] **Step 1: Locate Stage 2b loop with Serena**

```
find_symbol("Running AI-enhanced semantic extraction", file="src/Graphify.Cli/PipelineRunner.cs")
```

- [ ] **Step 2: Replace Stage 2b loop**

Replace the existing `foreach` inside the `if (_chatClient != null)` block with:

```csharp
                var semanticExtractor = new SemanticExtractor(_chatClient);
                int semanticProcessed = 0;
                int semanticIndex = 0;

                foreach (var file in detectedFiles)
                {
                    semanticIndex++;
                    WriteProgress("AI extracting...", semanticIndex, detectedFiles.Count);
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var result = await semanticExtractor.ExecuteAsync(file, cancellationToken);
                        if (result.Nodes.Count > 0 || result.Edges.Count > 0)
                        {
                            extractionResults.Add(result);
                            semanticProcessed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_verbose)
                            await WriteLineAsync($"      Warning: Semantic extraction failed for {file.RelativePath}: {ex.Message}");
                    }
                }

                await WriteLineAsync();
                await WriteLineAsync($"      AI extracted from {semanticProcessed} files");
```

- [ ] **Step 3: Build**

```
dotnet build src/Graphify.Cli/Graphify.Cli.csproj --no-restore
```

Expected: Build succeeded, 0 errors.

---

### Task 5: Run all tests and commit

**Files:** none new

- [ ] **Step 1: Run the two new progress tests**

```
dotnet test src/tests/Graphify.Tests/Graphify.Tests.csproj --filter "DisplayName~ProgressCounter" --no-build
```

Expected: PASS — both tests pass.

- [ ] **Step 2: Run full test suite**

```
dotnet test graphify-dotnet.slnx --no-build
```

Expected: All tests pass, none regressed.

- [ ] **Step 3: Manual smoke test (optional but recommended)**

```
dotnet run --project src/Graphify.Cli -- run . --format json
```

Observe that `[1/N]` counter updates in-place while Stage 2 runs, then summary line appears after.

- [ ] **Step 4: Commit**

```bash
git add src/Graphify.Cli/PipelineRunner.cs \
        src/tests/Graphify.Tests/Cli/PipelineRunnerTests.cs
git commit -m "feat: add inline progress counter to AST and semantic extraction loops

Shows [current/total] counter overwritten via \\r during Stage 2 and Stage 2b.
Fixes silent hang appearance during AI-enhanced semantic extraction."
```
