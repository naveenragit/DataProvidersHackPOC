# Adversarial Architecture Review: Package 10 — Prism UI & Workflow Visualization

**Lens:** Fin Adversary Architect (frontend red-team). **Date:** 2026-07-07.
**Target:** `frontend/src/` — `pages/ReconciliationPage.tsx`, `components/prism/*`,
`components/workflow/*`, `hooks/useReconciliation.ts`, `lib/prismFormat.ts`, `App.tsx`.

> **VERDICT: demo GO-WITH-FIXES.** The money moment (issuer → verdict board → high-severity
> STALE banner → verbatim rule modal → export) renders, is P2/P4-honest, and is unit-tested.
> But the two headline spec visuals — the **confirm-scope gate** and the **leverage/overlay
> waterfall** — do **not** appear on the real NordStar path; the sweep is a **consequential write
> auto-fired on mount with no gate**; one **Workflow sourceFile points at a non-existent backend
> file**; and the **"mandatory PDF fallback" is an unwired HTML file**. Fix the three blockers below
> and brief the presenter that the waterfall shows an amber residual bar, not a 3-bar chart.

## Threat Model Summary
- Topology attacked: browser (React Query cache) → `/api/v1` (Vite proxy) → C# API → Cosmos/Search.
  The CopilotKit sidecar (:4000) is **gated off** (`App.tsx` L30-33), so EvidenceStream + the
  interactive ScopeGate are deferred (acknowledged) — the page runs a server-driven query path.
- Highest-risk assumption: *"modelling the POST sweep as a TanStack `useQuery` is safe."* It fixed
  the StrictMode hang but silently turned a **non-idempotent Cosmos write** into an
  auto-running, reconnect-refetching, cache-keyed query with **no human gate in front of it**.

---

## Findings

### [ARC-10-01] The consequential sweep auto-fires on mount with no confirm-scope gate — Severity: High
- **Target:** `pages/ReconciliationPage.tsx` L36-37 (`useReconciliationRun(issuerId, asOf)`),
  L101 (`<ScopeNotice />` — a static banner, not a gate); `hooks/useReconciliation.ts` L16-25.
- **Attack:** Navigating to `/reconciliation?issuer=nordstar` immediately POSTs
  `/reconciliations`, which **persists a new dossier + audit event to Cosmos** (backend is
  non-idempotent — see `08-review-architecture.md` ARC-08-01: new `issuerId:{guid}` id + audit per
  call). The query sets `retry:0` and `refetchOnWindowFocus:false` but **omits
  `refetchOnReconnect`** (defaults `true`) → a network blip re-fires the write. `Re-run`
  (`handleRerun` → `refetch()`, L39-42) mints another. `ScopeNotice` is decorative copy; there is
  **no `renderAndWaitForResponse` / no Approve button** in front of the write.
- **Impact:** **P5 violated on the demo surface** — the sweep (a persisted, auditable, LLM-costing
  action) runs *before* any confirmation. Every mount past `gcTime` (5 min) and every reconnect
  duplicates dossiers on the hot `/issuerId` partition. Acceptance item 1's **"gate →"** step is
  unmet; the shipped substitute is *worse* than "no gate" because it auto-commits.
- **Hardening:** Replace auto-run with an explicit **"Run reconciliation"** button (this *is* the
  cheap, honest human gate for the demo): keep `useReconciliationRun` but add `enabled:false` +
  drive it from `refetch()` on click, OR gate render behind a `hasConfirmed` `useState`. Immediately
  set `refetchOnReconnect:false`. This restores P5, kills write-amplification, and keeps the
  StrictMode-safe query shape.
- **Reference:** P5 (human-in-the-loop before consequential actions); WAF Reliability/Cost.

### [ARC-10-02] The waterfall never renders on the real demo cast; and it is not a waterfall — Severity: High
- **Target:** `components/prism/DecompositionWaterfall.tsx` L79-106 (residual branch) vs L108-141
  (recharts branch); `lib/prismFormat.ts` L54-58 (`isResidualDominated` threshold 0.8).
