# Project Context

- **Owner:** Bruno Capuano
- **Project:** graphify-dotnet — A .NET 10 port of safishamsi/graphify, a Python AI coding assistant skill that reads files, builds a knowledge graph, and surfaces hidden structure. Uses GitHub Copilot SDK and Microsoft.Extensions.AI for semantic extraction.
- **Stack:** .NET 10, C#, GitHub Copilot SDK, Microsoft.Extensions.AI, Roslyn (AST parsing), xUnit, NuGet
- **Source:** https://github.com/safishamsi/graphify — Python pipeline: detect → extract → build_graph → cluster → analyze → report → export. Uses NetworkX, tree-sitter, Leiden community detection, vis.js.
- **Created:** 2026-04-06

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **2026-04-07 — NuGet Publishing Plan Created**. Comprehensive plan documented in `.squad/decisions/inbox/neo-nuget-publish-plan.md` for publishing graphify-dotnet as a dotnet global tool on NuGet.org. Plan covers:
  - **Phases:** 4 phases (metadata/assets, workflow, environment setup, documentation/validation)
  - **Key decisions:** Single-target `net10.0` (not multi-target), OIDC trusted publishing (no API keys), symbol packages enabled
  - **Manual steps:** 5 one-time setup tasks for Bruno (icon generation, GitHub environment, NuGet.org OIDC trust)
  - **Workflow pattern:** Adapted from ElBruno.MarkItDotNet; triggers on GitHub `release` event + `workflow_dispatch`
  - **Validation:** Dry-run strategy (Phase 4.4) before production release
  - **Risk mitigation:** .NET 10 SDK availability, OIDC trust configuration, version management strategy
  - **Success criteria:** Package published, installable via `dotnet tool install -g graphify-dotnet`, badges in README, no long-lived credentials
  - **Open questions:** Auto-publish vs. manual (recommend manual), pre-release strategy, post-publish validation timing
  - **Security:** Minimal permissions (id-token: write, contents: read), environment-specific secrets, OIDC eliminates key rotation burden
  - **Post-publish:** Plan includes local install verification, NuGet.org metrics monitoring, future auto-publish possibility

- **2026-04-07 — Regression test suite created**at `src/tests/Graphify.Tests/Regression/`. 26 tests across 4 files covering all 3 fixed bugs plus edge cases:
  - `SemanticCacheRegressionTests.cs` (6 tests): Deadlock prevention for ClearAsync — verifies SemaphoreSlim reentrance fix (SaveIndexCoreAsync pattern), concurrent operations, sequential clears, and load testing.
  - `InputValidatorRegressionTests.cs` (9 tests): SanitizeLabel control char stripping — verifies regex + char.IsControl fallback, mixed content preservation, null bytes, unicode handling, extended ASCII preservation, empty/long input graceful handling.
  - `TimeoutRegressionTests.cs` (2 tests): CI hang prevention — meta-test guardrail scans Regression namespace for missing Timeout attributes, plus root-cause verification showing SemaphoreSlim(1,1) double-acquire times out rather than deadlocks.
  - `EdgeCaseTests.cs` (9 tests): Boundary conditions for KnowledgeGraph (null, empty, duplicate, missing-node edges), SemanticCache thread safety (concurrent reads/writes), Extractor resilience (empty files, binary files), FileDetector deep traversal.
  - **xUnit v2 constraint**: `[Fact(Timeout = X)]` only works on async tests. All synchronous tests must return `async Task` (with `await Task.CompletedTask`) to support Timeout.
  - **xUnit DoesNotContain trap**: `Assert.DoesNotContain(needle, haystack)` where needle is empty string will always fail — empty string is found in any string. This was exactly Bug 2's root cause. Use `Assert.False(str.Contains(char))` or `Assert.DoesNotContain(collection, predicate)` instead.
  - **Pre-existing build errors fixed**: `SemanticExtractorTests.FakeChatClient` had stale `IList<ChatMessage>` signatures (interface changed to `IEnumerable<ChatMessage>`). `ExportIntegrationTests` had ambiguous overload call to `HtmlExporter.ExportAsync`. Both fixed to unblock test runs.

