# Pkg 06 — Foundry Narration Agents — Standards Adversarial Review

**Mode:** Prism Standards Adversary (conformance red-team — assume non-conformance until proven).
**Scope:** `backend/FinancialServices.Api/Agents/*` + `Services/DossierNarrator.cs` +
`Services/IDossierNarrator.cs` + narration wiring in `Services/ReconciliationService.cs` +
`Infrastructure/ServiceCollectionExtensions.cs` (`AddPrismAgents`) + `Program.cs`.
**Anchors:** `architecturalPlan/00-core-principles.md` (P1–P8), arch 01/02/03/04/05/07/11,
`implementationPlan/06-foundry-agents.md` Acceptance.
**Reviewed:** 2026-07-07. Build/tests reported green (171) — this review is about **conformance**, not compilation.

---

## Verdict: **FAIL vs package acceptance** (2 of 5 acceptance items met) — 1 Critical, 5 High

The deterministic-honesty *spine* is genuinely solid: `DossierNarrator` fills **only**
`Narrative`/`Explanation` and preserves every deterministic field + list order, so the LLM **cannot**
change a displayed notch/gap/flag value or ordering (the one thing that would be Critical if broken).
The failures are elsewhere: **no P4 enforcement on model output**, **acceptance items 1/2/5 unmet**,
**silent config degradation**, and **near-zero test coverage of the four agents + the narrator**.

---

## Findings (severity-tagged, file-anchored)

### CRITICAL

