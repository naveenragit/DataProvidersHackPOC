# Package 10 — Prism UI & Workflow Visualization — Detailed Plan

**Date:** 2026-07-06 · **Orchestrator:** Prism Build Orchestrator (executes phases in-process)
**Spec:** `implementationPlan/10-frontend-prism-ui-and-workflow.md`
**Depends on:** 07 (DEFERRED — CopilotKit sidecar / AG-UI streaming / LLM narration), 08 (DONE + LIVE),
09 (DONE — frontend shell). **Blocks:** 12.

**Governing standards:** `architecturalPlan/00-core-principles.md` (P1–P8), `10-frontend-architecture.md`,
`01-naming-conventions.md`, `09-api-and-contracts.md`, `11-testing-and-quality.md`.

---

## 0. Live contract (verified vs http://localhost:8000 on 2026-07-06)

- `GET /api/v1/issuers` → `IssuerListItem[]`, `coverage` **always present** (`string[]`).
- `POST /api/v1/reconciliations` `{ issuerId, asOf }` → `DossierResponse`.
- `GET /api/v1/reconciliations/{id}` → `DossierResponse`.
- `GET /api/v1/reconciliations/{id}/export` → `text/html` (printable), 200.
- 404 → `{ error: { code:"NOT_FOUND", message, details:{resource,id} } }`.
- Dates are `DateTimeOffset` with `-04:00/-05:00` offsets. The STALE rule text embeds the **UTC**
  date (`2025-09-15` = Msci `2025-09-14T20:00:00-04:00`) → **format verdict dates in UTC** to match.
- Scenarios: **nordstar** STALE high, 9/9/10 = **1-notch** split (not the pre-pivot "3-notch"),
  all divergences residual-dominated (Methodology bucket = whole gap). **cedargrove** full consensus,
  0 flags, conf 1.0. **onyx** 4-notch split, 3 flags, conf 0.55. **asterbio** 2 verdicts + 1
  MISSING_COVERAGE, conf 0.667. `attribution.explanation` and `flag.narrative` are `""` (pkg 06/07
  deferred → honest placeholder, never fabricated).

---

## 1. Guardrails (every task)

- **P1** — real API only; honest loading/empty/error; no mock/fabricated rows or narration.
- **P2** — the UI **renders** what the API returns for notch/gap/flag; it never computes them.
  Residual-dominance / widest-pair / date-format are **display derivations** over API-provided
  buckets (explicitly permitted). Comment them as presentation.
- **P4** — never buy/sell/hold/recommend/allocate/trade/alpha/signal in code, copy, tooltips, labels,
  or workflow nodes. Grep-verify before gate.
- **P8** — reuse pkg-09 shell (apiClient, hooks, shadcn primitives, ErrorBoundary, TooltipProvider,
  WorkflowDiagram). Do not hand-roll primitives; do not add ad-hoc `useEffect`+`fetch`.
- Strict TS; functional components; Tailwind + shadcn tokens; `lucide-react` icons; recharts for charts.

---

## 2. Tasks (one item per file)

### T1 — Re-sync `frontend/src/types/prism.ts` (EDIT)
- `IssuerListItem.coverage: Provider[]` → **required** (live always returns it).
- Update header comment: contract is now **LIVE-VERIFIED** vs :8000 (drop "INFERRED").
- No other shape change (verdict/divergence/attribution/flag/dossier already match live).

### T2 — `frontend/src/lib/prismFormat.ts` (CREATE, pure presentation helpers)
- `formatUtcDate(iso: string): string` → `new Date(iso).toISOString().slice(0,10)` (yyyy-MM-dd, UTC)
  so verdict as-of dates match the deterministic rule text. Guard invalid → return the raw string.
- `formatConfidence(n: number): string` → `${Math.round(n*100)}%`.
- `bucketOf(d, bucket)` → the `BucketAttributionDto` for a bucket (or undefined).
- `residualShare(d): number` → `|methodologyNotches| / max(|notchGap|,1)` (mirrors pkg-05
  `DivergenceDecomposer.ResidualShare`; **presentation** — chooses the chart, does not compute the gap).
- `isResidualDominated(d, threshold=0.8): boolean` → `|notchGap|>=1 && residualShare(d)>=threshold`.
- `widestDivergence(divergences): PairDivergenceDto | undefined` → max `|notchGap|` (highlight pick).
- `highSeverityFlags(flags)` / `sortFlagsBySeverity(flags)` → order high→medium→low.
- All pure, no React. Comment: "display derivations over API-provided values (P2)".

### T3 — `frontend/src/components/prism/ProviderVerdictCard.tsx` (CREATE)
- Props `{ verdict: ProviderVerdictDto; highlighted?: boolean }`.
- shadcn `Card`; `Badge` for the letter; provider label via `PROVIDER_LABELS`; notch, `as-of`
  (UTC-formatted, muted), `input as-of` (UTC), `methodologyDocId` as a monospace cited ref.
