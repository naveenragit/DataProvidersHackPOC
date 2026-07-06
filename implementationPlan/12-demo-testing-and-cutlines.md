# 12 — Demo, Testing & Cut Lines

**Purpose:** convert working software into a winning 3-minute demo, and protect it with the right
tests and pre-planned cuts. Days 4–5.

**Depends on:** all packages.

---

## A. The 3-minute script (rehearse 3×)

| Time | Beat | Screen |
|---|---|---|
| 0:00–0:20 | Enter **NordStar Industrials** + as-of date → three verdicts appear: DBRS `A (low)`, Bloomberg `BBB+`, MSCI `BBB-` — **3-notch split**. "Which do you trust?" | DivergenceBoard |
| 0:20–0:35 | **Confirm-scope gate** (governed agents) | ScopeGate |
| 0:35–1:15 | Provider agents stream reasoning cards; Fundamentals pulls **real EDGAR** + **FRED** context | EvidenceStream |
| 1:15–1:45 | **Waterfall** assembles: 3-notch gap = leverage (2) + sector overlay (1) | DecompositionWaterfall |
| 1:45–2:15 | **RED FLAG** — MSCI's leverage input is pre-Q3; issuer deleveraged in Q3 (filed 6 weeks ago). Click → deterministic rule + EDGAR date + MSCI as-of. "MSCI is stale." | RedFlagBanner → RuleModal |
| 2:15–2:35 | **Control** — Cedar Grove: all three within 1 notch → "consensus, high confidence" | DivergenceBoard |
| 2:35–3:00 | **Export** the reconciliation dossier — every claim hyperlinked | DossierPanel |

Narrative spine: *market data providers look at the same bond and disagree; Prism reconstructs each
provider's reasoning, decomposes the gap deterministically, and flags stale data — auditable, cited.*
**Never** say buy/sell/recommend.

---

## B. Testing priorities (80% on the parts judges probe)

**Backend (xUnit + FluentAssertions):**
- NotchLadder mapping incl. DBRS/Moody's aliases + IG/HY boundary (02).
- Decomposer attribution invariant `sum == gap` (05).
- RedFlagEngine: NordStar → 1 STALE_INPUT; Cedar Grove → 0; Aster Bio → MISSING_COVERAGE (05).
- EDGAR as-of filtering boundary (Q2 vs Q3) with stubbed handler (04).
- Narrator-honesty test: LLM narrative never overrides deterministic numbers (06).
- Integration: `WebApplicationFactory<Program>` for `/api/v1/issuers` + fallback reconciliation (08).

**Frontend (vitest + testing-library):** DivergenceBoard renders a 3-notch split; RuleModal shows the
verbatim rule + both dated rows; waterfall bars sum to the gap.

**Live smoke (rehearsal):** full NordStar + Cedar Grove paths against live Foundry, twice.

---

## C. Risks → mitigations

| Risk | Mitigation |
|---|---|
| Waterfall illegible in 90s | Day 3 dedicated; ≤4 bars; big labels |
| "You rigged the corpus" | Show the deterministic rule on flag + demo the consensus control |
| Provider-methodology accuracy | Label all ratings synthetic; cite methodology docs; say "style-modeled" |
| AG-UI streaming breaks | Fallback REST + SSE path (07); keep committed |
| PDF export flakes | Pre-rendered PDF committed (10) |
| 429s from fan-out | Chatty agents on `gpt-4.1-mini`; backoff+jitter; quota bump day 0 |
| RBAC 403 in cloud | Wait ~10 min for propagation before debugging (11) |

## D. Cut lines (in order)
1. Live PDF → pre-rendered PDF.
2. Coverage/Confidence agent → fold into red-flag engine.
3. Treasury sanity layer (04) → drop.
4. Extra issuers → keep 3 (NordStar stale, Helios overlay, Cedar Grove consensus).
5. Azure deploy → localhost against **live Foundry**.

---

## E. Rubric self-check before submitting

- [ ] **Storytelling** — one-sentence hook lands; red-flag reveal is crisp.
- [ ] **Business accuracy** — split ratings framing correct; real EDGAR/FRED shown; providers framed accurately.
- [ ] **Agentic design** — visible orchestrator + specialist cards + HITL gate.
- [ ] **Technical feasibility** — deterministic core; runs on Azure; clear customer-build path.
- [ ] **Creativity & reuse** — novel framing; built on the in-repo FinCopilotKit accelerator (call it out).
- [ ] **Teamwork** — roles split (data/corpus · C# agents · React/viz · demo); everyone can speak to their piece.
