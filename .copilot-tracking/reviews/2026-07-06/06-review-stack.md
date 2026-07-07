# Adversarial Stack-Fit Review: Prism Work Package 06 (Foundry Narration Agents)

**Target:** `backend/FinancialServices.Api/Agents/*`, `Services/DossierNarrator.cs`,
`Orchestration/PrismSweepSteps.cs`, `Infrastructure/PrismOptions.cs`, `FinancialServices.Api.csproj`, `.env.example`
**Reviewer stance:** technology-fit red-team — SDK-surface accuracy, version pinning, interoperability.
**Method:** live NuGet catalog/registration API + Microsoft Learn API reference (fetched 2026-07-07). No praise.

---

## Verification Log

| # | Claim under test | Source (live) | Verdict |
|---|---|---|---|
| V1 | `Microsoft.Agents.AI` **1.13.0** is GA | flat-container index: `1.0.0` GA → … → `1.13.0`, no `-preview/-rc` suffix | **VERIFIED GA** |
| V2 | `Microsoft.Agents.AI.OpenAI` **1.13.0** is GA | catalog entry `isPrerelease:false`, `published 2026-07-03` | **VERIFIED GA** |
| V3 | `Azure.AI.OpenAI` **2.1.0** is GA | catalog entry `isPrerelease:false`, `published 2024-12-06`; newest *stable* of the line (2.2.0+ are all `-beta`) | **VERIFIED GA** |
| V4 | `client.GetChatClient(dep).AsAIAgent(instructions:, name:)` is the correct surface | MS Learn *Azure OpenAI Agents (C#)* + *OpenAI Agents (C#)* show this exact Chat-Completion pattern | **VERIFIED — documented, not coincidence** |
| V5 | `.AsIChatClient()` adapter is required first | Docs show `AsAIAgent` **directly on `ChatClient`** for the no-tools overload | **REFUTED — not needed here** (only the *tools* overload needs `IChatClient`) |
| V6 | `response.Text` is the right accessor | MS Learn: `AgentResponse.Text { get; }` (Microsoft.Agents.AI.Abstractions v1.13.0) — "concatenated text content of all messages", `""` when none | **VERIFIED** |
| V7 | Non-generic `RunAsync` return type | MS Learn: `Task<AgentResponse>` (.NET). Code uses `var` → binds correctly. (`AgentRunResponse` is the **Python** type name.) | **VERIFIED — `var` was the right call** |
| V8 | `Microsoft.Agents.AI.OpenAI` 1.13.0 transitive floors (net9.0) | catalog `dependencyGroups/net9.0`: `OpenAI [2.10.0, )`, `System.ClientModel [1.14.0, )`, `Microsoft.Extensions.AI[.OpenAI] [10.6.0, )`, + .NET-10 BCL wave (`System.Text.Json [10.0.9, )` …). **No dep on `Azure.AI.OpenAI`.** | **VERIFIED** |
| V9 | `Azure.AI.OpenAI` 2.1.0 floors | catalog: `OpenAI [2.1.0, )`, `Azure.Core [1.44.1, )` | **VERIFIED** |

**Net of V4–V7: the SDK *surface* the runner uses is documented, GA, and correct.** The "guessed API / coincidence"
attack fails. The interesting damage is in **version interop (V8×V9)** and **config/runtime wiring**, below.

---

## Findings

### [STK-01] The pinned OpenAI matrix is not coherent — `Azure.AI.OpenAI 2.1.0` is force-run on base `OpenAI 2.10.0` — Severity: **High** (correctness/reproducibility; not a today-crash)
- **Target:** [FinancialServices.Api.csproj](../../../backend/FinancialServices.Api/FinancialServices.Api.csproj) pkg-06 block (Azure.AI.OpenAI `2.1.0` + Microsoft.Agents.AI[.OpenAI] `1.13.0`)
- **Claim under test:** "Azure.AI.OpenAI 2.1.0 ↔ Microsoft.Agents.AI[.OpenAI] 1.13.0 ↔ OpenAI base — a coherent, released matrix."
- **Reality (V8×V9):** `Microsoft.Agents.AI.OpenAI 1.13.0` depends on **`OpenAI [2.10.0, )`** and **`System.ClientModel [1.14.0, )`**; it does **not** reference `Azure.AI.OpenAI` at all. `Azure.AI.OpenAI 2.1.0` (Dec 2024) shipped against **`OpenAI 2.1.0`**. NuGet unifies to the higher floor → the app actually runs on **OpenAI 2.10.0 + System.ClientModel 1.14.0**, i.e. `Azure.AI.OpenAI 2.1.0` executed against a base OpenAI **9 minor versions newer than it was built/tested against**. The csproj comment ("Azure.AI.OpenAI 2.1.0 … matches SeedData") presents 2.1.0 as the effective surface; the real client/serialization/auth code path is OpenAI 2.10.0. Anyone reasoning about behavior/CVEs from the csproj is misled.
- **Why it still runs:** producer (`AzureOpenAIClient.GetChatClient`) and consumer (`AsAIAgent`) both bind the **same unified `OpenAI 2.10.0`** assembly; same major → binary unification holds. Green today.
- **The trap:** there is **no *stable* `Azure.AI.OpenAI` that targets OpenAI 2.10.0** (stable tops out at 2.1.0; 2.2.0-beta.1 … 2.9.0-beta.1 are prerelease). So the "GA-only, no prerelease" stance the csproj brags about is only half-true: the Azure extension is GA but yoked to a base OpenAI it never shipped with, and the *aligned* Azure.AI.OpenAI builds are all prerelease. The Agent-Framework floor `[2.10.0, )` is open-ended → a future `dotnet restore` can silently drag an even newer base OpenAI under the Dec-2024 Azure shim. **That** seam (Azure.AI.OpenAI 2.1.0 ↔ base OpenAI), not `AsAIAgent`, is the real "breaks on a bump" risk.
- **Fix:** pin the base explicitly (`<PackageReference Include="OpenAI" Version="2.10.0" />`) and `<PackageReference Include="System.ClientModel" Version="1.14.0" />` so the effective versions are visible + reproducible; add a comment that Azure.AI.OpenAI 2.1.0 is intentionally run above its shipped base; add a CI `dotnet list package --include-transitive` gate to catch silent base-OpenAI drift.

### [STK-02] `.env.example` + code defaults pin **four non-existent deployments** while the only real model is `gpt-5.4` — Severity: **High**
- **Target:** [.env.example](../../../.env.example#L40-L43) (`Prism__Models__Orchestrator=gpt-5`, `__Provider=gpt-4.1-mini`, `__Fundamentals=gpt-4.1-mini`, `__RedFlag=gpt-5-mini`) and the same defaults in [PrismOptions.cs](../../../backend/FinancialServices.Api/Infrastructure/PrismOptions.cs#L52-L57).
- **Reality:** the account has exactly one deployment: `gpt-5.4` (per project notes). A fresh clone that copies `.env.example` → `.env` (the documented onboarding step) requests deployments `gpt-5` / `gpt-4.1-mini` / `gpt-5-mini` that **do not exist** → Azure returns **404 `DeploymentNotFound`** on the first `agent.RunAsync` for every agent. The example ships a guaranteed-broken narration config.
- **Interlock with STK-03:** the 404 is then swallowed (see below) → the app returns a 200 dossier with **blank narration and no surfaced error** on default config.
- **Fix:** set all four `Prism__Models__*` in `.env.example` to the real deployment (`gpt-5.4`) or a documented placeholder that is validated at boot; change the code defaults to fail closed rather than to other non-existent names.

### [STK-03] `SafeNarrateAsync` swallows **permanent config/auth errors** identically to transient faults — P1 fail-loud violation — Severity: **High**
- **Target:** [DossierNarrator.cs](../../../backend/FinancialServices.Api/Services/DossierNarrator.cs#L60-L76) `catch (Exception ex) { … return string.Empty; }`
- **Reality:** the only distinguished case is `OperationCanceledException` (rethrown). Everything else — `404 DeploymentNotFound`, `401` bad credential, wrong `OpenAiEndpoint`, model-family 400 — is caught and downgraded to an empty narrative with a single `LogWarning`. A completely misconfigured Foundry setup therefore looks "working": the deterministic dossier persists, the API returns 200, and the entire pkg-06 value (LLM narration) silently produces nothing. This is exactly the "No silent fallbacks … fail loudly with a clear, actionable error" rule in `.github/copilot-instructions.md` and P1 — a **permanent** config error is being treated as a **transient** hiccup.
- **Fix:** classify errors — surface config/auth/deployment errors (fail loud once, or a startup/first-use probe that validates each configured deployment exists), keep the best-effort swallow only for genuine transient LLM faults (timeouts, 429, 5xx).

### [STK-04] Sequential 10–12 LLM round-trips per reconciliation on a single `gpt-5.4` deployment — latency + 429 — Severity: **High**
- **Target:** [DossierNarrator.cs](../../../backend/FinancialServices.Api/Services/DossierNarrator.cs#L33-L54) (`foreach` flag, then `foreach` divergence × `foreach` bucket — all `await`ed serially).
- **Reality:** NordStar = 1 flag + 3 pairs × 3 buckets = **10 serial calls**; Onyx (3 flags) = **12**. On a gpt-5-family (reasoning) deployment each call is ~2–4s → **~20–48s wall-clock** per reconciliation, and the browser auto-runs this on issuer-select. All traffic hits **one** `gpt-5.4` deployment, so two concurrent reconciliations (or the AG-UI path + REST path together) stack 20–24 in-flight requests → TPM/RPM **429**. Worse, for the residual-dominated demo cast the `Weighting` and `Input` buckets are ~0 — so **6 of the 9 bucket calls narrate zero-contribution buckets**: pure latency/cost with nothing to say.
- **On "is per-call agent creation wasteful?":** minor. `GetChatClient(dep).AsAIAgent(...)` is a cheap in-memory allocation over the cached `AzureOpenAIClient` — **no network I/O**. The cost is the serial round-trips, not the agent objects. Caching agents is a micro-opt; parallelizing the calls is the real fix.
- **Fix:** bound-parallel the independent narrations (`Task.WhenAll` + a small `SemaphoreSlim` sized to the deployment's RPM), and/or narrate **one call per divergence** (feed all 3 buckets at once → 3 calls not 9), and skip buckets whose `Notches == 0`. Add a per-call timeout (see STK-07).

### [STK-05] Two of the four pkg-06 agents never reach the live dossier — Severity: **Medium**
- **Target:** [DossierNarrator.cs](../../../backend/FinancialServices.Api/Services/DossierNarrator.cs#L16-L17) injects **only** `RedFlagNarratorAgent` + `DivergenceNarratorAgent`. `ProviderExplainerAgent` and `FundamentalsAgent` are consumed **only** by [PrismSweepSteps.cs](../../../backend/FinancialServices.Api/Orchestration/PrismSweepSteps.cs#L72) / [L88](../../../backend/FinancialServices.Api/Orchestration/PrismSweepSteps.cs#L88) — the pkg-07 AG-UI/SSE path.
- **Reality:** the working browser demo hits `POST /api/v1/reconciliations` → `ReconciliationService.RunAsync` → `DossierNarrator`, which narrates flags + divergence buckets only. Moreover the persisted `DossierResponse` verdict shape (`provider, letter, notch, asOfDate, inputAsOfDate, methodologyDocId`) has **no field** to hold a provider explanation or a fundamentals summary — so even the outputs `PrismSweepSteps` produces have nowhere to land in the REST dossier; they exist only as pkg-07 stream events. Acceptance items #1 (provider agent returns a cited explanation) and #2 (fundamentals figures) are satisfied by offline tests + the not-yet-live AG-UI path, **not** by the shipped demo.
- **Fix:** decide the contract — either add `narrative` fields to the verdict DTO and wire `ProviderExplainer`/`Fundamentals` into `DossierNarrator`, or explicitly scope pkg-06 REST narration to flags+divergences and move provider/fundamentals narration to pkg-07 in the plan/acceptance.

### [STK-06] `AgentApi="Responses"` "fail-loud" throw is defeated in the REST path but propagates in the AG-UI path — Severity: **Medium**
- **Target:** [AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs#L61-L70) throws `ConfigurationException` for `Responses`; the comment claims "fails loud (P1 — no silent downgrade)."
- **Reality:** in the REST path that throw is caught by `DossierNarrator.SafeNarrateAsync` (STK-03) → **silently disables all narration**, the opposite of fail-loud. In the AG-UI path, `PrismSweepSteps` has no try/catch → the same throw **propagates (→ 500)**. Same misconfiguration, two contradictory behaviors; the comment describes only the path where it doesn't hold.
- **Fix:** validate `AgentApi` once at startup (options validation) so an unsupported value fails fast and consistently, independent of which path runs first.

### [STK-07] No per-call timeout on `agent.RunAsync` — a hung model stalls the serial sweep — Severity: **Medium**
- **Target:** [AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs#L48) `await agent.RunAsync(prompt, cancellationToken: ct)` — `ct` is only the ambient request-abort token; there is no bounded per-call deadline.
- **Reality:** combined with the serial sweep (STK-04), one slow/hung gpt-5.4 call blocks all remaining narrations up to the ASP.NET request timeout. Mirrors the pkg-04 "no `HttpClient.Timeout`" finding at the LLM layer.
- **Fix:** wrap each call in a linked `CancellationTokenSource` with a per-call timeout (e.g. 15–20s) so a stuck call degrades that one narration, not the whole dossier.

### [STK-08] Latent gpt-5 reasoning-param 400 (safe only because no `ChatOptions` are sent) — Severity: **Low** (Unverified-live)
- **Target:** [AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs#L48) sends **no** `ChatClientAgentRunOptions` (no temperature, no max tokens).
- **Reality:** that is *why* it doesn't 400 today — gpt-5 reasoning deployments on Chat Completions reject non-default `temperature` and require `max_completion_tokens` (not `max_tokens`). The moment anyone adds the natural cost control (`Temperature = 0.2`, `MaxOutputTokens = …`) to trim the unbounded responses, a reasoning `gpt-5.4` can start returning 400. It is a loaded gun, not a current break.
- **Fix:** before adding options, confirm whether `gpt-5.4` is a reasoning deployment; if so, only set `MaxOutputTokens` (maps to `max_completion_tokens`) and leave temperature default.

### [STK-09] csproj comment: "Responses … not on this GA pin" is inaccurate — Severity: **Low**
- **Target:** [AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs#L64-L66) / csproj comment.
- **Reality:** with the base OpenAI unified to **2.10.0** (STK-01), the Responses client is in fact available; deferring it to pkg-07 is a *choice*, not a capability gap. The comment states a technical impossibility that isn't one.
- **Fix:** reword to "Responses deferred to pkg-07 by design," not "not on this pin."

### [STK-10] Micro-nits — Severity: **Low**
- `response.Text ?? string.Empty` ([AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs#L49)) is dead-defensive: `AgentResponse.Text` is a **non-nullable** `string`, doc-guaranteed to be `""` when empty (V6). Harmless, but signals uncertainty about the surface.
- The `_client` double-checked lock ([AzureAgentRunner.cs](../../../backend/FinancialServices.Api/Agents/AzureAgentRunner.cs#L28)) reads a **non-`volatile`** field outside the lock — the standard-but-technically-unguaranteed DCL under the ECMA memory model. Prefer `Lazy<AzureOpenAIClient>(LazyThreadSafetyMode.ExecutionAndPublication)`.

---

## Unverified (needs live confirmation — not asserted true)
- **Is `gpt-5.4` a reasoning deployment?** Drives both the per-call latency in STK-04 and the 400 risk in STK-08. Could not verify from the repo.
- **Exact restored base `OpenAI` version.** I proved the *floor* is `2.10.0` (V8); a `dotnet list package --include-transitive` would confirm the resolved version and whether anything pins higher.
- **The git-ignored real `.env`.** Project notes say all four `Prism__Models__*` are overridden to `gpt-5.4`; I could not read `.env`. If **any** of the four is left at the `.env.example` default, that one agent 404s silently (STK-02/03). The live "it works" claim only holds if all four are correctly overridden.

---

## Top 3 Won't-Run / Won't-Behave Risks
1. **Fresh clone on `.env.example` → every narration 404s (`DeploymentNotFound`) and is silently swallowed** → zero narration, HTTP 200, no error surfaced (STK-02 × STK-03).
2. **Auto-run + any concurrency on the single `gpt-5.4` deployment → 429 and ~20–48s latency** from 10–12 serial reasoning calls (STK-04).
3. **Base-OpenAI unification is untested:** `Azure.AI.OpenAI 2.1.0` runs on `OpenAI 2.10.0`/`System.ClientModel 1.14.0` via an open-ended `[2.10.0, )` floor with no GA Azure.AI.OpenAI aligned to it — the real bump-fragility seam (STK-01).

---

## GO / NO-GO

| Dimension | Verdict |
|---|---|
| **SDK surface correctness** (`GetChatClient().AsAIAgent(instructions:, name:)`, `response.Text`, `RunAsync`) | **GO** — documented, GA, verified (V4–V7). The core attack is refuted. |
| **Version interop / pinning** | **CONDITIONAL (amber)** — compiles + runs, but the matrix is misleading (effective base = OpenAI 2.10.0, not the pinned 2.1.0) and **cannot be made GA-coherent**. Acceptable for a hackathon; not a reproducible product pin. Fix via STK-01. |
| **Real-data narration reaching the demo** | **NO-GO as shipped on default config** — `.env.example` points at non-existent models and failures are swallowed (STK-02/03). **With a correct `.env` (all `gpt-5.4`)**: flag + divergence narration works live; provider + fundamentals narration **structurally never reach the REST dossier** (STK-05). |

**Bottom line:** the deterministic money-moment is unaffected (it is not LLM-dependent), and the Agent-Framework *surface* is right. The blockers are **config honesty + fail-loud** (STK-02/03), **throughput** (STK-04), and a **misleading version pin** (STK-01) — none a compile break, all real stack-fit defects. Close STK-02/03 before any demo on a clean machine; close STK-01 before calling the pin reproducible.
