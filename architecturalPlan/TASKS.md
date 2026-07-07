# TASKS — Prism Architecture & Build Tracker

Single place to track progress. Check items off as you complete them. `[~]` = in progress,
`[x]` = done, `[ ]` = not started, `[!]` = blocked. Add initials/owner in the Owner column.

Legend: **Arch** = follows a rule in `architecturalPlan/`; **Impl** = a work package in
`implementationPlan/`.

---

## Phase 0 — Foundations & governance (Day 0)

| ✔ | Task | Ref | Owner |
|---|---|---|---|
| [ ] | Team read `architecturalPlan/00-core-principles.md`; pin say/never-say list | Arch 00 | |
| [ ] | Register GPT-5 access (aka.ms/openai/gpt-5); confirm GPT-4.1 fallback | Impl 01 | |
| [ ] | `az login` for all; assign `Foundry User` + `Cognitive Services OpenAI User` RBAC | Impl 01 / Arch 06 | |
| [ ] | Pick region (Responses API + File Search); provision Foundry, AI Search Basic, Cosmos free tier, Azure OpenAI | Impl 01 / Arch 08 | |
| [ ] | Request TPM quota bump | Impl 01 | |
| [~] | Scaffold repo from `templates/` — **backend done** (sln + API + tests, builds green); frontend/copilot-runtime deferred to pkg 09/07; `run-backend.bat` done | Impl 01 / Arch 02 | orch |
| [x] | Create `.env.example` with `Azure__*` + `Prism__*`; `.gitignore` covers secrets | Arch 05 | orch |
| [ ] | Pin prerelease AG-UI NuGet versions | Impl 07 / Arch 04 | |
| [ ] | Smoke test: `/api/health` 200; frontend shell loads; `PingAgent` returns from live Foundry | Impl 01 / P1 | |

## Phase 1 — Deterministic foundation & data (Day 1)

