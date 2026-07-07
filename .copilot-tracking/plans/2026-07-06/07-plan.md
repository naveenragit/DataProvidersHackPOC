# Package 07 — Orchestration, AG-UI & CopilotKit — Implementation Plan

Date: 2026-07-06 · Package: `implementationPlan/07-orchestration-agui-and-copilotkit.md`
Depends on: 05 (deterministic core) + 06 (Foundry narrators) — both DONE. Blocks: 10 (already built with placeholders).

## Verified technical facts (Phase 0)

- **AG-UI hosting package RESOLVES**: `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` `1.13.0-preview.260703.1`
  (prerelease only; version line matches pinned GA core `Microsoft.Agents.AI` 1.13.0). Confirmed via the
  NuGet flat-container index (the azuresearch `packageid:` query hides date-stamped previews). Restored
  clean, no downgrade/conflict with `Azure.AI.OpenAI` 2.1.0.
- **Surface compiler-verified** under `TreatWarningsAsErrors`:
  - `builder.Services.AddAGUI();` + `app.MapAGUI("/prism", agent);`
  - tools: `AIFunctionFactory.Create(delegate)` (`[Description]` on method + params).
  - HITL: `new ApprovalRequiredAIFunction(AIFunctionFactory.Create(fn))` — emits **MEAI001** (evaluation-only)
    → needs `#pragma warning disable MEAI001`.
  - agent+tools: `client.GetChatClient(dep).AsIChatClient().AsAIAgent(instructions:, name:, tools: AITool[])`
    (the tools overload is on `IChatClient`, not OpenAI `ChatClient`).
- **DECISION**: ship BOTH, gate on the fallback.
  - **Live AG-UI path** (spec §A–C): one AG-UI agent + in-process function tools + `confirmScope` HITL,
    `MapAGUI("/prism")`, **config-gated** (`Prism:AgUiEnabled`). Compiler-verified; smoke-attempted live.
  - **Guaranteed-green streaming fallback** (spec §D): `POST /api/v1/reconciliations/stream` (SSE) runs the
    SAME deterministic sweep, emits ordered progress events, client-confirm scope gate. A real transport
    (P1-ok). Live-verifiable via curl + browser. Existing `POST /api/v1/reconciliations` remains the atomic
    REST fallback (acceptance item 4, already green).

## Core-principle guardrails

- **P2**: ordering + notch/gap/attribution/flag math come from the deterministic `Analysis/` engines
  (via `DossierAssembler` / `IReconciliationService`). The LLM orchestrates + narrates; it never recomputes
  a number. The streamed final dossier is produced by the SAME `ReconciliationService.RunAsync` as REST →
  identical object (acceptance item 4).
- **P4**: no buy/sell/hold/recommend/allocate/trade/alpha/signal in tool names, instructions, events, UI, code.
  Tool names: `confirmScope`, `retrieveProviderRating`, `groundFundamentals`, `decomposeDivergence`,
  `evaluateRedFlags`, `assembleDossier`.
- **P6**: `DefaultAzureCredential` (no keys); AG-UI tool args (issuerId/asOf/provider) treated as hostile and
  re-authorized server-side (validated against the corpus, asOf clamped ≤ now); log ids + counts only. Sidecar
  authenticates `/copilotkit` and forwards the end-user bearer to the C# API.
- **P7**: `CancellationToken` through every call.
- **arch-03** error envelope; **arch-07** span per tool/step; **arch-09** DTOs; **arch-10** frontend state via hooks.

## Backend files

1. `FinancialServices.Api.csproj` — (DONE) add `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` prerelease.
2. `Infrastructure/PrismOptions.cs` — add `bool AgUiEnabled { get; init; } = false;` (opt-in; default off keeps the
   fallback the default demo path and unit-test host clean).
3. `Orchestration/PrismStreamEvents.cs` — SSE event envelope + typed payloads: `scope-confirm`,
   `provider-rating`, `fundamentals`, `divergence`, `red-flag`, `dossier-ready`, `error`. camelCase via PrismJson.
4. `Orchestration/PrismSweepSteps.cs` — shared per-step logic reused by SSE + AG-UI (DRY, P8):
   `RetrieveProviderRatingAsync(issuerId, provider)` → corpus rating + `ProviderExplainerAgent` narrative;
   `GroundFundamentalsAsync(issuerId, asOf)` → corpus filing + `FundamentalsAgent`; `NormalizeIssuerId` (P6);
   `ResolveIssuerAsync` (throws NotFound). Deterministic decompose/red-flags/assemble delegated to
   `IReconciliationService.RunAsync` (authoritative + persist).
5. `Orchestration/PrismStreamingOrchestrator.cs` — ordered sweep with an event callback
   `Func<PrismStreamEvent, CancellationToken, Task>`. Gate: if not confirmed → emit `scope-confirm`(approved:false)
   + stop. Else scope-confirm(approved) → per-provider `provider-rating` → `fundamentals` → call
   `ReconciliationService.RunAsync` (authoritative dossier + persist) → emit `divergence`/`red-flag` from it →
   `dossier-ready` (full `DossierResponse`). Span per step (arch-07).
6. `Orchestration/PrismAgentOrchestrator.cs` — the ONE AG-UI `AIAgent`: 5 tools via `AIFunctionFactory.Create`
   (`retrieveProviderRating`, `groundFundamentals`, `decomposeDivergence`, `evaluateRedFlags`, `assembleDossier`)
   + `confirmScope` as `ApprovalRequiredAIFunction` (`#pragma warning disable MEAI001`). Orchestrator instructions:
   parse issuer + as-of → `confirmScope` (wait) → fan out retrieve/ground → decompose → evaluate → assemble+persist.
   Built via `AsIChatClient().AsAIAgent(instructions, name, tools)`; wrapped with the MS-Learn approval middleware
   so `confirmScope` surfaces as a `request_approval` client tool call. Exposes `.Agent` for `MapAGUI`. P4-clean instructions.