- `highlighted` → `border-primary` ring (widest-gap pair). No narrative. P4-clean.

### T4 — `frontend/src/components/prism/MissingCoverageCard.tsx` (CREATE)
- Props `{ provider: Provider }`. Muted `Card` with an "off"/`SearchX` icon and
  "No rating published" — honest placeholder for an absent provider (corroborated by MISSING_COVERAGE).

### T5 — `frontend/src/components/prism/DivergenceBoard.tsx` (CREATE)
- Props `{ dossier: DossierResponse }`.
- Header: `consensusSummary` (verbatim) + `ConfidenceMeter` (T6).
- Card row: for each `p` in `ALL_PROVIDERS`, render `ProviderVerdictCard` if a verdict exists else
  `MissingCoverageCard`. Highlight the two providers in `widestDivergence(dossier.divergences)`
  when the widest gap ≥ 1.

### T6 — `frontend/src/components/prism/ConfidenceMeter.tsx` (CREATE)
- Props `{ confidence: number }`. `formatConfidence` label + a slim bar (`bg-primary`, width = %).
  aria-valuenow/min/max. Purely renders the API value.

### T7 — `frontend/src/components/prism/DecompositionWaterfall.tsx` (CREATE)
- Props `{ divergence: PairDivergenceDto }`.
- `notchGap === 0` → "No divergence — full consensus for this pair" state (no chart).
- `isResidualDominated(divergence)` → **honest single-bucket framing**: a short "methodology-driven
  divergence" panel (the whole gap is a methodology residual not mechanically attributable to
  weighting/input timing) with a one-bar indicator — **not** a misleading rich waterfall.
- else → recharts `BarChart`, ≤4 bars (Weighting, Input, MethodologyAdjustment, + net gap), each
  labeled with bucket + signed notches. Wrap in a fixed-height container.
- Always render an accessible text legend of `bucket: notches` (so tests assert text, not SVG, and
  for a11y). `explanation` empty → a muted "narrative pending (narrator agent — pkg 06/07)" note
  (`DeferredNarrationNote`), never fabricated.

### T8 — `frontend/src/components/prism/DeferredNarrationNote.tsx` (CREATE)
- Small muted inline note: LLM narration (CopilotKit + AG-UI, pkg 07) not yet wired → narrative
  intentionally blank; the **deterministic rule/values above are authoritative**. Keeps P1 honest.

### T9 — `frontend/src/components/prism/RuleModal.tsx` (CREATE)
- Props `{ flag: RedFlagDto; open; onOpenChange }`. shadcn `Dialog`.
- Verbatim `flag.rule` in a monospace block; severity `Badge`; `code`; the `evidenceRefs` rendered
  as a list of cited source rows (for STALE: `nordstar-Msci` + `edgar:0000000001:10-Q`). Narrative
  block only if non-empty else `DeferredNarrationNote`. This is the "we didn't rig it" surface.

### T10 — `frontend/src/components/prism/RedFlagPanel.tsx` (CREATE)
- Props `{ flags: RedFlagDto[] }`.
- Empty → honest consensus state: "No red flags — provider ratings reconcile; fully defensible."
- Else list (sorted high→medium): each row = severity `Badge` (high=`destructive`, medium=amber),
  `code`, verbatim `rule`, evidence chips (monospace), "View rule & evidence" → opens `RuleModal`.

### T11 — `frontend/src/components/prism/RedFlagBanner.tsx` (CREATE)
- Props `{ flags: RedFlagDto[] }`. Renders the prominent destructive banner **only** when a
  high-severity flag exists (the money moment). Shows the flag `rule`; "View rule & evidence" →
  `RuleModal`. Nothing when no high-severity flag.

### T12 — `frontend/src/components/prism/ScopeNotice.tsx` (CREATE)
- Amber/orange banner (P5 HITL convention): "Scope: reconciliation only — Prism explains why provider
  data diverges; it makes no investment decisions." Honest static placeholder; notes the interactive
  confirm-scope gate (`renderAndWaitForResponse`) lands with the copilot sidecar (pkg 07). P4-clean.

### T13 — `frontend/src/components/prism/DossierPanel.tsx` (CREATE)
- Props `{ dossier: DossierResponse }`. Summary: issuer id, as-of (UTC), dossier id (monospace),
  verdict/flag counts. **Export** `Button` → `window.open('/api/v1/reconciliations/{id}/export','_blank')`
  (real endpoint, browser prints to PDF). No fabricated content.

