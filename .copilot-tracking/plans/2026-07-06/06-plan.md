# Package 06 — Foundry Agents — Detailed Plan

**Date:** 2026-07-06 · **Package:** `implementationPlan/06-foundry-agents.md` · **Depends on:** 03, 04 (DONE) · **Blocks:** 07

> The LLM layer — thin C# wrappers that **retrieve, narrate, and cite**. They never compute notch math
> or fire flags (P2, that's pkg 05). Package 06 delivers the **4 narrator/retriever wrappers + their DI
> + a post-validation layer + wiring narration into `ReconciliationService`**. The AG-UI orchestrator +
> function tools are **pkg 07** (explicitly out of scope here).

---

## SDK decision (verified — NuGet + MS Learn; compiler-verified in Phase 2)

- **Inline C# agents over GA packages** — NOT portal agents, NOT `Azure.AI.Projects`.
  - `Microsoft.Agents.AI` **1.13.0** (GA) + `Microsoft.Agents.AI.OpenAI` **1.13.0** (GA) +
    `Azure.AI.OpenAI` **2.1.0** (GA, matches `tools/SeedData` pin).
  - `Microsoft.Agents.AI.AzureAI` (1.0.0-preview.*) + `Azure.AI.Projects` (1.0.0-beta.*) are
    **prerelease only** → excluded from a runtime path (P1 robustness). Portal-defined agents do not
    exist on the account.
- **Verified GA surface** (MS Learn `agent-framework/agents/providers/azure-openai` + code samples):
  ```csharp
  AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
      .GetChatClient(deployment)                 // Chat Completions — default (no hosted tools needed)
      .AsAIAgent(instructions: "...", name: "..."); // name optional
  var response = await agent.RunAsync(prompt, cancellationToken: ct); // response.Text (use var — template's AgentRunResponse is stale)
  ```
  Responses variant (`.GetResponsesClient().AsAIAgent(model: deployment, ...)`) is reserved for the
  pkg-07 tool-using orchestrator; `GetResponsesClient()` is **compiler-probed** before any use.
- **Default client = Chat Completions**: the pkg-06 agents use **no hosted tools** (tools are pkg 07),
  need broad compatibility with the single `gpt-5.4` deployment, and Chat Completions is the
  unambiguous, universally-supported surface. A config toggle (`AzureOptions.AgentApi`) leaves the door
  open for Responses in pkg 07.
- **Models** come from `Prism__Models__*` (all `gpt-5.4` in `.env`) — never hardcoded (arch-04).
- **Endpoint** = `Azure__OpenAiEndpoint` = `https://foundry-dataproviders-poc.openai.azure.com/`
  (the dedicated `.openai` host; `services.ai` does not work for the Azure OpenAI SDK).

### Testability seam (P1 — fakes live only in the test project)
- **One SDK touchpoint:** `IAgentTextRunner.RunAsync(deployment, agentName, instructions, prompt, ct)
  → Task<string>`. Production `AzureAgentRunner` is the only class that references the Agent Framework
  SDK. The four agent wrappers depend on `IAgentTextRunner`, so they are fully unit-testable offline
  with a `FakeAgentTextRunner` (test-only). This is a deliberate, documented refinement of the
  per-agent-client skeleton in arch-04/the template (which assumed portal `GetAIAgentAsync("name")`).

---

## Files (all under `backend/FinancialServices.Api/` unless noted)

### Config + packages
1. **`FinancialServices.Api.csproj`** (edit) — add `Azure.AI.OpenAI` 2.1.0, `Microsoft.Agents.AI`
   1.13.0, `Microsoft.Agents.AI.OpenAI` 1.13.0.
2. **`Infrastructure/AzureOptions.cs`** (edit) — add `OpenAiEndpoint` (bind `Azure__OpenAiEndpoint`)
   and `AgentApi` (`"ChatCompletions"` default | `"Responses"`).
3. **`Infrastructure/PrismOptions.cs`** (edit) — add `NarrationEnabled` (default `true`); the feature
   flag that gates the Foundry narration step.

### Agents/ (new folder)
4. **`Agents/IAgentTextRunner.cs`** — the SDK seam interface.
5. **`Agents/AzureAgentRunner.cs`** — production impl. Builds/caches one `AzureOpenAIClient`
   (`AzureOptions.OpenAiEndpoint` + the DI `TokenCredential`); per call creates an `AIAgent` via
   `GetChatClient(deployment).AsAIAgent(instructions, name)` (or Responses when toggled), runs it,
   returns `.Text`. `ActivitySource("FinancialServices.Agents")` span per call (arch-07). Fail-loud
   `ConfigurationException("Azure:OpenAiEndpoint", …)` when the endpoint is unset (P1). Logs
   ids + response length only, never prompt bodies (P6). `CancellationToken` plumbed (P7).
6. **`Agents/NarrationGuard.cs`** — pure static P2 post-validator.
   `Sanitize(string narrative, string groundingText, IReadOnlyList<string> requiredRefs) → string`:
   returns the narrative iff (a) **every** `requiredRef` appears in it AND (b) every numeric/date token
   in it also appears in `groundingText` (narrative numbers ⊆ grounding numbers). Otherwise returns
   `""` — the narrative is dropped and the caller keeps the authoritative deterministic value. This is
   the mechanical guarantee that "the LLM cannot alter a deterministic number."
7. **`Agents/ProviderExplanation.cs`** — small result records:
   `ProviderExplanation(string Text, IReadOnlyList<string> Citations)` and
   `FundamentalsSummary(string Text, IReadOnlyList<string> Citations)`.
8. **`Agents/ProviderExplainerAgent.cs`** — `ExplainAsync(Issuer, ProviderRating, CancellationToken)`.
   Grounds on the already-retrieved `ProviderRating` (letter/notch/as-of + `MethodologyDocId`) — the
   card facts — builds citation-only instructions, calls the runner with `Models.Provider`, sanitizes
   with `requiredRefs = [MethodologyDocId]`. Returns a cited `ProviderExplanation`.
9. **`Agents/FundamentalsAgent.cs`** — `SummarizeAsync(Issuer, FundamentalSnapshot, CancellationToken)`.
   Grounds on the EDGAR/FRED snapshot (filing date + type + any non-null ratios; honestly states when
   figures are absent — P1 no fabrication), cites `edgar:{cik}:{filingType}`. Uses `Models.Fundamentals`.
10. **`Agents/RedFlagNarratorAgent.cs`** — `NarrateAsync(Issuer, RedFlag, CancellationToken) → string`.
    Grounds on the flag's `Rule` + `EvidenceRefs`; **must** cite every `EvidenceRef`; sanitized against
    the Rule text (numbers ⊆ Rule numbers). Uses `Models.RedFlag`.
11. **`Agents/DivergenceNarratorAgent.cs`** —
    `NarrateAsync(Issuer, PairDivergence, BucketAttribution, CancellationToken) → string`. Grounds on
    the bucket's `Notches`/`EvidenceRefs` + pair gap; sanitized. Uses `Models.RedFlag` (both narrators
    map to the `gpt-5-mini` slot per arch-04 — a separate `Divergence` slot would default to the
    undeployed `gpt-5-mini`; reuse keeps the deployment always `gpt-5.4`).

