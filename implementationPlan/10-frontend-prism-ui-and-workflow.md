# 10 — Prism UI & Workflow Visualization

**Purpose:** the demo surface — the divergence board, the decomposition waterfall, the streaming
evidence cards, the red-flag reveal, the dossier export, and the Workflow page tab. **Day 3 is
dedicated to this** (HACKATHON-FINDINGS): the demo lives or dies on legibility in ~90 seconds.

**Depends on:** 07, 08, 09. **Blocks:** 12 (demo).

Components under `frontend/src/components/prism/`.

---

## A. The reconciliation experience (`pages/ReconciliationPage.tsx`)

Top-to-bottom flow that mirrors the demo script:

1. **IssuerHeader** — issuer, sector, sample bond, as-of date picker.
2. **ScopeGate** — rendered by `renderAndWaitForResponse` on `confirmScope` (package 07): confirm
   issuer + as-of + providers → Approve. Amber/orange styling (domain HITL convention).
3. **DivergenceBoard** — three `ProviderVerdictCard`s (Bloomberg / DBRS Morningstar / MSCI): native
   letter, notch, as-of, confidence. Highlight the **notch gap** between the extremes ("3-notch split").
4. **EvidenceStream** — CopilotKit generative cards as tools stream (provider explanations,
   fundamentals). Each card cites its source doc id (click → opens the card content).
5. **DecompositionWaterfall** (recharts) — for a selected provider pair, three bars: Weighting /
   Input / MethodologyAdjustment summing to the notch gap. Label each bar with the narrator text.
6. **RedFlagBanner** — when a flag fires, a prominent banner; click opens **RuleModal** showing the
   **deterministic rule text** (package 05) + both evidence rows (the MSCI card's `inputAsOfDate` and
   the EDGAR filing date). This is the "we didn't rig it" moment.
7. **DossierPanel** — assembled summary + **Export**.

Consensus control (Cedar Grove): same UI, board shows agreement, no banner, "fully defensible."

---

## B. Component notes

- `ProviderVerdictCard` — shadcn `Card` + `Badge` for the letter; muted `as-of`; a small sparkline is
  optional. Color the widest-gap pair.
- `DecompositionWaterfall` — recharts `BarChart` with a running total; keep to ≤4 bars (legibility).
- `EvidenceStream` — virtualized list of cards; each maps a streamed tool call to a typed card.
- `RuleModal` — shadcn `Dialog`; render `RedFlag.Rule` verbatim in a monospace block + the two
  cited source rows side by side.
- All data via TanStack Query / the AG-UI agent state — no ad-hoc `useEffect` fetching.

---

## C. Dossier export

- Primary: server returns printable HTML (`/reconciliations/{id}/export`, package 08); browser
  `window.print()` → PDF. Every claim hyperlinks to its evidence card/source.
- **Mandatory fallback:** a pre-rendered PDF committed to the repo, in case live export flakes on
  stage (HACKATHON-FINDINGS cut line).

---

## D. Workflow visualization tab

In `frontend/src/components/workflow/workflowData.ts`, add a tab
`rating-reconciliation-pipeline` using the template's `WorkflowTab` shape. Nodes (each with a
populated `detail` panel: title, sourceFiles, responsibilities, dataFlow, technologies):

| Node | type | sourceFiles |
|---|---|---|
| Start / Inquiry | service | Controllers/ReconciliationsController.cs |
| Reconciliation Orchestrator | agent | Orchestration/PrismOrchestrator.cs |
| Confirm-Scope Gate | gate | Orchestration/PrismOrchestrator.cs (ApprovalRequiredAIFunction) |
| Provider Agent × Bloomberg/DBRS/MSCI | agent | Agents/ProviderExplainerAgent.cs |
| Fundamentals Grounding | agent | Agents/FundamentalsAgent.cs + Connectors/* |
| Divergence Decomposer | service | Analysis/DivergenceDecomposer.cs (deterministic) |
| Red-Flag Engine | service | Analysis/RedFlagEngine.cs (deterministic) |
| Azure AI Search | datastore | Services/SearchCorpus.cs |
| Cosmos DB | datastore | Services/ReconciliationService.cs |
| Dossier Ready | outcome | — |

Edges follow the orchestration order; dashed edge for the red-flag escalation path. Wire
`WorkflowPage.tsx` into the `/workflow` route.

---

## Acceptance for this package
- [ ] Full NordStar path renders: gate → board (3-notch split) → streaming cards → waterfall
      (leverage 2 + overlay 1) → red-flag banner → RuleModal with both dated source rows → export.
- [ ] Cedar Grove renders the consensus/all-green path.
- [ ] Waterfall is readable at a glance (≤4 bars, labeled).
- [ ] Workflow tab shows the pipeline with populated detail panels.
- [ ] Pre-rendered PDF fallback committed.
