# Decision: Complete .NET 10 Solution Scaffolding

**Author:** Neo (Lead/Architect)  
**Date:** 2026-04-06  
**Status:** Implemented

## Context

The project needed a complete .NET 10 solution structure as the foundation for all future work. This unblocks all other todos.

## Decision

Scaffolded the complete solution with 6 projects:

1. **src/Graphify/Graphify.csproj** — Core library targeting `net10.0`
   - Dependencies: Microsoft.Extensions.AI 10.*, QuikGraph 2.*, TreeSitter.Bindings 0.*
   - Stub folders: Pipeline/, Graph/, Export/, Cache/, Security/, Validation/, Ingest/, Models/
   - InternalsVisibleTo: Graphify.Tests

2. **src/Graphify.Cli/Graphify.Cli.csproj** — Console app (Exe)
   - Dependencies: System.CommandLine 2.*
   - Project ref: Graphify
   - Minimal console stub (System.CommandLine API simplified for now)

3. **src/Graphify.Sdk/Graphify.Sdk.csproj** — SDK library
   - Dependencies: Microsoft.Extensions.AI 10.*
   - Project ref: Graphify
   - Stub: CopilotExtractor class

4. **src/Graphify.Mcp/Graphify.Mcp.csproj** — MCP stdio server
   - Dependencies: ModelContextProtocol 0.* (actual: 1.2.0 available)
   - Project ref: Graphify
   - Stub: TODO comment for MCP server setup

5. **src/tests/Graphify.Tests/Graphify.Tests.csproj** — Unit tests
   - Dependencies: xunit 2.*, xunit.runner.visualstudio 2.*, Microsoft.NET.Test.Sdk 17.*, coverlet.collector 6.*
   - Project ref: Graphify
   - IsPackable: false, IsTestProject: true
   - One passing sample test

6. **src/tests/Graphify.Integration.Tests/Graphify.Integration.Tests.csproj** — Integration tests
   - Same test dependencies
   - Project refs: Graphify, Graphify.Cli, Graphify.Sdk
   - One passing sample test

### Root files created:
- **global.json** — SDK 10.0.100, rollForward: latestMajor
- **Directory.Build.props** — Shared build properties (latest LangVersion, nullable, implicit usings, code analysis, CI build support)
- **graphify-dotnet.slnx** — XML-based solution file with logical folders (/src/, /src/tests/)

## Key Learnings

- TreeSitter package on NuGet is `TreeSitter.Bindings` (version 0.4.0), not `TreeSitter.Bindings.CSharp`
- ModelContextProtocol package exists and is at v1.2.0
- System.CommandLine API: simplified Program.cs to basic console output stub (full CLI implementation deferred)
- All projects target `net10.0` single target (no multi-targeting)

## Validation

- `dotnet restore graphify-dotnet.slnx` — ✅ passed
- `dotnet build graphify-dotnet.slnx` — ✅ passed
- `dotnet test graphify-dotnet.slnx` — ✅ 2/2 tests passed
- Git commit e4398e1 pushed to main

## Impact

This scaffolding unblocks ALL other project work. The team can now begin implementing:
- Core pipeline stages
- AST parsing and extraction
- Graph building and export
- CLI commands
- SDK integration
- MCP server