7. `Controllers/ReconciliationsController.cs` — add `[HttpPost("stream")]` writing `text/event-stream`; delegates
   to `PrismStreamingOrchestrator`. `[AllowAnonymous]` (demo) but args re-authorized in the orchestrator.
8. `Infrastructure/ServiceCollectionExtensions.cs` — `AddPrismOrchestration()`: register `PrismSweepSteps`,
   `PrismStreamingOrchestrator`, `PrismAgentOrchestrator` (singletons, stateless); call `services.AddAGUI()`.
9. `Program.cs` — `builder.Services.AddPrismOrchestration();` + gated
   `if (options.AgUiEnabled) app.MapAGUI("/prism", sp => sp.GetRequiredService<PrismAgentOrchestrator>().Agent);`
   (resolve options from a built provider).
10. `.env.example` — add `Prism__AgUiEnabled=false` note.

## Backend tests (offline, fakes only — P1)

- `Orchestration/PrismStreamingOrchestratorTests.cs` — event ordering; gate stops when not confirmed; deterministic
  numbers identical on repeat; final `dossier-ready` equals REST dossier. Reuse existing fakes
  (FakeAgentTextRunner, fake corpus/store) from `TestSupport/`.
- `Orchestration/PrismSweepStepsTests.cs` — `NormalizeIssuerId`, unknown issuer → NotFound, provider retrieval,
  P4 vocabulary scan on instructions/tool descriptions.

## Node sidecar `copilot-runtime/` (repo root — new)

- `server.ts` — AG-UI primary: `CopilotRuntime({ agents: { prism: new HttpAgent({ url: PRISM_AGUI_URL }) } })` +
  `ExperimentalEmptyAdapter` + `copilotRuntimeNodeHttpEndpoint('/copilotkit')` on `:4000`. Security: authenticate
  `/copilotkit`, forward end-user bearer via `HttpAgent` headers to the C# `/prism`.
- `server.openai-fallback.ts` — the committed OpenAIAdapter + actions variant (spec §B / pkg 00 fallback path).
- `package.json` — `@copilotkit/runtime` (match frontend react-* major = 1.4.x) + `@ag-ui/client` + `express`,
  dev `tsx` + `typescript`. Scripts: `dev`, `start`, `build` (`tsc`).
- `tsconfig.json` — NodeNext, strict, `noEmit` for the build check.
- `.env.example` — `PORT`, `API_BASE_URL`, `PRISM_AGUI_URL`, `COPILOT_REQUIRE_AUTH`.

## Frontend `frontend/src/`

- `types/prism.ts` — add `PrismStreamEvent` union (scope-confirm/provider-rating/fundamentals/divergence/red-flag/
  dossier-ready/error) mirroring the backend event payloads.
- `lib/reconciliationStream.ts` — fetch POST `/api/v1/reconciliations/stream` + ReadableStream SSE parser → async
  iterator of typed events. Fail-loud (P1).
- `hooks/useReconciliationStream.ts` — hook: `{ events, status, dossier, error, start(confirmed), reset }`.
- `components/prism/ScopeConfirmGate.tsx` — interactive amber gate (Approve / Decline) → P5 gate; run starts only on approve.
- `components/prism/EvidenceStream.tsx` — renders streamed events in order as cards (reuse ProviderVerdictCard,
  fundamentals note, DecompositionWaterfall, red-flag) — the live "evidence stream".
- `components/prism/PrismCopilotBridge.tsx` — `useCoAgent({name:'prism'})` + `useCopilotAction({name, render})` per
  tool + `renderAndWaitForResponse` on `confirmScope`. Mounted ONLY inside the CopilotKit provider branch.
- `pages/ReconciliationPage.tsx` — streaming-first: ScopeConfirmGate → EvidenceStream → full board on
  `dossier-ready` (reuse RedFlagBanner/DivergenceBoard/DecompositionWaterfall/RedFlagPanel/DossierPanel). Keep a
  "Run without streaming" REST fallback (`useReconciliationRun`).
- `App.tsx` — render `<PrismCopilotBridge/>` inside the CopilotKit branch (gated on VITE_COPILOT_URL).
- `.env` / `.env.example` — `VITE_COPILOT_URL=/copilotkit` (re-enables CopilotKit).
- Tests: `useReconciliationStream` parse + `EvidenceStream` render smoke (vitest). Keep build+test green.

## Root

- `run-copilot-runtime.bat` — `npm install` (if needed) + start the sidecar on :4000.

## Acceptance mapping

| Acceptance | Delivered by |
|---|---|
| `/prism` streams gate → provider → fundamentals → waterfall → red-flag (NordStar) | Live AG-UI (attempt) **and** SSE `EvidenceStream` (verified) |
| `confirmScope` pauses until user approves | AG-UI `ApprovalRequiredAIFunction` + `renderAndWaitForResponse`; SSE `ScopeConfirmGate` (run starts only on approve) |
| deterministic tools identical on repeat | `Analysis/` engines via `ReconciliationService` (P2) — unit-tested |
| fallback REST produces the same dossier | `POST /api/v1/reconciliations` (08) — the streamed dossier IS this code path |

## Build gates (must stay green)

- `dotnet build backend/FinancialServices.slnx` + `dotnet test` (0 warn).
- `npm run build` + `npm run test` in `frontend/`.
- Sidecar `npm run build` (tsc).
