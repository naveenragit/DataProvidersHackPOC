# Adversarial Stack-Fit Review: Prism Package 07 (Orchestration, AG-UI & CopilotKit)

**Reviewer:** Fin Adversary Stack Critic · **Date:** 2026-07-06 · **Target:** work-package 07 shipped C# + deferred Node/React interop

## VERDICT

- **SDK surface: GO** — every AG-UI / Agent Framework surface used is REAL (source-verified in `microsoft/agent-framework` + compiles clean under `TreatWarningsAsErrors`). No hallucinated method or overload found. All four deferred npm exports (`HttpAgent`, `CopilotRuntime`, `ExperimentalEmptyAdapter`, `copilotRuntimeNodeHttpEndpoint`) are real and resolvable.
- **Version interop: AMBER** — restores + builds green today, but three latent seams: (1) `OpenAI` floats to **2.10.0** under an `Azure.AI.OpenAI 2.1.0` pin (9 minors past its tested floor); (2) the whole hosting/extensions substrate is the **.NET 10 wave** (Hosting 10.0.1, Extensions.AI 10.6.0) pinned into a **net9.0** app plus a **prerelease** AG-UI package; (3) the deferred CopilotKit sidecar's runtime line (1.5x–1.62x, needed for the AG-UI `agents` model) is **incompatible with the frontend's pinned `@copilotkit/react-core`/`react-ui` 1.4.8**.
- **Real-data: GO** — on a fresh clone with only `gpt-5.4` deployed the shipped paths do **NOT** 404 on deployment names (all four model roles now default to `gpt-5.4`, in both `PrismOptions` and `.env.example`). The deterministic money-moment (STALE_INPUT) needs no LLM. **Caveat (see STK-03):** the SSE stream transport is not resilient to a Foundry fault — it aborts instead of degrading — so the robust real-data path is the REST fallback, not the stream.

---

## Verification Log

| Claim under test | Method | Source / evidence | Verdict |
|---|---|---|---|
| `app.MapAGUI("/prism", agent)` is a real extension at this version | GitHub source | `microsoft/agent-framework` `AGUIEndpointRouteBuilderExtensions.cs` L101-115: `MapAGUI(this IEndpointRouteBuilder endpoints, [StringSyntax("route")] string pattern, AIAgent aiAgent)` | **VERIFIED REAL** — shipped call matches the `(pattern, AIAgent)` overload exactly |
| `builder.Services.AddAGUI()` is real | GitHub source | `MicrosoftAgentAIHostingAGUIServiceCollectionExtensions.AddAGUI(IServiceCollection)` (namespace `Microsoft.Extensions.DependencyInjection`) | **VERIFIED REAL** |
| `AsAIAgent(instructions:, name:)` (Chat, no tools) is GA | Compiler + pkg06 log | `AzureAgentRunner.CreateAgent` → `GetChatClient(dep).AsAIAgent(instructions:, name:)`; builds 0-warn | **VERIFIED REAL** |
| `AsIChatClient().AsAIAgent(instructions:, name:, tools:)` (tools overload) is real | Compiler | `PrismAgentOrchestrator.BuildAgent` → `chat.AsAIAgent(instructions:, name:, tools: tools)`; builds 0-warn | **VERIFIED REAL** |
| `new ApprovalRequiredAIFunction(AIFunctionFactory.Create(...))` + `MEAI001` pragma | Compiler | `PrismAgentOrchestrator.BuildAgent` L94-103; resolved `Microsoft.Extensions.AI 10.6.0`; builds 0-warn | **VERIFIED REAL** |
| `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore 1.13.0-preview.260703.1` exists + resolves | NuGet flat-container + `dotnet list package` | nuspec 200; resolved graph shows it + `Microsoft.Agents.AI.Hosting` 1.13.0-preview.260703.1 | **VERIFIED REAL** (prerelease-only) |
| Resolved `OpenAI` base library version | `dotnet list package --include-transitive` | **OpenAI 2.10.0**, System.ClientModel 1.14.0, Microsoft.Extensions.AI 10.6.0 | **VERIFIED** — Azure.AI.OpenAI 2.1.0 runs on OpenAI 2.10.0 (STK-01) |
| `Microsoft.Extensions.Hosting >= 10.0.1` forced into net9.0 app | `dotnet list package` | Hosting **10.0.1**, Hosting.Abstractions **10.0.3**, DI **10.0.1** (all transitive) | **VERIFIED** (STK-02) |
| `Microsoft.Agents.AI.Foundry` referenced/needed | csproj read + resolved graph | **NOT referenced** anywhere; runner uses `Azure.AI.OpenAI` + `Microsoft.Agents.AI[.OpenAI]` only | **VERIFIED absent** — spec §A's named package is not used (correctly; it's not needed for the Chat path) |
| `@ag-ui/client` `HttpAgent` export | jsDelivr `index.d.ts` @0.0.57 | `declare class HttpAgent extends AbstractAgent { constructor(config: HttpAgentConfig); run(input): Observable<BaseEvent> }`, exported | **VERIFIED REAL** |
| `@copilotkit/runtime` `CopilotRuntime`/`ExperimentalEmptyAdapter`/`copilotRuntimeNodeHttpEndpoint` | GitHub `CopilotKit/CopilotKit` source | `export const ExperimentalEmptyAdapter = EmptyAdapter`; `copilotRuntimeNodeHttpEndpoint(options)`; `new CopilotRuntime({ agents })` across dozens of showcase routes | **VERIFIED REAL** |
| C# `/prism` endpoint shape matches what `HttpAgent` posts | Both sources | MapAGUI = `MapPost(pattern, ([FromBody] RunAgentInput) → AGUIServerSentEventsResult)`; HttpAgent posts `RunAgentInput`, parses SSE `BaseEvent` | **VERIFIED coherent** — the sidecar↔C# hop is sound |
| `@copilotkit/runtime` latest vs frontend pin | npm registry | runtime latest **1.62.2**; frontend `@copilotkit/react-core`/`react-ui` **1.4.8** (pkg09) | **VERIFIED mismatch** (STK-04) |
| Fresh-clone model names 404? | Code read | `PrismOptions.ModelOptions` all default `gpt-5.4`; `.env.example` L43-46 all `gpt-5.4` | **VERIFIED — no 404** (pkg06 STK-02 fixed) |
| Landmine respected (single agent, no .NET workflow streaming) | Code read | ONE `AIAgent` built with `AIFunctionFactory.Create` tools; no `MapWorkflow`/workflow-streaming; `Microsoft.Agents.AI.Workflows 1.13.0` present transitively but unused | **VERIFIED respected** (STK-10) |

