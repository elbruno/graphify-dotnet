# Tank's Relative Path Handling Test Suite — Summary for Trinity

## What Was Done

I (Tank) have written **15 comprehensive xUnit tests** that define the exact behavior needed to fix GitHub Issue #2 ("Graphify files are taking absolute path instead of relative path").

## Test File Location

```
src/tests/Graphify.Tests/Export/RelativePathHandlingTests.cs
```

700+ lines of test code, organized into 6 logical sections.

## What the Tests Validate

Your implementation needs to ensure:

1. **JSON exports** store relative file paths, not absolute paths
2. **HTML exports** embed relative paths in the vis.js `source_file` field
3. **Cross-platform compatibility** — Unix (`/`) and Windows (`\`) paths both work
4. **Nested directories** are handled correctly (deeply nested paths stay relative)
5. **Edge cases** like special characters, null paths, and external files work gracefully
6. **Consistency** — exporting the same graph multiple times yields identical relative path strings

## Key Implementation Hints

- Use `Path.GetRelativePath()` to convert absolute paths to relative before serialization
- Update `JsonExporter` (the `NodeDto.FilePath` field in JSON output)
- Update `HtmlExporter` (the `source_file` field in `BuildVisNodes()`)
- Handle null `FilePath` gracefully (concept nodes)
- Test with realistic directory structures — not just flat layouts

## Running the Tests

```bash
cd C:\src\graphify-dotnet
dotnet build graphify-dotnet.slnx
dotnet test graphify-dotnet.slnx --filter "RelativePathHandlingTests"
```

**Expected:** 8 tests fail now (by design), 7 pass. After your fix, all 15 should pass.

## Test Patterns Used

- **Path validation:** `Assert.False(Path.IsPathRooted(filePath))` — universal check for absolute vs relative
- **JSON inspection:** `JsonDocument.Parse()` to read and verify JSON fields
- **HTML inspection:** Search for embedded absolute path strings in HTML output
- **Temp directories:** Tests create isolated project structures for each test

## Known Issues to Avoid

1. **HtmlExporter ambiguous overload:** When calling `ExportAsync()` on HtmlExporter, use named parameter:
   ```csharp
   await exporter.ExportAsync(graph, outputPath, cancellationToken: default);
   ```

2. **NaN serialization:** Single isolated nodes cause `maxDegree = 0` → degree calculation produces `NaN` → JSON serialization fails. Always use 2+ connected nodes in HTML tests.

3. **Path separators:** .NET's `Path.GetRelativePath()` automatically uses the OS-native separator. Don't hardcode `/` or `\`.

## Acceptance

Your fix is complete when:
- ✅ All 15 tests pass
- ✅ No regressions in existing export tests
- ✅ JSON and HTML exports produce consistent relative paths
- ✅ Edge cases handled gracefully

---

**Questions?** See `.squad/decisions/inbox/tank-relative-paths-tdd.md` for full specification.