**[C1] P4 has ZERO mechanical enforcement on model output — the guard checks numbers + refs, never vocabulary.**
`NarrationGuard.Sanitize` ([Agents/NarrationGuard.cs](../../../backend/FinancialServices.Api/Agents/NarrationGuard.cs#L30-L63))
enforces (1) every required `EvidenceRef` is cited and (2) narration digit-tokens ⊆ grounding
digit-tokens. It does **not** scan for `buy/sell/hold/recommend/allocate/trade/alpha/signal`. The four
agent instructions forbid those words (e.g.
[Agents/ProviderExplainerAgent.cs](../../../backend/FinancialServices.Api/Agents/ProviderExplainerAgent.cs#L23-L30)),
but an instruction is not enforcement. The narrated strings become **UI copy** (`RedFlag.Narrative`,
`BucketAttribution.Explanation`), and P4 explicitly governs "code comments, prompts, **UI copy**, and
the pitch." A single noncompliant/jailbroken completion containing a forbidden term reaches the dossier
unchecked. **Why it fails:** the guard is the project's stated "keep the LLM honest" backstop yet omits
the one non-negotiable principle. **Fix:** add a P4 denylist to `NarrationGuard` (case-insensitive word
boundaries) that drops any narration containing a forbidden token; unit-test it. *(Latent, not a demonstrated
emission — the deterministic side and instructions are P4-clean — but there is no backstop, so it breaks
under the first noncompliant completion.)*

### HIGH

**[H1] P2/P3 — the numeric guard is digit-only; word-numbers and semantic inversions bypass it, and SEVERITY/direction are never validated.**
`NumericToken` = `\d+(?:[.,:/\-]\d+)*`
([NarrationGuard.cs](../../../backend/FinancialServices.Api/Agents/NarrationGuard.cs#L14-L16)). Therefore:
(a) **word-numbers** — "predates the filing by about **two months**" adds a claim with no digit token →
accepted; (b) **semantic inversion** — for `STALE_INPUT`, grounding holds both dates, so "the latest
filing dated 2025-09-15 **postdates** the rating action on 2025-11-05" swaps which date is which, cites
both refs, uses only grounding digits → accepted while asserting the opposite of the deterministic rule;
(c) **severity** ("high") is a word, not a digit — a narration that downgrades or omits it is not caught.
The guard validates numbers and refs, **not meaning, direction, or severity**. Mitigated only by P2's
separate display of the verbatim `Rule` beside the prose. **Fix:** constrain narration length/shape, add a
severity-token presence check, and prefer templated narration for flags over free generation.

**[H2] P1 — with `NarrationEnabled=true` (default) and `Azure:OpenAiEndpoint` unset, the config error is swallowed → silent deterministic-only degradation.**
`AzureAgentRunner.GetClient()` throws `ConfigurationException("Azure:OpenAiEndpoint", …)` at first use
([AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs#L86-L92)), but that
exception is caught by `DossierNarrator.SafeNarrateAsync` (catch `Exception` → `string.Empty`,
[DossierNarrator.cs](../../../backend/FinancialServices.Api/Services/DossierNarrator.cs#L64-L76)) **and** again
by `ReconciliationService.RunAsync` (catch `Exception` → `LogWarning`,
[ReconciliationService.cs](../../../backend/FinancialServices.Api/Services/ReconciliationService.cs#L53-L62)).
`AzureOptions.OpenAiEndpoint` is not gated by `ValidateOnStart`
([AzureOptions.cs](../../../backend/FinancialServices.Api/Infrastructure/AzureOptions.cs#L20-L27)). Net effect:
narration is on by default, yet a misconfigured deploy produces deterministic-only dossiers **forever**
with only a per-run warning and no health/operator signal. This is the exact "silent fallback" P1 forbids
("throw a clear error that names the missing setting; missing required config fails at **startup**"). The
"never fake" half of P1 *is* honored (deterministic dossier is authoritative). **Fix:** when
`NarrationEnabled=true`, `ValidateOnStart` that `OpenAiEndpoint` is set (fail loud at boot), or surface a
degraded-readiness signal; do not let `ConfigurationException` be swallowed as best-effort.

**[H3] Acceptance items 1 & 2 unmet — `ProviderExplainerAgent` + `FundamentalsAgent` are NOT in the shipped REST path, and the synthetic cast has no figures.**
`DossierNarrator` injects only `RedFlagNarratorAgent` + `DivergenceNarratorAgent`
([DossierNarrator.cs](../../../backend/FinancialServices.Api/Services/DossierNarrator.cs#L15-L20)). The provider
and fundamentals agents are consumed **only** by `PrismSweepSteps`
([Orchestration/PrismSweepSteps.cs](../../../backend/FinancialServices.Api/Orchestration/PrismSweepSteps.cs#L20-L21)),
i.e. the pkg-07 AG-UI/SSE path — and AG-UI is gated on `AgUiEnabled` (default **false**,
[Program.cs](../../../backend/FinancialServices.Api/Program.cs#L78-L82)). `ReconciliationDossier` has no field
to carry a provider explanation or fundamentals summary
([Models/PrismModels.cs](../../../backend/FinancialServices.Api/Models/PrismModels.cs#L54-L64)). Separately,
`ReconciliationService` builds the snapshot with `DebtToEbitda/InterestCoverage/CashAndEquivalents = null`
([ReconciliationService.cs](../../../backend/FinancialServices.Api/Services/ReconciliationService.cs#L42-L45)),
and `GroundFundamentalsAsync` does the same
([PrismSweepSteps.cs](../../../backend/FinancialServices.Api/Orchestration/PrismSweepSteps.cs#L83-L92)) → the
agent honestly renders "unavailable" (P1-good) but reports **no Q3 figures**. **Why it fails:** acceptance 1
("each provider agent returns a cited explanation for NordStar") and 2 ("FundamentalsAgent reports NordStar
Q3 figures with the real filing date") are unreachable in the default/always-on path and untested; item 2's
"figures" do not exist. **Fix:** either wire the two agents into a surfaced output + test it, or amend the
acceptance to scope them to pkg-07.

**[H4] arch-03 + Acceptance item 5 — no 429 backoff/`RateLimitException`, and chatty agents are not on `gpt-4.1-mini`.**
`AzureAgentRunner.RunAsync` calls `agent.RunAsync` with no retry/backoff-with-jitter and no mapping of a
429 to `RateLimitException`
([AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs#L50-L51)); arch-03
requires "429 → exponential backoff **with jitter**, bounded retries; then `RateLimitException`." Raw SDK
faults propagate and are swallowed as best-effort (H2). Model slots come from config
([ProviderExplainerAgent.cs](../../../backend/FinancialServices.Api/Agents/ProviderExplainerAgent.cs#L44),
[FundamentalsAgent.cs](../../../backend/FinancialServices.Api/Agents/FundamentalsAgent.cs#L41)); in the live
environment only `gpt-5.4` is deployed, so `Prism__Models__*` are all `gpt-5.4` — the "chatty ones on
`gpt-4.1-mini`" TPM safeguard is not in force. **Why it fails:** acceptance 5 is unmet as worded and the
required backoff is absent. **Fix:** add bounded jittered retry + `RateLimitException` in the runner; deploy
`gpt-4.1-mini`/`gpt-5-mini` or accept the deviation explicitly.

**[H5] arch-11 — the four agents + `DossierNarrator` have ZERO tests; the `IAgentTextRunner` seam built "for testability" is unused; the P2 field-preservation property is untested.**
`FinancialServices.Tests/Agents/` contains only `NarrationGuardTests.cs`; there is **no**
`FakeAgentTextRunner` in `TestSupport/` (only `PassthroughDossierNarrator` that bypasses the real narrator,
[TestSupport/PassthroughDossierNarrator.cs](../../../backend/FinancialServices.Tests/TestSupport/PassthroughDossierNarrator.cs#L11-L15)).
So nothing proves: `ProviderExplainerAgent`/`FundamentalsAgent` build correct grounding + pass the right
`requiredRefs`; `FundamentalsAgent` renders "unavailable" for null figures; `RedFlagNarratorAgent` forwards
`flag.EvidenceRefs`; and — most importantly — that `DossierNarrator` **only** writes
`Narrative`/`Explanation`, preserves every deterministic field, swallows a narrator fault, honors
`NarrationEnabled=false`, and rethrows on cancellation. arch-11 targets **≥80% on agents**; effective agent
coverage here is the pure `NarrationGuard` function only. **Fix:** add a `FakeAgentTextRunner` (tampering,
empty, throwing, cancelling) and cover the four agents + `DossierNarrator`, asserting deterministic-field
identity.

### MEDIUM

**[M1] P3 / spec §C — the demo-leading `MethodologyAdjustment` bucket carries no `EvidenceRefs`, so its narration is provenance-free (guard citation check is vacuous).**
The residual bucket's refs come from `RefsFor(a, b, "overlay")` = factors present in exactly one provider
([Analysis/DivergenceDecomposer.cs](../../../backend/FinancialServices.Api/Analysis/DivergenceDecomposer.cs#L54)
+ `AddOverlayRefs`, L167). The demo cast is letter-only with **empty `Factors`** (pkg-05), so overlay refs =
**empty** for the very bucket that carries ~100% of the residual-dominated gap. `DivergenceNarratorAgent`
then passes `bucket.EvidenceRefs` (empty) as `requiredRefs`
([DivergenceNarratorAgent.cs](../../../backend/FinancialServices.Api/Agents/DivergenceNarratorAgent.cs#L40-L45)),
and `NarrationGuard`'s ref loop over an empty list is a no-op → the narrated `Explanation` needs **no**
citation. Spec §C: "Fills each bucket `Explanation` with a citation." **Fix:** when a bucket has no
`EvidenceRefs`, leave `Explanation` empty (no ungrounded prose) or attach the two methodology doc ids as the
required refs.

**[M2] arch-07 — no OpenTelemetry provider is registered anywhere; the agent `ActivitySource` has no listener, so no agent spans are emitted.**
`AzureAgentRunner` declares `ActivitySource("FinancialServices.Agents")` and calls `StartActivity("Agent.Run")`
([AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs#L26,L38)), but
`Program.cs`/`appsettings.json` contain **no** `AddOpenTelemetry`/`AddSource`/`UseAzureMonitor`
(grep across `backend/**` finds only a *comment* in `ConnectorTelemetry.cs`). With no registered listener,
`StartActivity` returns null → the span is a no-op and nothing is exported. arch-07: "turn on day 1… every
agent call gets a span." **Fix:** wire `AddOpenTelemetry().WithTracing(t => t.AddSource("FinancialServices.Agents")…)`
(+ `UseAzureMonitor` when the connection string is present).

**[M3] arch-03 — `ConfigurationException` is defined to "fail at startup (ValidateOnStart)" but here is thrown at first use and then caught.**
The runner's missing-endpoint / `AgentApi=Responses` paths throw `ConfigurationException`
([AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs#L61-L69,L86-L92)); the
arch-03 taxonomy maps this class to a **boot** failure, not a swallowed runtime warning. Overlaps H2/H3.
**Fix:** as H2 — gate at startup when narration/AG-UI is enabled.

### LOW

**[L1] arch-07 span naming.** The only span is generic `"Agent.Run"` with `prism.agent` as a tag
([AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs#L38-L39)), not the
`"{Agent}.{Method}"` arch-07/arch-04 prescribe; the guard accept/drop outcome is not recorded on the span,
and `DossierNarrator` opens no batch span.

**[L2] arch-01/02 — `AgentResults.cs` holds two records; file name ≠ type name.**
`ProviderExplanation` + `FundamentalsSummary` share
[Agents/AgentResults.cs](../../../backend/FinancialServices.Api/Agents/AgentResults.cs#L1-L16); arch-01 says
"one primary type per file; file name = type name," and result records would conventionally live under
`Models/`. (Repo precedent groups records in `PrismModels.cs`, so minor.)

**[L3] arch-02/P2 — a pure deterministic validator lives in `Agents/`.**
`NarrationGuard` is no-I/O, no-LLM, deterministic; the golden-split rule places deterministic logic in
`Analysis/`. Defensible (it is the agent-honesty contract) but it blurs the physical P2 separation.

**[L4] arch-04 — agents/runner/narrator registered `Singleton`, not `Scoped`.**
[ServiceCollectionExtensions.cs](../../../backend/FinancialServices.Api/Infrastructure/ServiceCollectionExtensions.cs#L150-L157)
registers all as singletons. Documented, stateless, avoids a captive dependency under the singleton
`ReconciliationService` — acceptable, but a stated deviation from arch-04 "scoped."

**[L5] arch-04/05 — `DivergenceNarratorAgent` reuses the `RedFlag` model slot.**
It reads `options.Value.Models.RedFlag`
([DivergenceNarratorAgent.cs](../../../backend/FinancialServices.Api/Agents/DivergenceNarratorAgent.cs#L43))
rather than a dedicated `Divergence` slot. Matches arch-04's `gpt-5-mini` mapping but couples two agents'
model selection.

---

## Conformant (attacked, held) — one line each

- **P2 (core) SOLID:** `DossierNarrator` writes only `Narrative`/`Explanation` via `with` and rebuilds lists
  in order → no deterministic value or ordering can change
  ([DossierNarrator.cs](../../../backend/FinancialServices.Api/Services/DossierNarrator.cs#L34-L56)).
- **P6:** `DefaultAzureCredential` via shared `TokenCredential`, no keys; logs ids + char-lengths only, never
  prompt/output ([AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs#L44-L53)).
- **P7:** `CancellationToken` plumbed through every call; genuine cancellation rethrown, not swallowed
  ([DossierNarrator.cs](../../../backend/FinancialServices.Api/Services/DossierNarrator.cs#L67-L70)).
- **P4 (deterministic side):** all four instructions are P4-worded and framed "data-quality tool, not
  investment tool."
- **Item 4 (digit level):** `NarrationGuardTests` proves altered-date, invented-number, and missing-ref
  narrations are dropped; thousands-separator equivalence handled.

---

## Acceptance matrix (`implementationPlan/06-foundry-agents.md`)

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Each provider agent returns a cited explanation for NordStar (live Foundry) | **FAIL** | Wired only in pkg-07 `PrismSweepSteps` (AG-UI gated off by default); absent from REST dossier; no test — H3 |
| 2 | `FundamentalsAgent` reports NordStar Q3 figures + real filing date | **FAIL** | Snapshot ratios `null` → "unavailable"; only the date; pkg-07-only; no test — H3 |
| 3 | `RedFlagNarratorAgent` narrates `STALE_INPUT` citing MSCI card + EDGAR date | **PASS** | Wired via `DossierNarrator`; guard-tested + live-claimed |
| 4 | Narrators never alter deterministic numbers (validation test) | **PASS (conditional)** | Digit-level proven by `NarrationGuardTests`; word-number / inversion / severity gaps — H1 |
| 5 | All agents within TPM quota (chatty on `gpt-4.1-mini`) | **FAIL** | All on `gpt-5.4`; no 429 backoff/`RateLimitException` — H4 |

**Overall: FAIL vs acceptance (2/5 met).** The money-moment honesty spine (items 3–4 + `DossierNarrator`
field-preservation) is real and well-guarded; conformance fails on P4 output enforcement (C1), unshipped/
unfigured provider+fundamentals agents (H3), missing quota handling (H4), silent config degradation (H2),
and thin agent/narrator tests (H5).

### Fastest path to PASS-conditional
1. C1 — add P4 denylist to `NarrationGuard` + test.
2. H5 — `FakeAgentTextRunner` + tests for the 4 agents and `DossierNarrator` (assert deterministic-field identity).
3. H2/M3 — fail loud at boot when `NarrationEnabled`/`AgUiEnabled` and `OpenAiEndpoint` is unset.
4. H3 — either surface + test provider/fundamentals output, or re-scope acceptance 1/2 to pkg-07.
5. H4 — jittered 429 retry + `RateLimitException`; resolve the `gpt-4.1-mini` deployment gap.
6. M2 — register the OTel tracer with `AddSource("FinancialServices.Agents")`.