---

## Findings

### [STK-01] `Azure.AI.OpenAI 2.1.0` actually executes on `OpenAI 2.10.0` — open-ended base-library float — Severity: High
- **Target:** [backend/FinancialServices.Api/FinancialServices.Api.csproj](../../../backend/FinancialServices.Api/FinancialServices.Api.csproj) (`Azure.AI.OpenAI` 2.1.0 line + its "GA-only / matches SeedData pin" comment); consumed by [AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs) `GetChatClient(...).AsAIAgent(...)`.
- **Claim under test:** csproj comment — *"Azure.AI.OpenAI 2.1.0 matches the tools/SeedData pin"* / *"GA-only, inline C# agents."*
- **Reality (ground truth, `dotnet list package --include-transitive`):** `Microsoft.Agents.AI.OpenAI 1.13.0` floors `OpenAI` at `[2.10.0,)` and `System.ClientModel` at `[1.14.0,)`. NuGet unifies **up** → the resolved base is **OpenAI 2.10.0 / System.ClientModel 1.14.0 / Microsoft.Extensions.AI 10.6.0**. `Azure.AI.OpenAI 2.1.0` shipped against `OpenAI 2.1.0`, so it now runs on a base library **nine minor versions** past its tested floor. No *stable* `Azure.AI.OpenAI` targets OpenAI 2.10.0 (the aligned ones are all beta). The "2.1.0 GA pin" is half-true: it pins the wrapper, not the base surface that actually executes.
- **Fix:** explicitly pin `OpenAI 2.10.0` + `System.ClientModel 1.14.0` in the csproj so the executing base is deterministic, and add a CI transitive-restore gate that fails on an unexpected `OpenAI` bump. Runs green today (same major), hence High not Critical. (Carried unresolved from pkg06 STK-01.)

