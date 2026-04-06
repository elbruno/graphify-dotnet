# Decision: Multi-Provider IChatClient Architecture (Features 2 & 3)

**Author:** Morpheus (SDK Dev)
**Date:** 2026-04-06
**Status:** Implemented

## Context

Graphify.Sdk had a single provider stub (GitHub Models) that threw `NotImplementedException`. The short-term roadmap called for Azure OpenAI and Ollama/local model support, both using the `IChatClient` abstraction from Microsoft.Extensions.AI.

## Decision

Implemented three provider factories behind a unified `ChatClientFactory`:

| Provider | Package | Pattern |
|---|---|---|
| GitHub Models | `OpenAI` (via M.E.AI.OpenAI) | `OpenAIClient` with custom endpoint → `.AsIChatClient()` |
| Azure OpenAI | `Azure.AI.OpenAI` 2.* | `AzureOpenAIClient` → `GetChatClient(deployment)` → `.AsIChatClient()` |
| Ollama | `OllamaSharp` 5.* | `OllamaApiClient` implements `IChatClient` natively |

### Unified Entry Point

```csharp
ChatClientFactory.Create(new AiProviderOptions(AiProvider.Ollama, ModelId: "phi3"));
```

Dispatches to the correct factory. Required fields are validated per provider at creation time.

### Why OllamaSharp (not Microsoft.Extensions.AI.Ollama)

Microsoft.Extensions.AI.Ollama was deprecated in favour of OllamaSharp, which natively implements `IChatClient`. OllamaSharp is the recommended client per the .NET AI ecosystem docs.

## Impact

- Users can now target Azure OpenAI, GitHub Models, or local Ollama with zero code changes beyond config
- `CopilotExtractor` and any future pipeline stage can accept any `IChatClient` from this factory
- All 185 existing tests continue to pass
- Trinity can wire `ChatClientFactory` into the CLI when ready

## Open Questions

1. Should we add `DefaultAzureCredential` support for Azure OpenAI (Entra ID / managed identity)?
2. Should `ChatClientFactory` live in DI as a registered service, or stay as a static factory?