- **Attack:** Every real dossier in the cast is **residual-dominated** (NordStar 9/9/10 → all gaps
  `methodology = gap`; Onyx 6/6/10 → 4-notch gaps 100% residual — see `test/prismFixtures.ts`). So
  `isResidualDominated` is always true and the recharts `BarChart` branch (L108-141) is **dead on
  stage** — the demo only ever shows the amber "methodology-driven divergence" bar. Worse, when the
  chart *does* render (only for the synthetic `richDivergence` fixture) it is a **plain bar chart**
  (three independent bucket bars + a "Net gap" bar, each from zero) — **not a running-total /
  floating waterfall**. The bars do not visually stack to the gap, so the "these buckets sum to the
  notch gap" story the spec promises is not shown.
- **Impact:** Acceptance item 1 **"waterfall (leverage 2 + overlay 1)"** is unmet on the NordStar
  path — judges primed for a decomposition waterfall see a single amber progress bar. The honest
  residual framing is *correct* for letter-only data (pkg-05 product truth), but the spec's headline
  visual is absent and the presenter will be caught flat-footed if unbriefed.
- **Hardening:** (a) Rename the component/legend to "Divergence attribution" so nobody promises a
  waterfall; (b) if a true waterfall is wanted for the `rich` case, use stacked/floating bars with a
  cumulative base so buckets visibly sum to the net; (c) **brief the presenter** that NordStar/Onyx
  render the amber residual bar by design — lead the narration with the STALE flag, not the chart.
- **Reference:** P2 (deterministic core is authoritative — the honest treatment is right); demo
  legibility (HACKATHON-FINDINGS 90s rule).

### [ARC-10-03] Workflow node sourceFiles point at a non-existent / wrong backend file — Severity: High
- **Target:** `components/workflow/workflowData.ts` — `reconciliation-orchestrator` node
  `sourceFiles: ['backend/FinancialServices.Api/Agents/ReconciliationOrchestrator.cs']`; and
  `confirm-scope-gate` node `sourceFiles: ['frontend/src/pages/ReconciliationPage.tsx']`.
- **Attack:** `Agents/ReconciliationOrchestrator.cs` **does not exist** (verified: `Agents/` holds
  `AzureAgentRunner.cs`, `ProviderExplainerAgent.cs`, `FundamentalsAgent.cs`,
  `DivergenceNarratorAgent.cs`, `RedFlagNarratorAgent.cs`, `NarrationGuard.cs`,
  `AgentResults.cs`, `IAgentTextRunner.cs`). The real orchestrators live in `Orchestration/`
  (`PrismAgentOrchestrator.cs`, `PrismStreamingOrchestrator.cs`, `PrismSweepSteps.cs`). The
  confirm-scope-gate node points at the **frontend page** — which contains only the static
  `ScopeNotice`, not the `ApprovalRequiredAIFunction` gate (that lives in `Orchestration/`).
- **Impact:** Acceptance item 4 explicitly requires **"sourceFiles point at real backend files."**
  The Workflow tab is the "look how we built it" proof surface; a judge clicking the orchestrator
  node sees a fabricated path — directly undercutting the credibility the tab exists to establish.
- **Hardening:** Point the orchestrator node at `Orchestration/PrismAgentOrchestrator.cs` +
  `Orchestration/PrismStreamingOrchestrator.cs`; point the gate node at the same server file(s)
  (`ApprovalRequiredAIFunction`), optionally *also* the frontend page. Add a tiny build/test guard
  that asserts every `sourceFiles` entry exists on disk (a vitest that `fs.existsSync`-checks each
  path against the repo root would have caught this).
- **Reference:** P8 (reuse called out honestly); WAF Operational Excellence (docs match reality).

### [ARC-10-04] "Mandatory PDF fallback" is an unwired HTML file — Severity: High
- **Target:** `public/fallback/nordstar-dossier.html` (committed); `components/prism/DossierPanel.tsx`
  L17-20 (`handleExport` → `window.open('/api/v1/reconciliations/{id}/export')`).