### [STK-02] .NET 10 preview-wave substrate pinned into a `net9.0` app — Severity: High
- **Target:** [FinancialServices.Api.csproj](../../../backend/FinancialServices.Api/FinancialServices.Api.csproj) — `<TargetFramework>net9.0</TargetFramework>` + the `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore 1.13.0-preview.260703.1` reference.
- **Claim under test:** the package comments frame this as a stable "GA core (1.13.0)" story.
- **Reality:** the resolved graph is dominated by the **.NET 10 wave**: `Microsoft.Extensions.Hosting 10.0.1`, `Microsoft.Extensions.Hosting.Abstractions 10.0.3`, `Microsoft.Extensions.DependencyInjection 10.0.1`, `Microsoft.Extensions.AI 10.6.0`. The **prerelease** AG-UI transitive dep is what drags `Hosting` up to ≥ 10.0.1 (the repo already ate an `NU1605` in `tools/SeedData` for exactly this and bumped Hosting 9.0.0 → 10.0.1). A `net9.0` app on the .NET 9 runtime consuming .NET-10-wave assemblies is coherent **only** as long as those packages keep shipping a `net9.0` asset; it builds green now (no `NU1701` fallback), but any Extensions 10.x servicing bump can pull a newer BCL, and on ACA (pkg 11) the container runtime must match.
- **Fix:** either move the app to `net10.0` to match the substrate it already ships, **or** explicitly pin the `Microsoft.Extensions.*` versions and gate CI. Document the net9.0-on-net10-wave decision instead of hiding it behind a "GA core" comment.

### [STK-03] The SSE stream (the DEFAULT demo transport) aborts on any Foundry fault instead of degrading — Severity: High
- **Target:** [PrismStreamingOrchestrator.cs](../../../backend/FinancialServices.Api/Orchestration/PrismStreamingOrchestrator.cs) L62-88; [ProviderExplainerAgent.cs](../../../backend/FinancialServices.Api/Agents/ProviderExplainerAgent.cs) L42; [FundamentalsAgent.cs](../../../backend/FinancialServices.Api/Agents/FundamentalsAgent.cs) L40; [ReconciliationsController.cs](../../../backend/FinancialServices.Api/Controllers/ReconciliationsController.cs) L66-88.
- **Claim under test:** spec §D — the SSE path is the *"guaranteed-green fallback transport."*
- **Reality:** `PrismStreamingOrchestrator.RunAsync` narrates each provider/fundamentals card **before** it assembles the dossier, and those narrator calls are **unguarded**: `ProviderExplainerAgent.ExplainAsync` and `FundamentalsAgent.SummarizeAsync` both `await runner.RunAsync(...)` with **no try/catch**. Any Foundry fault — `ConfigurationException` (endpoint unset), 401/404/429, or a content-filter 400 — propagates up through the orchestrator (which also has no per-step guard) to the controller's `catch (Exception)`, which emits a single `error` event and the stream **stops**. Contrast the REST path: `IReconciliationService.RunAsync` wraps narration and falls back to the deterministic dossier, so `POST /reconciliations` is robust while `POST /reconciliations/stream` is not. Worse, the deterministic dossier (the money moment) is only computed at step 4 (`assembleDossier`), **after** the fragile narration steps — so a transient narration hiccup denies the user even the deterministic result on the stream.
- **Fix:** wrap each narrated card step in try/catch and emit the card with an empty narrative on fault (mirror the REST `NarrationGuard`-drop contract), **or** assemble the deterministic dossier first and narrate opportunistically. A narration fault must never abort the deterministic stream.

