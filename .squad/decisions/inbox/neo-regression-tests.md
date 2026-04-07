# Decision: Regression Test Suite for Fixed Bugs

**Author:** Neo (Lead/Architect)
**Date:** 2026-04-07
**Status:** Implemented

## Context

Three bugs were fixed in the project but lacked regression tests to prevent recurrence:
1. SemanticCache.ClearAsync deadlock (SemaphoreSlim reentrance)
2. InputValidator.SanitizeLabel empty string assertion failure
3. CI test hanging (no timeout configured)

## Decision

Created a dedicated `Regression/` directory under tests with 26 tests across 4 files:

| File | Tests | Bug |
|---|---|---|
| SemanticCacheRegressionTests.cs | 6 | Deadlock prevention |
| InputValidatorRegressionTests.cs | 9 | Control char sanitization |
| TimeoutRegressionTests.cs | 2 | CI hang guardrail |
| EdgeCaseTests.cs | 9 | Boundary conditions |

### Test Quality Standards Applied
- All tests use `[Fact(Timeout = 10000)]` (must be async for xUnit v2)
- Regression tests tagged `[Trait("Category", "Regression")]`
- Edge case tests tagged `[Trait("Category", "EdgeCase")]`
- XML doc comments reference the specific bug each test prevents
- Can be filtered: `dotnet test --filter "Category=Regression|Category=EdgeCase"`

### Key Learnings Codified
- xUnit v2's `Timeout` only works on async tests returning `Task`
- `Assert.DoesNotContain(emptyString, anyString)` always fails — use predicates or `Assert.False(str.Contains(char))`
- Meta-tests using reflection can enforce timeout policies across the codebase

## Impact

- All 3 fixed bugs now have regression coverage preventing recurrence
- Full test suite: 404 tests, 0 failures
- CI can use `--filter Category=Regression` for fast smoke testing
