# ItsAllSemantics

Lightweight Blazor + SignalR demo that showcases streaming LLM responses, multi-turn context handling, and an experimental multi-agent orchestration layer built on Semantic Kernel.

This repository contains a working demo of token/fragment streaming from chat completion services, a SignalR-based real-time pipeline for Blazor, and an experimental concurrent orchestrator using Semantic Kernel's orchestration primitives.

## Highlights / What you'll find here

- Blazor Server frontend with SignalR client for real-time streaming UI.
- Streamed event model (`StreamingChatEvent`) with Start / Delta / End / Error events.
- `IChatResponder` and `IChatOrchestrator` abstractions for pluggable responders.
- `SemanticKernelConcurrentOrchestrator` — experimental SK concurrent orchestrator (Summarizer + Expander) that multiplexes agent deltas into a single stream.
- Diagnostics, preflight checks and fallback paths (synthetic injection, single-agent fallbacks) to make debugging streaming issues easier.
- Detailed roadmap and design notes in `Docs/ROADMAP.md`.

## Quick start (development)

Prereqs: .NET 9 SDK, an LLM provider (OpenAI or Azure OpenAI) and the provider API key.

1. Set API keys and feature flags (either via `appsettings.json` or environment variables). Common settings:

```bash
# for OpenAI (example)
export OPENAI_API_KEY="sk-..."
export PROVIDER="OpenAI"        # or AzureOpenAI
export MODEL="gpt-4o-mini"      # example model id used in samples

# for Azure OpenAI
export AZURE_OPENAI_API_KEY="..."
export AZURE_OPENAI_ENDPOINT="https://my-azure-openai.openai.azure.com/"
export PROVIDER="AzureOpenAI"
```

2. (Optional) enable the experimental concurrent SK orchestrator in `ItsAllSemantics.Web/appsettings.json`:

```json
{
  "SemanticKernel": {
    "UseMultiAgentSKConcurrent": true
  }
}
```

3. Run the application:

```bash
dotnet restore
dotnet run --project ItsAllSemantics.Web/ItsAllSemantics.Web.csproj -c Debug
```

4. Open the UI in your browser (check the console and server logs for streaming events). The UI shows live streaming bubbles, per-agent metadata for concurrent orchestrations, and a diagnostics panel.

## How streaming works (short)

- The hub/responder pipeline returns `IAsyncEnumerable<StreamingChatEvent>`.
- The client receives Start → many Delta events (fragmented text) → End.
- The concurrent orchestrator wires SK's `ConcurrentOrchestration` callbacks into those Delta events so multiple agents can stream into one multiplexed channel with metadata: `agent`, `agentSeq`, `globalSeq`, `status`, `mode`.

## Troubleshooting streaming silence

If you see no streaming tokens from the orchestrator but single-agent calls work:

- Confirm API keys and provider selection in `appsettings.json` or environment variables.
- Check server logs for the preflight diagnostic log entry (the orchestrator emits a preflight diagnostic delta on first run).
- Verify the orchestrator is creating a Kernel per agent (logs: "Agent {Agent} created model=...").
- Look for these diagnostic paths in logs: runtime pump, streaming callback activation, synthetic injection messages.
- If nothing appears, try toggling the fallback: synthetic injection is triggered after a short grace window — it's a visible sign that orchestration didn't produce deltas.

If you want to inspect a single agent directly (to validate service/model behavior): use the direct single-agent fallback in `SemanticKernelConcurrentOrchestrator` — the code already contains both non-streaming and streaming direct invoke attempts with tracing.

## Development notes

- Code of interest:
  - `ItsAllSemantics.Web/Services/SemanticKernelConcurrentOrchestrator.cs` — the concurrent orchestrator implementation.
  - `ItsAllSemantics.Web/Hubs/ChatHub.cs` — SignalR hub wiring.
  - `Docs/ROADMAP.md` — roadmap, design decisions and next steps.
- Logging is intentionally verbose around orchestration and fallbacks to make diagnosing streaming flow easier.
