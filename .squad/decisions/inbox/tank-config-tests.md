# Decision: ConfigPersistence, ConfigurationFactory & CopilotSdk Test Coverage

**Author:** Tank (Tester)
**Date:** 2026-04-07
**Status:** Implemented

## Context

Trinity implemented an interactive configuration wizard (ConfigWizard, ConfigPersistence, ConfigurationFactory updates) for the CLI using Spectre.Console, plus CopilotSdk routing fixes. Tests were needed for file I/O, configuration layering, and CopilotSdk CLI override routing.

## Decision

- **ConfigWizard is NOT unit-testable** in its current form — it uses static `AnsiConsole` methods that require a TTY. If we want wizard-level testing in the future, we'd need to inject `IAnsiConsole` instead.
- **ConfigPersistence** is fully tested via file round-trips. The `[Collection("ConfigFile")]` attribute is required on any test class that reads/writes `appsettings.local.json` in `AppContext.BaseDirectory`.
- **ConfigurationFactory** integration tests write temp files and verify layer priority (local file < CLI args).
- **CopilotSdk routing** tests verify that `CliProviderOptions(Provider: "copilotsdk", Model: X)` routes to `Graphify:CopilotSdk:ModelId` (not AzureOpenAI), and that `--endpoint` is silently dropped.

## Impact

- Tests covering ConfigPersistence code paths, ConfigurationFactory local config loading, and CopilotSdk CLI override routing.
- Any future test class that touches `appsettings.local.json` in the test output directory MUST use `[Collection("ConfigFile")]` to avoid race conditions.
- appsettings.json defaults (Ollama endpoint) are loaded by ConfigurationFactory.Build() — tests must not assert those are null