### Services/ (narration orchestration + wiring)
12. **`Services/IDossierNarrator.cs`** + **`Services/DossierNarrator.cs`** —
    `NarrateAsync(Issuer, ReconciliationDossier, CancellationToken) → ReconciliationDossier`. When
    `NarrationEnabled` is false → returns the dossier unchanged. Otherwise narrates each flag
    (`RedFlagNarratorAgent`) and each divergence bucket (`DivergenceNarratorAgent`), returning a new
    dossier (`record with`) with `RedFlag.Narrative` / `BucketAttribution.Explanation` filled.
    Per-item `try/catch` → a single agent failure logs (ids only) and leaves that field empty; it never
    aborts the others.
13. **`Services/ReconciliationService.cs`** (edit) — inject `IDossierNarrator`. After
    `DossierAssembler.Assemble`, wrap `dossier = await narrator.NarrateAsync(entry.Issuer, dossier, ct)`
    in `try/catch`: a narration fault logs a warning and keeps the **deterministic** dossier (P1 —
    fail-loud stays on Search/Cosmos; narration is best-effort). Persist + audit the (possibly narrated)
    dossier. Add `narratedFlagCount` to the audit metadata.

### DI + host
14. **`Infrastructure/ServiceCollectionExtensions.cs`** (edit) — add `AddPrismAgents()` registering
    `IAgentTextRunner→AzureAgentRunner`, the four agents, and `IDossierNarrator→DossierNarrator` as
    **singletons** (stateless; `ReconciliationService` is a singleton → singleton deps avoid a captive
    dependency; documented deviation from arch-04 "scoped").
