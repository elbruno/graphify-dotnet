# Project Context

- **Owner:** Bruno Capuano
- **Project:** graphify-dotnet — A .NET 10 port of safishamsi/graphify, a Python AI coding assistant skill that reads files, builds a knowledge graph, and surfaces hidden structure. Uses GitHub Copilot SDK and Microsoft.Extensions.AI for semantic extraction.
- **Stack:** .NET 10, C#, GitHub Copilot SDK, Microsoft.Extensions.AI, Roslyn (AST parsing), xUnit, NuGet
- **Source:** https://github.com/safishamsi/graphify — Python pipeline: detect → extract → build_graph → cluster → analyze → report → export. Uses NetworkX, tree-sitter, Leiden community detection, vis.js.
- **Created:** 2026-04-06

## Learnings

<!-- Append new learnings below. Each entry is something lasting about the project. -->

- **2026-04-06 — Copilot instructions created** at `.github/copilot-instructions.md`. Based on ElBruno convention style (from ElBruno.MarkItDotNet) but heavily adapted: stripped all NuGet publishing, single-target `net10.0`, no multi-target, no pack workflows, no nuget_logo.png. Project structure: `src/Graphify/`, `src/Graphify.Cli/`, `src/Graphify.Sdk/`, `src/Graphify.Mcp/`, `src/tests/Graphify.Tests/`, `src/tests/Graphify.Integration.Tests/`.
- **Architecture convention:** Pipeline pattern (detect → extract → build → cluster → analyze → report → export) with composition over inheritance, interfaces + DI, QuikGraph for graph structures, TreeSitter.DotNet for AST parsing.
- **Key file:** `.github/copilot-instructions.md` — the single source of truth for repo conventions, project structure, CI, and coding standards.
- **2026-04-06 20:04 — Decision recorded** in `.squad/decisions.md`. Copilot instructions decision merged from inbox. Orchestration log created at `.squad/orchestration-log/2026-04-06T20-04-neo.md`.
- **2026-04-06 — Complete solution structure scaffolded**. All 6 projects created under `src/` with correct dependencies. Solution uses `.slnx` (XML) format. Core library (Graphify) has stub folders for Pipeline/, Graph/, Export/, Cache/, Security/, Validation/, Ingest/, Models/. Build, restore, and test all pass. TreeSitter package: `TreeSitter.Bindings` (not TreeSitter.Bindings.CSharp). ModelContextProtocol package exists at v1.2.0. System.CommandLine simplified to basic Console.WriteLine stub for now. Committed with SHA e4398e1, pushed to main.