### T14 — Rebuild `frontend/src/pages/ReconciliationPage.tsx` (EDIT/replace body)
- Read `?issuer=` (`useSearchParams`). As-of `Input type="date"` defaulting to `settings.defaultAsOf`
  or today.
- `useReconciliationRun()` mutation. Auto-run once per `issuerId::asOf` (effect with a last-run ref)
  + a manual "Re-run reconciliation" `Button`. (Mutation-from-effect is fine; not ad-hoc fetch.)
- No issuer → prompt with a link to `/issuers`.
- Pending → honest loading `Card`. Error → `ApiError.code` + generic message (mirror IssuersPage;
  never render raw message/details).
- Success layout (order): `ScopeNotice` → `RedFlagBanner` (money moment) → `DivergenceBoard` →
  `DecompositionWaterfall` for `widestDivergence` (auto honest-vs-rich) → `RedFlagPanel` →
  `DossierPanel`. Consensus path (cedargrove) → no banner, board all-green, panel "fully defensible".

### T15 — Enrich `frontend/src/components/workflow/workflowData.ts` (EDIT)
- Add datastore nodes: **Azure AI Search** (`Services/SearchCorpus.cs`) feeding provider-explainer;
  **Cosmos DB** (`Services/CosmosDossierStore.cs`) after dossier. Point deterministic nodes at real
  files (`Analysis/DivergenceDecomposer.cs`, `Analysis/RedFlagEngine.cs`), controller at
  `Controllers/ReconciliationsController.cs`.
- Mark `red-flag-engine → confirm-scope-gate` edge `dashed: true, label:'escalation'` (red-flag
  escalation path). Add edges `azure-ai-search → provider-explainer-agent`,
  `dossier-ready → cosmos-db`. Reposition minimally for legibility. Keep populated detail panels.
  P4-clean (grep the file).

### T16 — Static export fallback `frontend/public/fallback/nordstar-dossier.html` (CREATE)
- Capture the **live** `/reconciliations/{id}/export` HTML (real content, not fabricated) as the
  on-stage fallback; browser prints to PDF. (True binary `.pdf` remains a documented cut-line —
  acceptance item marked PARTIAL, rationale in changes log.)

### T17 — Tests (CREATE, vitest smoke)
- `frontend/src/lib/prismFormat.test.ts` — formatUtcDate (Msci → `2025-09-15`), formatConfidence
  (0.6667 → `67%`), isResidualDominated (nordstar M↔Msci gap1/meth1 → true; a rich pair → false),
  widestDivergence (onyx → gap 4), sortFlagsBySeverity.
- `frontend/src/components/prism/__tests__/ProviderVerdictCard.test.tsx` — renders label/letter/notch.
- `frontend/src/components/prism/__tests__/RedFlagPanel.test.tsx` — NordStar STALE rule text verbatim
  + evidence refs; empty → consensus message.
- `frontend/src/components/prism/__tests__/DivergenceBoard.test.tsx` — 3 verdicts render; asterbio
  (2 verdicts) → MissingCoverageCard for Msci; consensusSummary shown.
- `frontend/src/components/prism/__tests__/DecompositionWaterfall.test.tsx` — residual-dominated →
  methodology-driven framing text; gap-0 → consensus state; rich pair → bucket labels present.
- `frontend/src/pages/__tests__/ReconciliationPage.test.tsx` — mock `@/hooks/useReconciliation` to
  return a NordStar dossier; wrap in `MemoryRouter`; assert STALE rule text + a verdict render.
  (Mocking the hook avoids CopilotKit/QueryClient providers.)

---

## 3. Acceptance mapping

| Spec acceptance item | Plan coverage | Note |
|---|---|---|
| NordStar path: gate → board → streaming cards → waterfall → red-flag banner → RuleModal (both dated rows) → export | T3–T14, T16 | **PARTIAL** — streaming cards = pkg-07 deferred (honest note); split is **1-notch** per live data |
| Cedar Grove consensus / all-green path | T5, T7, T10, T14 | Full |
| Waterfall readable (≤4 bars, labeled) | T7 | Full (residual → honest single-bucket framing) |
| Workflow tab: pipeline + populated detail panels | T15 | Full (datastores + dashed escalation) |
| Pre-rendered PDF fallback committed | T16 | **PARTIAL** — live export HTML committed; binary PDF = cut-line |

## 4. Gate
- `npm run build` (`tsc --noEmit && vite build`) GREEN + strict TS.
- `npm run test` (vitest) GREEN, run in `frontend/`.
- P4 grep = 0 hits in new/edited files.

## 5. Residual (07-deferred, tracked)
CopilotKit generative EvidenceStream + AG-UI live animation + `renderAndWaitForResponse` scope gate +
LLM `narrative`/`explanation` text. Rendered as honest placeholders; deterministic rule/values are the
demo substance.