- **Attack:** The export button opens the **live** server URL only. If the live export flakes on
  stage (the exact HACKATHON-FINDINGS cut-line the fallback exists to cover), the user gets a broken
  tab. The committed fallback is (a) **HTML, not the PDF** the acceptance item names, and (b)
  **never referenced anywhere in `src/`** (grep for `fallback`/`nordstar-dossier` = 0 hits) → dead
  file. There is no `onerror` / try-path that swaps to the committed artifact.
- **Impact:** Acceptance item 5 **"Pre-rendered PDF fallback committed"** is only partially met, and
  the fallback provides **zero resilience** because nothing wires it in. The stage-failure scenario
  it was created for is not actually covered.
- **Hardening:** Either (a) commit a real pre-rendered **PDF** and add a visible secondary
  "Open pre-rendered fallback" link to `/fallback/nordstar-dossier.pdf` in `DossierPanel`, or (b)
  probe the live export (`fetch(url,{method:'HEAD'})`) and fall back to the static asset on non-2xx.
  Minimum viable: add the static link so the presenter has a one-click escape hatch.
- **Reference:** WAF Reliability (graceful degradation); demo cut-line.

### [ARC-10-05] RuleModal shows opaque doc-id chips, not the two *dated* evidence rows — Severity: Medium
- **Target:** `components/prism/RuleModal.tsx` L57-73 (evidence `<li>` chips render
  `flag.evidenceRefs` verbatim, e.g. `nordstar-Msci`, `edgar:0000000001:10-Q`).
- **Attack:** The spec's "we didn't rig it" moment (acceptance item 1, §A.6) requires **both dated
  source rows** — the MSCI card's `inputAsOfDate` (2025-09-15) and the EDGAR filing date
  (2025-11-05) — shown side by side. The modal renders the *rule sentence* (which does contain both
  dates, good) but the Evidence section lists **bare doc ids** with no dates and no link to the
  MSCI verdict card. The refs are also casing-inconsistent with the verdict cards
  (`nordstar-Msci` chip vs `methodologyDocId: nordstar-msci`), so they don't visually tie together.
- **Impact:** The single most important legibility beat is weaker than specified — the "proof"
  panel is a pair of cryptic ids, not two human-readable dated rows. Judges must read the rule
  sentence to find the dates; the structured side-by-side comparison is missing.