| ✔ | Task | Ref | Owner |
|---|---|---|---|
| [x] | Domain records `PrismModels.cs` (8 sealed records; DTOs deferred to pkg 08/09) | Impl 02 / Arch 01,02,09 | orch |
| [x] | `NotchLadder` + unit tests (DBRS/Moody's aliases, IG/HY boundary) — 65 tests green | Impl 02 / Arch 11 | orch |
| [ ] | Author synthetic corpus: issuer cast + rating cards + methodology docs (planted divergences) | Impl 03 / P3 | |
| [ ] | Create AI Search index `prism-ratings` (as-of metadata, embedding model matched) | Impl 03 / Arch 08 | |
| [ ] | `tools/SeedData` idempotent uploader; acceptance query returns card with metadata intact | Impl 03 / Arch 08 | |
| [x] | Connectors: `EdgarClient` + `FredClient` (as-of + provenance) + `IProviderRatingsSource` (Moody's/DBRS pending-spec, MSCI synthetic); fundamentals derivation in pure `Analysis/FundamentalsCalculator` | Impl 04 / Arch 08 | orch |
| [x] | Connector as-of + fundamentals unit tests (stubbed HTTP) — 99 tests green | Impl 04 / Arch 11 | orch |
| [x] | Frontend shell: nav (Prism/Architecture/Settings), apiClient, `useIssuers`, dark theme — Vite app scaffolded, 12 vitest green, build green | Impl 09 / Arch 10 | orch |

## Phase 2 — Engines, agents, API (Day 2)

| ✔ | Task | Ref | Owner |
|---|---|---|---|
| [x] | `DivergenceDecomposer` (deterministic) + attribution-invariant tests — exact `sum==gap`, honest residual-dominance (`IsResidualDominated`) | Impl 05 / P2 / Arch 11 | orch |
| [x] | `RedFlagEngine` (deterministic) incl. STALE_INPUT money-moment rule (date-only UTC) + `ReconciliationScoring` + tests | Impl 05 / P2 / Arch 11 | orch |
| [ ] | Define Foundry Prompt Agents (provider ×3, fundamentals, narrators, orchestrator) | Impl 06 / Arch 04 | |
| [ ] | C# agent wrappers with OTel spans, grounding, citation contract | Impl 06 / Arch 04,07 | |
| [ ] | Narrator-honesty validation (LLM never overrides deterministic numbers) | Impl 06 / P2 / Arch 11 | |
| [ ] | Cosmos containers `rating_reconciliations`(/issuerId), `audit_events`; `ReconciliationService`, `AuditService` | Impl 08 / Arch 08 | |
| [ ] | Controllers: Issuers, Reconciliations, Health; standard error shape; ProblemDetails | Impl 08 / Arch 03,09 | |
| [ ] | Console end-to-end: NordStar dossier object + flag fires; Cedar Grove all-green | Impl 06,08 | |

## Phase 3 — Orchestration & the UI (Day 3)

| ✔ | Task | Ref | Owner |
|---|---|---|---|
| [ ] | AG-UI: `PrismOrchestrator` + function tools + `confirmScope` gate; `MapAGUI("/prism")` | Impl 07 / Arch 04 | |
| [ ] | Node sidecar → `ExperimentalEmptyAdapter` + `HttpAgent`; keep actions fallback committed | Impl 07 / Arch 04,06 | |
| [ ] | Sidecar security: authenticate endpoint, forward user identity, re-authorize tool args | Impl 07 / Arch 06 | |
| [ ] | `DivergenceBoard` + `ProviderVerdictCard` (3-notch split highlight) | Impl 10 / Arch 10 | |
| [ ] | `EvidenceStream` streaming cards; `ScopeGate` via `renderAndWaitForResponse` (amber) | Impl 10 / Arch 10 / P5 | |
| [ ] | `DecompositionWaterfall` (recharts, ≤4 bars, sums to gap) | Impl 10 / Arch 10 | |
| [ ] | `RedFlagBanner` → `RuleModal` showing deterministic rule + both dated source rows | Impl 10 / P2 | |
| [ ] | Workflow page tab "Rating Reconciliation Pipeline" with populated detail panels | Impl 10 / copilot-instructions | |

## Phase 4 — Persistence polish, deploy (Day 4)

| ✔ | Task | Ref | Owner |
|---|---|---|---|
| [ ] | Dossier export (printable HTML) + **pre-rendered PDF fallback committed** | Impl 10 / Impl 12 | |
| [ ] | Audit events on scope-confirmed / generated / exported (no PII) | Impl 08 / Arch 06 | |
| [ ] | Consensus control (Cedar Grove) path verified end-to-end | Impl 10 / Impl 12 | |
| [ ] | OTel/App Insights end-to-end trace visible | Arch 07 | |
| [ ] | Bicep/azd: ACA (API + sidecar), Static Web App, RBAC GUIDs, minReplicas 1, heartbeat | Impl 11 / Arch 06,08 | |
| [ ] | `azd up` green; SSE streams on ACA; managed identity works | Impl 11 / P1 | |

## Phase 5 — Test, rehearse, submit (Day 5)

| ✔ | Task | Ref | Owner |
|---|---|---|---|
| [ ] | Backend tests (notch, decomposer invariant, red-flags, EDGAR as-of, narrator-honesty, API) pass | Arch 11 | |
| [ ] | Frontend tests (board split, RuleModal, waterfall sum) pass | Arch 11 | |
| [ ] | Build clean (warnings-as-errors); `get_errors` empty | Arch 11 | |
| [ ] | Live smoke: NordStar + Cedar Grove twice against live Foundry | Impl 12 | |
| [ ] | Rehearse 3-min demo 3×; severity-ranked flags; cut-lines ready | Impl 12 | |
| [ ] | Rubric self-check (all six criteria) | Impl 12 | |

---

## Cross-cutting adherence gates (check before each PR/merge)

- [ ] No mock/fake data in runtime code; missing config fails loud (P1)
- [ ] Deterministic logic stayed in `Analysis/`; LLM only narrated/cited (P2)
- [ ] Every claim/flag carries a citation; as-of correctness preserved (P3)
- [ ] No buy/sell/recommend language anywhere (P4)
- [ ] Scope gate present before the sweep completes (P5)
- [ ] No PII/secrets in logs, prompts, or images; tool args re-authorized (P6, Arch 06)
- [ ] `CancellationToken` plumbed through all I/O (P7)
- [ ] Naming, folder placement, error shape, DTO separation match `architecturalPlan/` (Arch 01,02,03,09)

---

## Implementation Log

The automated build workflow (`/prism-build-package {NN}`) appends one entry per package here after
it implements, adversarially reviews, and corrects the work. Newest first.

<!-- Template — copy for each completed package:

### {NN} — {package name} · {{YYYY-MM-DD}}
- **Status:** done / partial / blocked
- **Files changed:** see `.copilot-tracking/changes/{{YYYY-MM-DD}}/{NN}-changes.md`
- **Plan:** `.copilot-tracking/plans/{{YYYY-MM-DD}}/{NN}-plan.md`
- **Adversarial review:** `.copilot-tracking/reviews/{{YYYY-MM-DD}}/{NN}-review.md`
  - Findings: {C} Critical / {H} High / {M} Medium / {L} Low — all Critical/High resolved
- **Build/tests:** `dotnet build` ✅ · `dotnet test` ✅ ({n} passing) · frontend ✅/n-a
- **Residual risks:** {none | …}
-->

### 10 — Prism UI & Workflow Visualization · 2026-07-06
- **Status:** done (deterministic dossier UI live against the pkg-08 API; pkg-07 copilot/AG-UI narration deferred as honest placeholders)
- **Files:** `frontend/src/` — `types/prism.ts` re-synced to live DTOs, `lib/prismFormat.ts` (+test), `components/prism/*` (ProviderVerdictCard, DivergenceBoard, DecompositionWaterfall, RedFlagBanner, RedFlagPanel, RuleModal, DossierPanel, ScopeNotice), rebuilt `pages/ReconciliationPage.tsx`, enriched `components/workflow/workflowData.ts`, `pages/__tests__/ReconciliationPage.test.tsx`. See `.copilot-tracking/plans/2026-07-06/10-plan.md`.
- **Adversarial review:** not run — the orchestrator sub-agent stopped after implementation (no `10-review.md`/`10-changes.md`). Verified **functionally + live in-browser** instead (issuer list → NordStar reconcile → STALE banner + verdict board + residual-dominance framing + red-flag panel all render from real Azure). **Follow-up:** run the adversary/reviewer phases.
- **Fixes applied post-implementation:** (1) `prismFormat.test.ts` fixtures used invalid `RedFlagCode` strings → real codes; (2) added `ResizeObserver` stub to `src/test/setup.ts` (recharts needs it in jsdom); (3) **StrictMode bug** — auto-run POST was a mutation fired from `useEffect`; React 18 double-mount stranded `isPending` → converted to a `useQuery` (cache-backed, StrictMode-safe); (4) gated the CopilotKit provider to mount only when `VITE_COPILOT_URL` is set (no dead `/copilotkit` hammering).
- **Build/tests:** `npm run build` ✅ strict tsc + vite · `npm run test` ✅ 36 passing.
- **Residual risks:** pkg-07 copilot narration + AG-UI streaming + workflow animation deferred (honest placeholders); adversarial review pending.

### 08 — API & Persistence · 2026-07-06
- **Status:** done — **all 5 acceptance criteria pass live against real Azure**
- **Files:** `Controllers/` (Issuers, Reconciliations), `Services/` (IReconciliationService + impl, SearchCorpus + mapper, DossierAssembler, DossierHtmlRenderer, CosmosDossierStore, AuditService), `Models/` (PrismDtos, PrismDtoMappings, AuditEvent), `Infrastructure/` (AzureOptions, PrismJson [`JsonStringEnumConverter`], PrismExceptionHandler, Errors/{NotFound,Validation}), Program.cs + ServiceCollectionExtensions. See `.copilot-tracking/plans/2026-07-06/08-plan.md`.
- **Live acceptance:** `GET /api/v1/issuers` → 6 issuers from Search; `POST /api/v1/reconciliations` (NordStar) → dossier persisted to Cosmos **with the STALE_INPUT flag**; `GET /api/v1/reconciliations/{id}` round-trips; `audit_events` written; error envelope `{error:{code,message,details}}`.
- **Adversarial review:** not run — orchestrator sub-agent stopped after implementation (no `08-review.md`/`08-changes.md`); verified live instead. **Follow-up:** run adversary/reviewer.
- **Fixes applied post-implementation:** missing `using` in a test; explicit `Newtonsoft.Json 13.0.3` package ref (Cosmos SDK transitive dep wasn't copied → runtime `FileNotFoundException`); `[property: Required]` → `[Required]` on the positional-record request DTO.
- **Build/tests:** `dotnet build -warnaserror` ✅ 0 warnings · `dotnet test` ✅ 163 passing (142 API + 21 SeedData).
- **Residual risks:** pkg-06 narrators not wired (dossier `narrative` empty — deterministic `rule` text is authoritative); adversarial review pending.

### 03 — Synthetic Data & AI Search Index · 2026-07-06
- **Status:** done — corpus authored + **live `prism-ratings` index seeded and verified**
- **Files:** `tools/SeedData/` (console seeder + 30 labeled-synthetic corpus docs: 6 issuers, 17 rating cards, 3 methodology, 4 reference) + `tools/SeedData.Tests/` (21 tests). See `.copilot-tracking/{plans,changes,reviews}/2026-07-06/03-*`.
- **Adversarial review:** `.copilot-tracking/reviews/2026-07-06/03-review.md` — no Critical; 1 High (client-side embedding for the acceptance query) + 3 Medium + 3 Low — **all resolved**.
- **Live gate cleared:** deployed `text-embedding-3-small` on the Foundry account, then `seed` + `--verify` — index config OK (18 fields, vectorizer, semantic), 30 docs, hybrid "NordStar leverage" → `nordstar-msci`, Cedar Grove consensus.
- **Build/tests:** `dotnet build -warnaserror` ✅ 0 warnings · `dotnet test` ✅ 144 passing (123 + 21 new).
- **Residual risks:** real-EDGAR issuer anchoring (fictional issuers, labeled synthetic) → pkg 04/12; runtime integrated vectorization would need the Search MI to hold *Cognitive Services OpenAI User*.

### 09 — Frontend Shell & Navigation · 2026-07-06
- **Status:** done (scaffolds the whole `frontend/` Vite app)
- **Files:** ~44 net-new under `frontend/` — pinned `package.json` (React 18.3, Vite 5, TanStack Q/T v5/v8, CopilotKit 1.4.8, Tailwind 3, react-router 6.30.4), hand-authored `components/ui/*` shadcn primitives, `components/layout/*` (Prism-branded), `components/workflow/*`, `pages/*`, `hooks/{useIssuers,useReconciliation}`, `lib/{apiClient,settings,utils,queryClient}`, `types/prism.ts`, `ErrorBoundary`. See `.copilot-tracking/changes/2026-07-06/09-changes.md`.
- **Plan:** `.copilot-tracking/plans/2026-07-06/09-plan.md`
- **Adversarial review:** `.copilot-tracking/reviews/2026-07-06/09-review.md` — Standards + Stack + Security (Architect → pkg 07/10).
  - Findings: 0 Critical + 3 High (TooltipProvider crash, auth-seed, dep advisories) + Medium/Low — **all resolved** (TooltipProvider mounted, ErrorBoundary, env-aware runtimeUrl, whitelist settings, safe error rendering, react-router-dom → 6.30.4 clearing runtime highs, +tests).
  - **CROSS-PACKAGE REQUIREMENT for pkg 08:** register `JsonStringEnumConverter` so `Provider` serializes as `"Moodys"` (else the frontend union mismatches). `types/prism.ts` sub-DTOs are inferred → re-sync when `PrismDtos.cs` lands.
  - Residual: full MSAL auth (seam in place) → pkg 07/08; CSP/fonts → pkg 11; 9 moderate CopilotKit-chain vulns (not exercised until sidecar) → pre-demo gate; workflow-tab shadcn polish → pkg 10.
- **Build/tests:** `npm run build` ✅ strict tsc + vite · `npm run test` ✅ 12 passing · runtime `npm audit` ✅ 0 critical/0 high.

### 05 — Divergence Decomposer & Red-Flag Rules (deterministic heart) · 2026-07-06
- **Status:** done
- **Files:** `Analysis/DivergenceDecomposer.cs` (+ `ResidualShare`/`IsResidualDominated`), `Analysis/RedFlagEngine.cs` (STALE_INPUT/MISSING_COVERAGE/OUTLIER_PROVIDER/METHODOLOGY_CONFLICT), `Analysis/ReconciliationScoring.cs`, `Infrastructure/Errors/DomainInvariantException.cs`, tests + `TestSupport/PrismFixtures.cs`. See `.copilot-tracking/changes/2026-07-06/05-changes.md`.
- **Plan:** `.copilot-tracking/plans/2026-07-06/05-plan.md`
- **Adversarial review:** `.copilot-tracking/reviews/2026-07-06/05-review.md` — Standards + Stack + a **mutation-proven** focused re-review.
  - Findings: 1 Critical + 5 High + Medium/Low — **all resolved** (honest residual-dominance replacing the residual-dump; STALE date-only UTC boundary; de-tautologized property test; InvariantCulture; confidence double-penalty; tolerance guard).
  - **Product-truth:** the waterfall is rich only with factor+vintage data; letter-only real providers are residual-dominated — now DETECTED honestly → demo leads with the STALE flag + "methodology-driven divergence."
  - Residual: `Decompose` caller + `IsResidualDominated` consumer → pkg 07/10; factor-name normalization → real API taxonomies.
- **Build/tests:** `dotnet build -warnaserror` ✅ 0 warnings · `dotnet test` ✅ 123 passing.

### 04 — Data Connectors (Moody's/Morningstar · EDGAR/FRED) · 2026-07-06
- **Status:** done (real-provider HTTP pending API specs — see residual)
- **Files:** `Connectors/` (EdgarClient, FredClient, IProviderRatingsSource + ProviderRatingRecord, SyntheticRatingsSource[MSCI], Moodys/MorningstarDbrsRatingsClient pending-spec stubs, AsOf, TransientRetryHandler, ConnectorTelemetry), `Analysis/FundamentalsCalculator.cs` (P2), `Infrastructure/PrismOptions.cs` + `ServiceCollectionExtensions.cs` + `Errors/`, `Program.cs`. See `.copilot-tracking/changes/2026-07-06/04-changes.md`.
- **Plan:** `.copilot-tracking/plans/2026-07-06/04-plan.md`
- **Adversarial review:** `.copilot-tracking/reviews/2026-07-06/04-review.md` — all four adversaries (full attack surface) + a focused Stack re-review of the XBRL Critical.
  - Findings: 3 Critical + 5 High + several Medium/Low — **all resolved over 2 correction rounds** (transport→UpstreamServiceException, HttpClient timeout, XBRL duration coherence moved to `Analysis/`, submissions-API filing date, FRED key log-leak, pending-stubs return null, retry Retry-After, point-of-use config, mapper, debt/D&A period coherence).
  - Residual (deferred): EBITDA=EBIT basis marker + TTM → pkg 05/06; real Moody's/DBRS HTTP → API specs; keyed-DI → pkg 07; caching → pkg 08.
- **Provider enum realigned:** `Bloomberg` → `Moodys` (final set Moodys/MorningstarDbrs/Msci).
- **Build/tests:** `dotnet build -warnaserror` ✅ 0 warnings · `dotnet test` ✅ 99 passing.

### 02 — Domain Model & Notch Ladder · 2026-07-06
- **Status:** done
- **Files:** created `Analysis/NotchLadder.cs`, `Models/PrismModels.cs`, `Tests/Analysis/NotchLadderTests.cs`, `backend/Directory.Build.props`; pinned test deps. See `.copilot-tracking/changes/2026-07-06/02-changes.md`.
- **Plan:** `.copilot-tracking/plans/2026-07-06/02-plan.md`
- **Adversarial review:** `.copilot-tracking/reviews/2026-07-06/02-review.md` — Prism Standards Adversary + Fin Adversary Stack Critic (Architect/Security deferred: no attack surface on a pure-logic package).
  - Findings: 1 Critical + 1 High + 6 Medium + Lows — **all Critical/High/feasible-Medium resolved** (ToLabel fail-loud, null guard, exception types, FluentAssertions license pin, warnings-as-errors gate, test rigor).
  - Residual (deferred): NR/WR/D sentinels → pkg 04; `IReadOnlyList` value-equality + `decimal` serialization → pkg 08.
- **Build/tests:** `dotnet build` ✅ 0 warnings (warnings-as-errors) · `dotnet test` ✅ 65 passing.

### Scaffold (package 01 automatable portion) · 2026-07-06
- **Status:** done — `backend/` solution (API + tests, net9.0) builds green; `.env.example`, `backend/.gitignore`, `run-backend.bat` created.
- **Deferred (human Azure gates):** GPT-5 registration, `az login` + RBAC, region + resource provisioning, TPM quota, AG-UI NuGet pin. Frontend/copilot-runtime scaffold → pkg 09/07.
