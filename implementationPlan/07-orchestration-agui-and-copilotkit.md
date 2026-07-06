# 07 — Orchestration, AG-UI & CopilotKit

**Purpose:** wire the orchestrator as a single **AG-UI** agent whose specialist calls stream to the
frontend as live generative-UI cards, with a **human-in-the-loop scope gate**. This is the visible
"agentic design" the rubric rewards.

**Depends on:** 05, 06. **Blocks:** 10.

> **Landmine (HACKATHON-FINDINGS §3):** .NET multi-agent *workflow* streaming over AG-UI is
> Python-only. Expose ONE AG-UI agent; run specialists as in-process **function tools**. Pin the
> prerelease AG-UI NuGet versions on day 0.

---

## A. C# — host the orchestrator over AG-UI

Packages (pin exact prerelease versions): `Microsoft.Agents.AI`,
`Microsoft.Agents.AI.Hosting.AGUI.AspNetCore`, `Microsoft.Agents.AI.Foundry`.

`Orchestration/PrismOrchestrator.cs` — define the orchestrator agent and register specialists +
deterministic steps as `AIFunctionFactory.Create(...)` tools with `[Description]`:

| Tool | Backed by | Renders as |
|---|---|---|
| `retrieveProviderRating(issuerId, provider)` | ProviderExplainerAgent (06) | evidence card |
| `groundFundamentals(issuerId, asOf)` | FundamentalsAgent (06) + EDGAR/FRED (04) | fundamentals card |
| `decomposeDivergence(issuerId)` | **DivergenceDecomposer (05, deterministic)** | waterfall payload |
| `evaluateRedFlags(issuerId)` | **RedFlagEngine (05, deterministic)** + narrator (06) | red-flag card |
| `confirmScope(issuer, asOf, providers)` | `ApprovalRequiredAIFunction` | **HITL gate** |

`Program.cs` additions:
```csharp
builder.Services.AddPrismAgents();                 // orchestrator + tools
// ...
app.MapAGUI("/prism", sp => sp.GetRequiredService<PrismOrchestrator>().Agent);
```

The orchestrator's instructions: *parse the issuer + as-of → call `confirmScope` (wait) → fan out
`retrieveProviderRating` per provider + `groundFundamentals` → `decomposeDivergence` →
`evaluateRedFlags` → assemble the `ReconciliationDossier` and persist (package 08).* Ordering and
math come from the deterministic tools; the model orchestrates and narrates.

---

## B. Node sidecar — switch to AG-UI

Change `copilot-runtime/server.ts` from `OpenAIAdapter` + actions to:
```ts
import { CopilotRuntime, ExperimentalEmptyAdapter, copilotRuntimeNodeHttpEndpoint } from '@copilotkit/runtime'
import { HttpAgent } from '@ag-ui/client'

const prism = new HttpAgent({ url: process.env.PRISM_AGUI_URL! }) // → C# /prism
const runtime = new CopilotRuntime({ agents: { prism } })
const serviceAdapter = new ExperimentalEmptyAdapter()
```
Keep the old actions file committed as the **fallback** path (package 00).

**Security (from accelerator server.ts):** authenticate the `/copilotkit` endpoint; forward the
end-user bearer token to the C# API; treat all LLM tool arguments (issuerId, asOf) as hostile — the
API re-authorizes them.

---

## C. Frontend — consume the AG-UI agent

- `useCoAgent({ name: 'prism' })` to drive/observe the run.
- `useCopilotAction({ name, render })` (or `renderToolCall`) to render each streaming tool call as a
  card in the Evidence Stream (package 10).
- `renderAndWaitForResponse` on `confirmScope` → the scope-confirmation gate UI.

---

## D. Fallback plan (keep buildable)
If AG-UI streaming breaks late: expose `POST /api/v1/reconciliations` that runs the same orchestration
synchronously and returns the dossier; stream progress via SSE/SignalR; render cards from the dossier.
~1–1.5 days, already de-risked because the deterministic core + agents are independent of transport.

---

## Acceptance for this package
- [ ] `POST` to `/prism` (via the sidecar) streams: gate → provider cards → fundamentals →
      waterfall → red-flag, end-to-end for NordStar.
- [ ] The `confirmScope` gate pauses the run until the user approves in the UI.
- [ ] Deterministic tools return identical numbers on repeat runs.
- [ ] Fallback REST endpoint produces the same dossier object.