### [STK-04] Deferred CopilotKit sidecar: frontend `@copilotkit/*` 1.4.8 is on a different version line than the runtime that has the AG-UI `agents` model — Severity: High
- **Target:** deferred `copilot-runtime/` sidecar (spec §B) + `frontend/package.json` (`@copilotkit/react-core`/`react-ui` **1.4.8**, pkg09).
- **Claim under test:** spec §B — swap the sidecar to `new CopilotRuntime({ agents: { prism } })` + `ExperimentalEmptyAdapter` + `copilotRuntimeNodeHttpEndpoint` + `HttpAgent`.
- **Reality:** the AG-UI `agents` model (`new CopilotRuntime({ agents: { name: new HttpAgent({url}) } })` + `ExperimentalEmptyAdapter`) is the **current-CopilotKit** pattern — verified across ~30 `CopilotKit/CopilotKit` showcase routes, all on modern CopilotKit; `@copilotkit/runtime` latest is **1.62.2**. CopilotKit requires `react-core`/`react-ui`/`runtime` to be **version-aligned**. To ship §B the frontend CopilotKit surface must jump from **1.4.8** to the runtime's line — a large bump (pkg09 already fought broken 1.4.4/1.4.5 empty-`dist` tarballs to land on 1.4.8). Two concrete blockers:
  1. **Type rejection:** `new CopilotRuntime({ agents })` does **not** typecheck against the published `@copilotkit/runtime` types — CopilotKit's own repo annotates every occurrence with `// @ts-ignore -- Published CopilotRuntime agents type wraps Record in MaybePromise<NonEmptyRecord<...>> which rejects plain Records; fixed in source, pending release.`
  2. **Version-line split:** `useCoAgent` / `useCopilotAction` wiring (§C) against react-core 1.4.8 predates the mature AG-UI agent-lock model.
- **What's sound:** all four exports are REAL (`HttpAgent` @ `@ag-ui/client` 0.0.57; `CopilotRuntime`/`ExperimentalEmptyAdapter`/`copilotRuntimeNodeHttpEndpoint` @ `@copilotkit/runtime`), and the **sidecar↔C#** hop is coherent — `HttpAgent` posts `RunAgentInput` and parses SSE `BaseEvent`, which is exactly what C# `MapAGUI` serves. The risk is entirely on the **browser↔sidecar** hop (CopilotKit's own protocol, version-sensitive).
- **Fix:** when building the sidecar, pin `@copilotkit/runtime` **and** bump `frontend` `@copilotkit/react-core`+`react-ui` to the **same** minor; add the `// @ts-ignore` (or move to a runtime version where the `agents` type is fixed); re-run the pkg09 tarball-integrity probe on the chosen version. This is the single biggest integration risk in §B/§C.

### [STK-05] Serial LLM fan-out on one `gpt-5.4` deployment + no per-call timeout — Severity: Medium
- **Target:** [PrismStreamingOrchestrator.cs](../../../backend/FinancialServices.Api/Orchestration/PrismStreamingOrchestrator.cs) L62-88 (serial provider `foreach` + fundamentals); [AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs) L49 (`agent.RunAsync(prompt, cancellationToken: ct)` — only the ambient `ct`, no timeout).
- **Reality:** the stream narrates providers serially, then fundamentals, then `reconciliation.RunAsync` (which itself narrates flags + divergences) — all on the **single** `gpt-5.4` deployment. NordStar ≈ 3 provider + 1 fundamentals + dossier-internal narrations back-to-back; under any concurrency this invites 429 and multi-second wall-clock (mirrors pkg06 STK-04). No per-call timeout means a single hung Foundry call stalls the whole serial sweep.
- **Fix:** wrap `runner.RunAsync` in a linked `CancellationTokenSource` with a per-call timeout; parallelize the independent provider narrations; add jittered 429 retry in `AzureAgentRunner`.

### [STK-06] Cross-language contract drift — C# DTOs ↔ TS types are hand-maintained, no generated client — Severity: Medium
- **Target:** [Models/PrismDtos.cs](../../../backend/FinancialServices.Api/Models/PrismDtos.cs) + [Orchestration/PrismStreamEvents.cs](../../../backend/FinancialServices.Api/Orchestration/PrismStreamEvents.cs) ↔ `frontend/src/types/prism.ts` (+ the deferred §C Evidence Stream).
- **Reality:** `ProviderVerdictDto`/`PairDivergenceDto`/`RedFlagDto`/`DossierResponse` are mirrored **by hand** into TypeScript (pkg09/pkg10 notes literally say "re-sync when `PrismDtos.cs` lands"). Package 07 **doubles** the drift surface: `PrismStreamEvents.cs` adds `ScopeConfirmPayload`/`ProviderRatingPayload`/`FundamentalsPayload`/`DivergencePayload`/… **and** a set of wire-string discriminators (`PrismStreamEventTypes.ScopeConfirm = "scope-confirm"`, etc.) that the deferred frontend must mirror by hand too. There is no OpenAPI-generated client (NSwag/Kiota/openapi-typescript) despite `AddOpenApi()` already emitting `/openapi/v1.json`. Concrete drift: rename `ProviderVerdictDto.MethodologyDocId` in C# → TS stays stale → silent `undefined` at runtime, **no** compile error on either side.
- **Fix:** generate the TS client from the emitted OpenAPI doc in CI and fail on drift; at minimum add a contract test asserting the event-type strings and DTO field names.

