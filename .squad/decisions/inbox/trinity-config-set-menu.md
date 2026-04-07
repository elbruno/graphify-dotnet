# Decision: Interactive `config set` Wizard + ConfigurationFactory CopilotSdk Routing

**Author:** Trinity (Core Developer)
**Date:** 2026-04-07
**Status:** Implemented

## Context

The CLI had `config show` but no way to interactively set provider configuration. Additionally, `ConfigurationFactory` had a bug where `--model` and `--endpoint` CLI overrides didn't recognize the CopilotSdk provider, falling through to Azure OpenAI defaults.

## Decision

### 1. `config set` uses Console.ReadLine() interactive prompts
- Numbered menu: 1=Ollama, 2=Azure OpenAI, 3=Copilot SDK
- Provider-specific follow-up prompts with defaults where applicable
- Azure OpenAI requires all fields (endpoint, key, deployment, model)
- Ollama/CopilotSdk have sensible defaults

### 2. Persistence via `dotnet user-secrets set --id`
- Uses `System.Diagnostics.Process` to shell out to `dotnet user-secrets`
- UserSecretsId is hardcoded: `graphify-dotnet-3134eb8e-5948-4541-b6e4-ab9f52f3df62`
- Each key/value pair saved individually

### 3. ConfigurationFactory now routes all three providers
- `--model` routes to correct section key per provider
- `--endpoint` is silently ignored for CopilotSdk (no endpoint needed)

## Impact

- Users can now configure providers without manually editing secrets.json
- CopilotSdk users can use `--model` CLI flag correctly
- No breaking changes to existing commands