- **2026-04-06 — Blog post created** at `docs/blog-post.md`. 800+ word dev blog post in Bruno's conversational, first-person style. Catchy title: "I Built a .NET 10 Knowledge Graph Builder (Inspired by Karpathy)." Structure: hook with Karpathy + @socialwithaayan tweets, explanation of graphify-dotnet with ASCII pipeline diagram, 4 code samples (build, query, explain, export), key features bulleted, architecture section with pipeline diagram, 2 image placeholders (16:9 hero, 1:1 social), Future Plans section summarizing 5 roadmap items (dotnet tool, Azure OpenAI, Ollama, watch mode, Roslyn C# extraction), strong CTA linking to GitHub repo, author sign-off with blog/YouTube/LinkedIn/Twitter/Podcast links matching elbruno.com style. All links verified (Karpathy tweet, @socialwithaayan tweet, graphify-dotnet repo). Image placeholders reference prompts from docs/image-prompts.md with full prompt text as HTML comments.

- **2026-04-06 — Image prompts document created** at `docs/image-prompts.md`. Comprehensive collection of 8 AI image generation prompts (4 per format) capturing graphify-dotnet's core concepts: code-to-graph transformation, interactive visualization, pipeline architecture, open-source community, knowledge graph aesthetics, CLI developer experience, technology badge, and Karpathy-inspired knowledge concept. Formats: 16:9 (1792×1024) for blog heroes, 1:1 (1024×1024) for LinkedIn/Twitter. Each prompt includes full text ready for DALL-E/Midjourney, alt text, and visual description. Document created to support marketing and social media outreach.

- **2026-04-06 — Copilot instructions created** at `.github/copilot-instructions.md`. Based on ElBruno convention style (from ElBruno.MarkItDotNet) but heavily adapted: stripped all NuGet publishing, single-target `net10.0`, no multi-target, no pack workflows, no nuget_logo.png. Project structure: `src/Graphify/`, `src/Graphify.Cli/`, `src/Graphify.Sdk/`, `src/Graphify.Mcp/`, `src/tests/Graphify.Tests/`, `src/tests/Graphify.Integration.Tests/`.
- **Architecture convention:** Pipeline pattern (detect → extract → build → cluster → analyze → report → export) with composition over inheritance, interfaces + DI, QuikGraph for graph structures, TreeSitter.DotNet for AST parsing.
- **Key file:** `.github/copilot-instructions.md` — the single source of truth for repo conventions, project structure, CI, and coding standards.
- **2026-04-06 20:04 — Decision recorded** in `.squad/decisions.md`. Copilot instructions decision merged from inbox. Orchestration log created at `.squad/orchestration-log/2026-04-06T20-04-neo.md`.
- **2026-04-06 — Complete solution structure scaffolded**. All 6 projects created under `src/` with correct dependencies. Solution uses `.slnx` (XML) format. Core library (Graphify) has stub folders for Pipeline/, Graph/, Export/, Cache/, Security/, Validation/, Ingest/, Models/. Build, restore, and test all pass. TreeSitter package: `TreeSitter.Bindings` (not TreeSitter.Bindings.CSharp). ModelContextProtocol package exists at v1.2.0. System.CommandLine simplified to basic Console.WriteLine stub for now. Committed with SHA e4398e1, pushed to main.
- **2026-04-06 — Documentation and CI completed**. Created README.md following exact structure from copilot-instructions.md with all sections (badges, features, getting started, usage, architecture, export formats, building from source, author, acknowledgments). Included credits to original tweet by @socialwithaayan and source repo safishamsi/graphify. Created ARCHITECTURE.md with detailed pipeline stages, data model layers (Extraction vs Graph), QuikGraph integration rationale, Python-to-.NET mappings, AI integration via Microsoft.Extensions.AI, export system, and performance characteristics. Created `.github/workflows/build.yml` CI pipeline (ubuntu-latest, .NET 10, restore/build/test on graphify-dotnet.slnx). Build verified successful. Committed with SHA a800718, NOT pushed per instructions.