### [STK-07] AG-UI endpoint merges client-supplied tools + trusts wire `ThreadId` (session-bleed) — latent, on-by-default when `AgUiEnabled=true` — Severity: Medium
- **Target:** [Program.cs](../../../backend/FinancialServices.Api/Program.cs) L79-82 (`MapAGUI("/prism", …)`, no auth); [Infrastructure/ServiceCollectionExtensions.cs](../../../backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs) `AddPrismOrchestration` (`AddAGUI()` only — no session store / isolation).
- **Reality (verified from `microsoft/agent-framework` `AGUIEndpointRouteBuilderExtensions.cs`):** the mapped `MapPost` handler injects **client-supplied** tools from the wire — `var clientTools = input.Tools?.AsAITools().ToList(); ChatOptions.Tools = clientTools` — and the source's own "Trust model" remark warns that `RunAgentInput.ThreadId` "arrives from the wire and is treated as a chain-resume identifier — any caller who knows or guesses another caller's `ThreadId` can resume that other caller's persisted thread" unless an `AgentSessionStore` is wrapped with `IsolationKeyScopedAgentSessionStore` (`UseClaimsBasedSessionIsolation`). Prism registers **no** session store (falls back to `NoopAgentSessionStore`, ephemeral), so cross-user resume is not active **today** — but `/prism` has no auth, and the moment a persistent store is added for a multi-user demo without isolation it becomes a session-bleed.
- **Fix:** gate `/prism` behind Entra auth before enabling `AgUiEnabled` anywhere shared; if a session store is ever added, wrap it with `UseClaimsBasedSessionIsolation`; document the "single-user / prototype only" constraint.

### [STK-08] Dead/inaccurate "Responses not on the GA pin" gate — Severity: Low
- **Target:** [AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs) L64-72 — throws `ConfigurationException("Azure:AgentApi", "The Responses client is not enabled on the GA Azure.AI.OpenAI 2.1.0 pin (it lands with the pkg-07 orchestrator)…")`.
- **Reality:** the Responses client is available on the *resolved* base (OpenAI 2.10.0 + Azure.AI.OpenAI 2.1.0), and pkg 07 actually ships the **Chat** path (`AsIChatClient().AsAIAgent(...)`), not Responses — so this branch is dead config with a comment that ties availability to the wrong pin. (Carried from pkg06 STK-09.)
- **Fix:** remove the dead Responses gate or reword it to reflect that the whole app uses Chat Completions.

### [STK-09] SSE has no heartbeat + relies on manual string framing — Severity: Low
- **Target:** [ReconciliationsController.cs](../../../backend/FinancialServices.Api/Controllers/ReconciliationsController.cs) L52-64.
- **Reality:** the framing itself is **correct** — `text/event-stream`, `data: {…}\n\n`, `FlushAsync` per event, `X-Accel-Buffering: no`, and `JsonSerializer.Serialize(payload, payload.GetType(), …)` (runtime type — avoids the "serialize-as-object slices to base props" trap). But there is no `:keep-alive` comment heartbeat during the (5–15s) gap between `scope-confirm` and the first provider card while the first LLM call runs, and no explicit `DisableBuffering()` — fine for Kestrel's default, brittle if response-compression middleware is later added.
- **Fix:** emit an SSE comment heartbeat during long gaps; consider `IHttpResponseBodyFeature.DisableBuffering()` for defense in depth.

### [STK-10] `Microsoft.Agents.AI.Workflows 1.13.0` pulled in transitively though the landmine forbids the .NET workflow path — Severity: Low (informational)
- **Target:** resolved graph (`dotnet list package --include-transitive`).
- **Reality:** `Microsoft.Agents.AI.Workflows 1.13.0` is present transitively via the Agent Framework, but Prism **does not** use it — the single-agent + `AIFunctionFactory.Create` function-tools + `ApprovalRequiredAIFunction` workaround is honored (verified: `PrismAgentOrchestrator.BuildAgent` builds ONE `AIAgent`; no `MapWorkflow` / workflow-streaming anywhere). This confirms the HACKATHON-FINDINGS §3 landmine was respected; the package is just latent surface area.
- **Fix:** none required.