- **Hardening:** Render each evidence ref as a labelled row (`MSCI rating card — input as-of
  2025-09-15` / `SEC EDGAR 10-Q — filed 2025-11-05`), sourcing the dates from the matching
  `verdict.inputAsOfDate` and the flag's filing date; or make the chip clickable to scroll/highlight
  the corresponding `ProviderVerdictCard` (the spec's "click → opens the card content").
- **Reference:** P3 (provenance & citation); demo legibility.

### [ARC-10-06] The headline notch-split magnitude is buried in muted body text — Severity: Medium
- **Target:** `components/prism/DivergenceBoard.tsx` L34-36 (`consensusSummary` rendered as
  `text-sm text-muted-foreground`).
- **Attack:** The board's most demo-critical number — "consensus within 1 notch" / "4-notch split"
  — is small, low-contrast, secondary text next to the card title. There is no large typographic
  callout of the divergence magnitude. For a 90-second read, the audience's eye has nothing to lock
  onto that says "these three providers disagree by N notches."
- **Impact:** Legibility. The story (data disagreement) has no visual anchor; the confidence meter
  (a thin bar) competes for the same header space.
- **Hardening:** Promote the notch-split to a prominent stat (e.g. a large number + "notch split"
  label) derived from `consensusSummary` or `widestDivergence`; keep the confidence meter secondary.
- **Reference:** Demo legibility (HACKATHON-FINDINGS 90s rule).

### [ARC-10-07] Workflow tab is themed with a hardcoded slate palette, not shadcn tokens — Severity: Medium
- **Target:** `components/workflow/WorkflowDiagram.tsx` (`bg-slate-900`), `WorkflowNode.tsx`
  (`bg-blue-900/50`, `text-indigo-200`…), `WorkflowDetailPanel.tsx` (`bg-slate-900`,
  `border-slate-700`, `text-slate-300`), `pages/WorkflowPage.tsx` (`bg-slate-800/80`).
- **Attack:** arch-10 mandates **"Tailwind + shadcn tokens"** (`bg-background`, `bg-card`,
  `border-border`, `text-foreground`, `text-muted-foreground`). The entire workflow surface bypasses
  the token system with raw `slate-*/indigo-*/blue-*/teal-*` classes (inherited verbatim from the
  template). It will not track any theme-token change and reads as a visually distinct app from the
  shadcn-tokened Reconciliation/Issuers pages.
- **Impact:** Visual inconsistency across the required nav groups; a theme/token change silently
  skips the Workflow tab. Not a demo blocker (it looks fine standalone) but a standards violation and
  a maintenance cliff.
- **Hardening:** Re-skin the workflow components to shadcn tokens (`bg-card`, `border-border`,
  `text-foreground`, `text-muted-foreground`) and keep only the *semantic* node-type accents
  (agent=indigo, gate=amber, datastore=teal, outcome=green) as an explicit legend palette.
- **Reference:** arch-10 (UI system); WAF Operational Excellence (consistency).

### [ARC-10-08] Duplicated derived state — `widestDivergence` computed twice; two RuleModal instances — Severity: Medium
- **Target:** `pages/ReconciliationPage.tsx` L44-47 (`widest` for the waterfall) **and**
  `components/prism/DivergenceBoard.tsx` L20 (`widest` for card highlight) — same derivation, two
  call sites. `RedFlagBanner.tsx` L16-17 and `RedFlagPanel.tsx` L19 each own an `activeFlag`
  `useState` + their **own `<RuleModal>`**.
- **Attack:** The "widest pair" the waterfall decomposes and the pair the board rings are computed
  independently; if the pick logic ever changes in one place (e.g. tie-break), the highlighted cards
  and the decomposed pair diverge silently. Two `RuleModal` DOM instances mount per dossier — benign
  today (separate triggers) but a duplicated-state smell that invites double-open bugs.
- **Impact:** Latent inconsistency; harder-to-reason state. No user-visible defect yet.
- **Hardening:** Compute `widestDivergence` once in the page and pass the pair down to both
  `DivergenceBoard` (for highlight) and `DecompositionWaterfall`. Lift the rule-modal open-state to
  the page (single `<RuleModal>`), passing an `onOpenFlag` callback to banner + panel.
- **Reference:** Layering & coupling; single source of truth.

### [ARC-10-09] Settings model list mismatches the deployed model; "Show deterministic rule" flag is unwired — Severity: Medium
- **Target:** `lib/settings.ts` L26 (`MODEL_OPTIONS = ['gpt-4o','gpt-4o-mini','o4-mini']`,
  `DEFAULT_SETTINGS.models` uses `gpt-4o`); the `flags.showDeterministicRule` field (L45-47) — never
  read by `RuleModal`/`RedFlagPanel` (both always render `flag.rule`).
- **Attack:** The only deployed model is `gpt-5.4` (session facts). The Settings page therefore lets
  a demoer "select" models that **do not exist** in the environment, and the "Show deterministic
  rule" toggle **does nothing** — the verbatim rule always renders regardless of the flag.
- **Impact:** Honesty/credibility: a Settings surface that advertises non-existent models and a
  dead toggle undermines the "real Azure, no theatre" posture if a judge opens Settings.
- **Hardening:** Set `MODEL_OPTIONS`/defaults to `['gpt-5.4', …deployed]`; either wire
  `showDeterministicRule` into the rule surfaces (hide/show the `<pre>` rule block) or remove the
  toggle. localStorage-only is fine — but it must not lie.
- **Reference:** P1 (no fake data / honest surfaces).

### [ARC-10-10] `useDossier` is dead code on the shipped page — Severity: Low
- **Target:** `hooks/useReconciliation.ts` L28-34. The Reconciliation page uses only
  `useReconciliationRun`; nothing consumes `useDossier` (`['reconciliation', id]`).
- **Impact:** Dead surface area; implies a GET-by-id read path that the UI never exercises (the page
  always re-POSTs rather than reading a persisted dossier by id).
- **Hardening:** Either use it (deep-link `/reconciliation?id=…` → read persisted dossier instead of
  re-running the write) or remove it. Reading-by-id would *also* mitigate ARC-10-01's write-amp.

### [ARC-10-11] Waterfall shows only the single widest pair; no pair selector — Severity: Low
- **Target:** `pages/ReconciliationPage.tsx` L44-47 + L137 (`{widest && <DecompositionWaterfall …>}`).
- **Attack:** The spec (§A.5) says "for a **selected** provider pair." The UI hardcodes the widest
  pair. Onyx has two 4-notch pairs (Moodys↔Msci, DBRS↔Msci); only one is ever decomposable.
- **Impact:** Minor feature/legibility gap; the runner-up divergence is invisible.
- **Hardening:** Add a small pair selector (shadcn `Tabs`/`Select`) over `dossier.divergences`,
  defaulting to the widest.

### [ARC-10-12] Minor a11y gaps in the workflow surface — Severity: Low
- **Target:** `components/workflow/WorkflowNode.tsx` L46 (`onKeyDown={(e)=> e.key==='Enter' && …}`
  — no Space); `WorkflowDetailPanel.tsx` (plain `<div>` panel — no `role="dialog"`, no Esc-to-close,
  no focus management); `DecompositionWaterfall.tsx` L131 (bar label `position:'top'` overlaps the
  baseline for negative bucket bars).
- **Impact:** Keyboard users can't activate nodes with Space; the detail panel isn't dismissible via
  Esc and doesn't move focus; negative-notch labels can collide with the axis.
- **Hardening:** Handle Space in the node keydown (or use a real `<button>`); make the panel a Radix
  `Dialog`/`Sheet` or add Esc + focus-return; set label `position` by sign.

### [ARC-10-13] Page-level tests miss the consensus path, the waterfall, and the whole Workflow tab — Severity: Low
- **Target:** `pages/__tests__/ReconciliationPage.test.tsx` (only NordStar + loading/error/empty);
  no `WorkflowPage`/`WorkflowDiagram` test exists; no test asserts `DecompositionWaterfall` renders
  on the page or that every workflow node has a populated `detail`.
- **Attack:** Acceptance item 2 (Cedar Grove consensus renders on the **page**) and item 4 (Workflow
  tab + populated detail panels + valid sourceFiles) are **unverified** by CI. ARC-10-03's broken
  sourceFile shipped precisely because nothing tests it.
- **Impact:** Regressions in the two least-exercised acceptance items go uncaught.
- **Hardening:** Add a page test rendering `cedarGroveDossier` (asserts no banner, green panel,
  consensus board) and a workflow test asserting each node's `detail` is complete **and** every
  `sourceFiles` path exists on disk.

---

## Design Strengths (kept honest — brief)
- **The money moment is real, P2-honest, and tested.** `RedFlagBanner`/`RedFlagPanel`/`RuleModal`
  render `flag.rule` **verbatim** in a monospace block; `ReconciliationPage.test.tsx` asserts the
  exact STALE sentence ("Rating action dated 2025-09-15 predates the issuer's latest filing (10-Q)
  on 2025-11-05."). The UI computes **no** notch/gap/flag — `lib/prismFormat.ts` is display-only and
  says so; `residualShare`/`isResidualDominated` only *choose the chart treatment*, never re-derive
  the gap.
- **P1 honesty throughout.** Explicit loading/error/empty on every async surface; error state renders
  the `ApiError.code` and *nothing fabricated*; `MissingCoverageCard` shows an explicit "No rating
  published"; `DeferredNarrationNote` marks where LLM narration will land instead of faking it.
- **The residual-dominated framing is the right call.** For letter-only real providers the amber
  "methodology-driven divergence" bar is *more* honest than a rigged 3-bar waterfall (pkg-05 product
  truth). The dishonest path was correctly avoided.
- **P4 clean.** Grep of `frontend/src` for `buy|sell|hold|recommend|allocate|trade|alpha|signal|
  position sizing` = 0 hits. `ScopeNotice` states the reconciliation-only scope explicitly.
- **All server state via TanStack Query**, StrictMode-safe (the documented mutation-in-effect trap
  is avoided). Stable query keys (`['reconciliation-run', issuerId, asOf]`, `['issuers']`).
- **Solid a11y on the data-viz primitives:** `ConfidenceMeter` `role="progressbar"` + aria values;
  residual bar `role="img"` + descriptive aria-label; `RuleModal` is a Radix `Dialog` (focus trap +
  Esc + aria for free); icon-only buttons carry `aria-hidden`/`aria-label`.
- **Workflow detail panels are complete.** Every node populates title / subtitle / description /
  sourceFiles / responsibilities / dataFlow / technologies; the **dashed red-flag escalation edge**
  is present (`e-redflag-gate … dashed:true`) and edges follow orchestration order. (Only the
  *sourceFile targets* are wrong — ARC-10-03.)

---

## Acceptance Checklist (met / partial / unmet)

| # | Acceptance item | Status | Notes |
|---|---|---|---|
| 1 | NordStar: gate → board → streaming cards → waterfall → banner → RuleModal (dated rows) → export | **Partial** | banner ✅, board ✅ (real gap is 1-notch not "3"), RuleModal ✅-rule/⚠️-dates (ARC-10-05), export ✅. **gate ✗** (ARC-10-01), **streaming cards ✗** (deferred pkg07), **waterfall ✗ on stage** (ARC-10-02) |
| 2 | Cedar Grove consensus/all-green path renders | **Met (untested at page level)** | `cedarGroveDossier` fixture drives green `RedFlagPanel` + consensus board in unit tests; no page test (ARC-10-13) |
| 3 | Waterfall readable ≤4 bars, labeled | **Met** | recharts branch = 4 bars + legend; but that branch is dead on the real cast (ARC-10-02); amber residual bar is legible |
| 4 | Workflow tab: pipeline + populated detail panels | **Partial** | tab ✅, all panels populated ✅, dashed escalation edge ✅ — but **sourceFiles broken** (ARC-10-03) and untested (ARC-10-13) |
| 5 | Pre-rendered PDF fallback committed | **Partial** | an **HTML** fallback is committed but it is **not a PDF** and **not wired** into export (ARC-10-04) |

---

## Demo blockers vs polish

**Demo blockers (fix before stage):**
1. **ARC-10-01** — auto-write-on-mount / no gate → add an explicit "Run reconciliation" button
   (restores P5, stops Cosmos write-amplification) + `refetchOnReconnect:false`.
2. **ARC-10-03** — fix the Workflow `sourceFiles` (orchestrator → `Orchestration/PrismAgentOrchestrator.cs`
   + `PrismStreamingOrchestrator.cs`; gate → same server file). Judges click these nodes.
3. **ARC-10-04** — wire the fallback (static link to the committed artifact, ideally a real PDF) so a
   flaky live export doesn't dead-end on stage.
4. **ARC-10-02 (brief, don't rebuild)** — accept the amber residual bar as the honest NordStar visual;
   **brief the presenter** to lead with the STALE flag, not a "waterfall."

**Polish (post-demo):** ARC-10-05 (dated evidence rows), ARC-10-06 (prominent notch-split),
ARC-10-07 (shadcn tokens on workflow), ARC-10-08 (dedupe derived state / single RuleModal),
ARC-10-09 (real model list + wire/remove the dead toggle), ARC-10-10..13 (dead hook, pair selector,
a11y, missing tests).

## Top 3 Must-Fix
1. **ARC-10-01** — Explicit run button + `refetchOnReconnect:false` (P5 gate + no write-amp).
2. **ARC-10-03** — Repoint Workflow `sourceFiles` to real files + add an `fs.existsSync` test guard.
3. **ARC-10-04** — Wire the export fallback (link the committed artifact / commit a real PDF).
