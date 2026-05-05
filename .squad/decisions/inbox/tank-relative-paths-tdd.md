# Decision: Relative Path Handling — TDD Test Suite for Issue #2

**Status:** New  
**Date:** 2026-04-07  
**Owner:** Tank (Tester)  
**Related:** GitHub Issue #2 "Graphify files are taking absolute path instead of relative path"  
**Routing:** To Trinity (Implementer)

---

## Problem

Users report that JSON and HTML exports store absolute file paths (e.g., `C:\Users\...\src\Class1.cs`) instead of relative paths. This breaks portability: exported graphs are hard to share or move because path references are machine-specific.

---

## Solution

Implemented **test-driven specification** for relative path handling:
- **15 comprehensive xUnit tests** validating JSON/HTML export relative path behavior
- Tests cover: basic relative paths, cross-platform normalization, nested directories, special characters, edge cases
- Tests are **intentionally failing** (by design) — they spec out what Trinity needs to implement

---

## Test Suite Details

**File:** `src/tests/Graphify.Tests/Export/RelativePathHandlingTests.cs`

**Test Categories:**

1. **JSON Relative Paths (5 tests)**  
   - Node file paths must not be `Path.IsPathRooted()` == true
   - Multiple output locations preserve same relative path strings
   - Edge metadata paths also relative

2. **HTML Relative Paths (2 tests)**  
   - Embedded JSON within HTML has relative file paths
   - `source_file` field in vis.js nodes uses relative paths

3. **Cross-Platform Normalization (4 tests)**  
   - Unix (`/`) paths → relative
   - Windows (`\`) paths → relative
   - Mixed separators handled gracefully

4. **Nested & Complex Structures (2 tests)**  
   - Deeply nested directories (4 levels) stay relative
   - Output dir nested within project preserves root-relative paths

5. **Edge Cases (2 tests)**  
   - Special chars (dashes, underscores, dots)
   - Null/empty paths (concept nodes)
   - Paths outside project root

6. **Consistency (1 test)**  
   - Same graph exported twice yields identical relative path strings

---

## Key Testing Patterns

- **Path validation:** `Assert.False(Path.IsPathRooted(filePath))` universally validates relative vs absolute
- **JSON inspection:** Parse exported JSON with `JsonDocument.Parse()` to verify `file_path` fields
- **HTML embedded JSON:** Search for `"source_file":"C:\\"` patterns in embedded vis.js data
- **Test isolation:** Each test creates fresh temp directory structure (`_testRoot/MyProject/src/...`)
- **HTML exporter gotcha:** Ambiguous `ExportAsync()` overload — must use named `cancellationToken: default` parameter
- **NaN serialization issue:** Single isolated nodes cause degree calc → NaN → JSON serialization error. Always test with 2+ nodes connected by edges.

---

## Implementation Notes for Trinity

**Current behavior:**  
- JsonExporter stores absolute paths: `C:\Users\brunocapuano\AppData\Local\Temp\...\MyProject\src\Class1.cs`
- HtmlExporter embeds absolute paths in `source_file` field

**Expected behavior:**  
- Store relative paths: `src/Class1.cs` or `src\Class1.cs` (OS-native separator)

**Suggested approach:**

1. Identify a consistent **reference point** for relative paths (likely: project root or common ancestor directory)
2. Use `Path.GetRelativePath(referencePath, filePath)` before serialization
3. Update JsonExporter `NodeDto.FilePath` and HtmlExporter's `BuildVisNodes()` to use relative paths
4. Handle edge cases:
   - Null/empty FilePath (concept nodes) → skip or store as-is
   - Paths outside project root → handle gracefully (may remain absolute or use `../` navigation)
   - Cross-platform separators → .NET's `Path.GetRelativePath()` handles this automatically

---

## Test Status

- **Total:** 15 tests
- **Failing:** 8 (by design — TDD spec)
- **Passing:** 7 (non-file-path scenarios: empty graphs, simple exports)

**Running tests:**
```bash
cd src\graphify-dotnet
dotnet test graphify-dotnet.slnx --filter "RelativePathHandlingTests"
```

---

## Acceptance Criteria

✅ All 15 tests pass after Trinity's implementation  
✅ Relative paths are consistent across export formats (JSON, HTML)  
✅ Cross-platform path normalization works on Windows and Linux  
✅ Edge cases (deeply nested dirs, external files, null paths) handled gracefully  
✅ No regressions in existing export tests

---