---

## Unverified (needs live confirmation — do not assume true)

- **Runtime behavior of `Azure.AI.OpenAI 2.1.0` on `OpenAI 2.10.0`** — proven to *restore + compile*; not proven at *runtime* against live Foundry from a clean machine. The live-verified runs to date used the same resolved graph, so this is low-risk, but a behavioral binding surprise from the 9-minor gap is possible.
- **Whether `gpt-5.4` is a reasoning model** — if it is, adding any `ChatOptions` (Temperature ≠ 1, MaxOutputTokens) could 400. Safe **only** because neither the narrators nor the orchestrator send `ChatOptions` today (carried from pkg06 STK-08).
- **Exact `@copilotkit/runtime` version that first exposes ALL of `agents:{}` + `ExperimentalEmptyAdapter` + `copilotRuntimeNodeHttpEndpoint`** — confirmed present in current/latest (1.62.2) and across showcases; the *minimum* compatible with a bumped `react-core`/`react-ui` was not pinned. The version-line split with the frontend's 1.4.8 is certain regardless.
- **Which TFM asset `Microsoft.Extensions.Hosting 10.0.1` resolves to under net9.0** — build shows no `NU1701` fallback (so a `net9.0`/compatible asset was chosen cleanly), but the nuspec lib listing was not enumerated.

## Top 3 Won't-Compile / Won't-Run Risks

1. **(STK-04) The deferred sidecar's `new CopilotRuntime({ agents: { prism } })` won't typecheck** against published `@copilotkit/runtime` types (needs `// @ts-ignore`), and the runtime version that has the AG-UI `agents` model is **incompatible with the frontend's pinned `@copilotkit/react-core`/`react-ui` 1.4.8** — the browser↔sidecar hop will not work until the whole CopilotKit surface is bumped in lockstep.
2. **(STK-03) The SSE stream won't *run* to completion on any Foundry fault** — an unguarded narrator exception aborts the "guaranteed-green" transport after `scope-confirm`, denying even the deterministic dossier. Highest-severity *shipped-C#* risk.
3. **(STK-01/STK-02) The version matrix is green-but-fragile** — `OpenAI` floats open-endedly to 2.10.0 under a "2.1.0 GA" pin, and a prerelease AG-UI package silently drags the .NET-10 hosting wave into a net9.0 app; a single upstream servicing bump can break restore or shift the executing base surface.

---

## What's Solid (credit where due)

- **`MapAGUI("/prism", agent)` is not hallucinated** — it matches the real `MapAGUI(this IEndpointRouteBuilder, string pattern, AIAgent aiAgent)` overload **exactly** (source-verified) and compiles clean under `TreatWarningsAsErrors`.
- **The landmine workaround is honored correctly** — ONE AG-UI `AIAgent` with in-process `AIFunctionFactory.Create` tools + `ApprovalRequiredAIFunction` HITL; the Python-only .NET workflow-streaming path is avoided (STK-10 confirms).
- **Both `AsAIAgent` surfaces are real GA** — Chat (`instructions,name`) for narrators and the tools overload (`AsIChatClient().AsAIAgent(..., tools:)`) for the orchestrator; entire API compiles 0-warn.
- **The SSE framing is correct** — `text/event-stream`, `data:\n\n`, per-event flush, `X-Accel-Buffering: no`, runtime-typed serialization, cancellation handled, in-band `error` events (arch-03).
- **The AG-UI ↔ HttpAgent wire contract is coherent** — the C# `/prism` endpoint (`POST RunAgentInput` → SSE `BaseEvent`) is exactly what `@ag-ui/client` `HttpAgent` posts and parses; the sidecar↔C# hop will work.
- **All four deferred npm exports are real and resolvable** — no hallucinated JavaScript API in spec §B.
- **Real-data model names resolve** — all four roles default to `gpt-5.4` (options + `.env.example`); no `DeploymentNotFound` 404 on a fresh clone (pkg06 STK-02 fixed).
- **Fallback discipline** — `AgUiEnabled` default `false`, so the prerelease AG-UI package never gates a bare boot or the test host; the stream's authoritative dossier is the SAME `IReconciliationService.RunAsync` as REST, so acceptance #4 (identical object) holds and P2 numbers are preserved.
