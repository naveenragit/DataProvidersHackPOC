# Plan 05 — Timeline Assembler & Red-Flag Rules (deterministic C#)

**Objective:** The reproducible core. Merge vault + market evidence into one strictly-ordered,
two-lane event chain (**pure C#, no LLM**) and run the deterministic red-flag rules. This is the
"reconstruction logic is real" proof that defuses rigged-corpus skepticism — **show the rule on screen
when a flag fires.**

**Depends on:** Plan 01 (consumes corpus from 02 + snapshot from 03 at runtime). **Primary day:** 2.

> ⚠ Core principle: timeline ordering and every red-flag trigger are deterministic C# in `Analysis/`.
> The LLM only narrates and cites. This plan owns the money moment.

---

## 1. Timeline data model

- [ ] `backend/FinancialServices.Api/Analysis/TimelineModels.cs`:
  - `TimelineEvent` (record): `id`, `lane` (`Private` | `Public`), `timestamp`, `title`, `sourceDocId`,
    `author?`, `authorRole?`, `eventKind`, `citesDocId?`
  - `Timeline` (record): ordered `IReadOnlyList<TimelineEvent>` + lane grouping
  - `RedFlag` (record): `id`, `severity`, `ruleId`, `ruleExpression` (human-readable, shown on screen),
    `message`, `citedDocIds[]`, `citedTimestamps[]`
  - `Dossier` shape stub (finalized in Plan 06)

## 2. Deterministic merge/sort (no LLM)

- [ ] `backend/FinancialServices.Api/Analysis/TimelineAssembler.cs`: merge private-lane vault docs +
      public-lane market/news events into one list, **strictly ordered by timestamp**, stable tiebreak
- [ ] Preserve the two lanes for the UI (private = what the firm knew; public = what the market knew)
- [ ] Pure function `Timeline Assemble(IReadOnlyList<VaultDoc>, MarketSnapshot)`; fully unit-tested
- [ ] No network, no model — deterministic and reproducible on stage

## 3. Red-flag rules engine (deterministic)

Author `backend/FinancialServices.Api/Analysis/RedFlagRules.cs`. Each rule is a plain C# predicate that
emits a `RedFlag` citing source docs + timestamps and a human-readable `ruleExpression`.

- [ ] **Rule 1 — the gotcha (highest severity):** `approval.createdTs < citedModel.modifiedTs` — a
      risk approval signed before the valuation model it cites was last edited. `ruleExpression =
      "approvalTs < modelEditedTs"`
- [ ] **Rule 2 — hindsight fabrication:** `researchNote.createdTs > decision.tradeTs` — a note dated
      after the trade
- [ ] **Rule 3 — MNPI proximity:** a Teams/email participant appears on the `restricted_list` doc
      (set membership check)
- [ ] **Rule 4 — traded-ahead-of-news:** decision/trade timestamp precedes the public `publishedTs` of
      the market-moving news that the private lane appears to anticipate (uses the lane gap)
- [ ] Rank flags by severity; return an ordered list
- [ ] Every flag carries the exact source doc ids + timestamps so the UI can deep-link both documents

## 4. Determinism guarantees

- [ ] Unit tests (`FinancialServices.Tests/Analysis/`) covering each rule: fires on the planted needle,
      does **not** fire on the clean control decision (no false positives)
- [ ] A "golden" test that runs the full NordStar corpus → exactly the expected ranked flags
- [ ] A "control" test that runs the all-green decision → **zero** flags (exoneration)
- [ ] Rules must be idempotent and order-independent given the same inputs

## 5. Wire into the pipeline

- [ ] `TimelineAssembler` + `RedFlagRules` are invoked by the `TimelineAssembler` and `GapAndRedFlag`
      function tools (Plan 04). The `GapAndRedFlag` LLM narration consumes the already-decided
      `RedFlag[]` — it never computes or suppresses a flag
- [ ] Emit the `ruleExpression` in the API response so the UI can render the rule when a flag fires

## Acceptance criteria

- Timeline is strictly ordered, two-laned, and reproducible (byte-identical across runs)
- All four rules pass their unit tests; the golden NordStar test yields the expected ranked flags
- The control decision yields zero flags
- Every flag exposes rule text + cited doc ids + timestamps

## Cut-lines

- Keep **Rule 1 (timestamp gotcha) + Rule 2 (hindsight note)** as the guaranteed core; Rules 3–4 can be
  dropped first if behind (⚠ global cut-line #2). Rule 1 alone lands the money moment.