15. **`Program.cs`** (edit) — call `.AddPrismAgents()` after `.AddPrismDataServices()`.

### Tests (`backend/FinancialServices.Tests/`)
16. **`TestSupport/FakeAgentTextRunner.cs`** — records `(deployment, agentName, instructions, prompt)`;
    returns a configurable canned string (or a per-agent function) so a narrator can be fed a wrong
    number for the honesty test. Test-only (P1).
17. **`Agents/NarrationGuardTests.cs`** — drops a narrative with an invented date/number; keeps a
    compliant one; drops when a required ref is missing.
18. **`Agents/RedFlagNarratorAgentTests.cs`** — NordStar STALE flag: a compliant narration citing both
    refs is returned; **an altered-number narration is dropped → empty (the deterministic Rule number
    survives)** — the acceptance "narrators never alter deterministic numbers" proof.
19. **`Agents/DivergenceNarratorAgentTests.cs`** — bucket `Explanation` filled when valid, dropped when
    it contradicts the deterministic `Notches`.
20. **`Agents/ProviderExplainerAgentTests.cs`** + **`Agents/FundamentalsAgentTests.cs`** — return cited
    text on the happy path; strip an uncited/invented-number response.
21. **`Services/DossierNarratorTests.cs`** — full NordStar dossier: flags + buckets narrated; a throwing
    runner → the deterministic dossier is returned unchanged (graceful fallback).
22. **`Services/ReconciliationServiceTests.cs`** (edit) — update `Build()` for the new ctor arg; add:
    with a fake runner, `RunAsync` returns populated narrative; with a throwing runner, it still
    persists the deterministic dossier.

---

## Acceptance mapping (spec §"Acceptance for this package")
| # | Criterion | Covered by |
|---|---|---|
| 1 | Each provider agent returns a **cited** explanation for NordStar (live Foundry) | `ProviderExplainerAgent` + offline test #20; **live gate** (Phase 5) |
| 2 | `FundamentalsAgent` reports NordStar Q3 figures + the real filing date | `FundamentalsAgent` #9 + test #20 (states filing date/type; figures null for synthetic → honest) |
| 3 | `RedFlagNarratorAgent` narrates STALE_INPUT citing **both** the MSCI card + the EDGAR date | `RedFlagNarratorAgent` #10 + test #18; **live gate** |
| 4 | Narrators never alter deterministic numbers (validation test) | `NarrationGuard` #6 + tests #17/#18/#19 (the P2 proof) |
| 5 | All agents run within TPM quota (chatty ones on cheap model) | single `gpt-5.4` deployment via `Prism__Models__*`; sequential per-item narration; **live gate** |

## Guardrails honored
- **P1** real services + fail-loud (`ConfigurationException` on missing endpoint); fakes only in tests;
  narration failure is an *explicit, logged* best-effort fallback (per user instruction), core stays loud.
- **P2** LLM only narrates/cites; `NarrationGuard` drops any output that alters a deterministic number.
- **P4** no buy/sell/hold/recommend/allocate/trade/alpha/signal in code, prompts, instructions, tests.
- **P6** `DefaultAzureCredential`, no keys; log ids + lengths only, never prompt bodies/PII. No user
  free-text path exists today (issuer notes not implemented) → Content Safety **deferred**, documented.
- **P7** `CancellationToken` plumbed through every agent + narration call.
- **arch-07** `ActivitySource("FinancialServices.Agents")` span per agent call.
- **arch-03** errors via existing `ConfigurationException`/envelope.

## Out of scope (pkg 07)
AG-UI `MapAGUI("/prism")`, function tools, the `ReconciliationOrchestrator`, the CopilotKit sidecar,
Responses-client hosted tools, Content Safety on user free-text.

## Live gate (Phase 5)
Load `.env` + `AZURE_TENANT_ID=ce48da85-…`; exercise a provider agent (cited NordStar explanation) and
the RedFlag narrator (STALE citing both refs) against live Foundry `gpt-5.4`. If Foundry is unreachable,
report it as a live gate — the **build + unit tests must pass regardless**.
