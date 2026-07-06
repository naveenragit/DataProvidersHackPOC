<!-- markdownlint-disable-file -->
# Implementation Plan: Package 05 — Divergence Decomposer & Red-Flag Rules (the deterministic heart)

**Package doc:** `implementationPlan/05-divergence-decomposer-and-redflags.md`
**Governance:** `architecturalPlan/` 00 (P1–P8, esp. **P2 deterministic core**, **P4** no trading language), 01 (naming), 02 (folders — `Analysis/`), 03 (errors — `DomainInvariantException`), 11 (property/table tests)
**Financial Domain:** Capital Markets — corporate-bond credit-rating reconciliation (NOT a trading agent — P4)
**Azure Services:** **none** — pure C#, **no LLM, no network** (P2). BCL-only; reuses `NotchLadder`.
**Repo state at plan time:** green, **99 tests**. Enum is `Provider { Moodys, MorningstarDbrs, Msci }` (code authoritative post real-data pivot; `01` still reads "Bloomberg" — stale, do not follow it).

---

## Context the implementor must not re-derive

- **Consumed models** (`Models/PrismModels.cs`): `ProviderRating(Provider, Letter, Notch, AsOfDate, InputAsOfDate, Factors, MethodologyDocId)`; `RatingFactor(Name, Weight, Score, SourceRef)`; `FundamentalSnapshot(IssuerId, FilingDate, FilingType, DebtToEbitda?, InterestCoverage?, CashAndEquivalents?)`; `Issuer(IssuerId, LegalName, Ticker, Cik, Sector, SampleBondIsin)`.
- **Produced models** (already exist — do not redefine): `BucketAttribution(Bucket, Notches[decimal], Explanation, EvidenceRefs)`; `PairDivergence(A, B, NotchGap[int], Attribution)`; `RedFlag(Code, Severity, Rule, Narrative, EvidenceRefs)`.
- **`Explanation` and `Narrative` stay EMPTY** — the LLM narrator fills them in pkg 06; the engine never writes them (P2).
- **Ratings arrive via** `ProviderRatingRecord.ToProviderRating()` (`Connectors/IProviderRatingsSource.cs`) which maps `RatingActionDate → InputAsOfDate`. Pkg 05 consumes the **domain `ProviderRating`**, not the record. Tests build `ProviderRating`/`FundamentalSnapshot`/`Issuer` **directly** (pkg-03 corpus is not indexed yet — everything offline).
- **Money moment:** `ProviderRating.InputAsOfDate` (real `ratingActionDate` from pkg 04) `<` `FundamentalSnapshot.FilingDate` (real EDGAR filing date) ⇒ `STALE_INPUT`.

---

## Design lock (algorithms) — implementor follows this unless an Open Decision overrides

### DivergenceDecomposer

`gap = b.Notch - a.Notch` (int; matches skeleton + `NotchLadder.Gap` sign: positive ⇒ `b` weaker). All three buckets are expressed in the **same `b−a` direction** so they sum to `gap`.

- **Weighting** (same inputs, different weights): for each factor `Name` present in **both** `a` and `b`,
  `term = (weightA − weightB) × ((scoreA + scoreB) / 2 / 100m)`; `weighting = WeightingNotchScale × Σ term`.
  (Symmetric/Bennet average-score form isolates the *weight* effect; see Open Decision 1 for the scale constant and the score choice.)
- **Input** (one provider built on older financials): `inputContribution(p, pInputs)` =
  `0m` when `pInputs`, `pInputs.DebtToEbitda`, or `latest.DebtToEbitda` is null (no fabrication — P1/P3);
  else `LeverageNotchScale × (pInputs.DebtToEbitda − latest.DebtToEbitda)`.
  `input = inputContribution(b, bInputs) − inputContribution(a, aInputs)` (sign chosen so the *stale* provider's bucket carries the divergence in the gap direction — locked by the NordStar directed test, Task 5.4).
- **MethodologyAdjustment** (residual = overlay factors present in one provider only, e.g. `SectorEsgOverlay`):
  `adj = (decimal)gap − weighting − input`.
- **INVARIANT (P2/03):** after computing, assert `weighting + input + adj == (decimal)gap` **exactly** (decimal, no rounding of stored values). On violation throw `DomainInvariantException` (defensive — residual makes it algebraically true; the guard + property test prove it). **Round only for display** — display rounding is a **frontend** concern (pkg 10); `BucketAttribution.Notches` stores the **exact** decimal.
- **`RefsFor(a, b, kind)`** (private, provenance P3) returns deterministic `EvidenceRefs`:
  `"weight"` ⇒ `SourceRef`s of shared factors whose weights differ + `a/b.MethodologyDocId`;
  `"input"` ⇒ leverage-factor `SourceRef`s of `a`,`b`;
  `"overlay"` ⇒ `SourceRef`s of factors present in exactly one provider. De-duplicated, ordinal-stable order.

### RedFlagEngine

Severity is **fixed by rule** (never by a model); `Rule` text is shown **verbatim** in the UI. `Narrative` empty.

| Code | Severity | Trigger | EvidenceRefs |
|---|---|---|---|
| `STALE_INPUT` | `high` | `r.InputAsOfDate < latest.FilingDate` (per rating) | `{issuerId}-{provider}`, `edgar:{cik}:{filingType}` |
| `MISSING_COVERAGE` | `medium` | `Enum.GetValues<Provider>()` member absent from `ratings` | `{issuerId}` |
| `OUTLIER_PROVIDER` | `medium` | `ratings.Count >= 3` **and** `abs(r.Notch − median) >= 3` | `{issuerId}-{provider}` |
| `METHODOLOGY_CONFLICT` | `medium` (Open Decision 4) | per pair: `abs(NotchGap) >= 2` **and** `abs(methodologyAdjustment) / abs(NotchGap) > 0.5` | `{issuerId}-{A}`, `{issuerId}-{B}` |

- `Median(IEnumerable<int>)` private helper: sort; odd ⇒ middle; even ⇒ average of two middles (may be `.5`) — compare as `decimal`/`double`.
- `Rule` strings copied **verbatim** from the package skeleton (STALE_INPUT/MISSING_COVERAGE/OUTLIER_PROVIDER); METHODOLOGY_CONFLICT gets a matching deterministic sentence naming the pair, gap, and the % explained by methodology overlay.
- **Signature (Open Decision 3):** extend the skeleton to
  `Evaluate(Issuer issuer, IReadOnlyList<ProviderRating> ratings, FundamentalSnapshot latest, IReadOnlyList<PairDivergence> divergences)`
  so METHODOLOGY_CONFLICT can read the decomposer output. Order preserved: STALE_INPUT → MISSING_COVERAGE → OUTLIER_PROVIDER → METHODOLOGY_CONFLICT.

### ReconciliationScoring (static class in `Analysis/`)

- `ConsensusSummary(IReadOnlyList<ProviderRating> ratings) -> string` from notch spread `= max(Notch) − min(Notch)`:
  `0` ⇒ `"full consensus"`; `1` ⇒ `"consensus within 1 notch"`; `>=2` ⇒ `"{spread}-notch split"` (empty ⇒ `"no coverage"`). Wording — Open Decision 5.
- `ConfidenceScore(IReadOnlyList<ProviderRating> ratings, IReadOnlyList<RedFlag> flags) -> double` (matches `ReconciliationDossier.ConfidenceScore`):
  `coverage = ratings.Count / (double)Enum.GetValues<Provider>().Length`; subtract severity penalties per flag (`high 0.30`, `medium 0.15`, `low 0.05`); `Math.Clamp(coverage − Σpenalty, 0.0, 1.0)`. Cedar Grove ⇒ `1.0`; NordStar (1 high) ⇒ `0.70`. Weights — Open Decision 6.

### DomainInvariantException (`Infrastructure/Errors/`)

Mirror `UpstreamServiceException.cs`: `sealed class : Exception`, `public const string ErrorCode = "DOMAIN_INVARIANT_VIOLATED";`, ctor `(string invariant, string message)`, `Invariant` property, `Code => ErrorCode`. BCL-only. Maps to **500** (this is a bug — 03 taxonomy). No connector/LLM concerns.

---

## Phases

### Phase 1 — Infrastructure: exception taxonomy

- [ ] **Task 1.1** Create `backend/FinancialServices.Api/Infrastructure/Errors/DomainInvariantException.cs`
  - `sealed class DomainInvariantException : Exception`; `ErrorCode = "DOMAIN_INVARIANT_VIOLATED"`; ctor `(string invariant, string message)`; `string Invariant { get; }`; `string Code => ErrorCode`.
  - **Satisfies:** 03 taxonomy row (`DomainInvariantException → 500`); precondition for the attribution-invariant guard (package Acceptance #1).

### Phase 2 — Analysis: DivergenceDecomposer (P2 pure)

- [ ] **Task 2.1** Create `backend/FinancialServices.Api/Analysis/DivergenceDecomposer.cs` — `sealed class` with `Decompose(ProviderRating a, ProviderRating b, FundamentalSnapshot latest, FundamentalSnapshot? aInputs, FundamentalSnapshot? bInputs) -> PairDivergence`.
  - No I/O, no LLM `using`s (only `Models`, `Analysis`, BCL). **Satisfies:** package §A, Acceptance #5 (pure).
- [ ] **Task 2.2** Implement `WeightingContribution(a, b)` private (shared-factor weight×avg-score × `WeightingNotchScale`; const declared here).
  - **Satisfies:** package §A bucket 1; Open Decision 1.
- [ ] **Task 2.3** Implement `InputContribution(a, b, latest, aInputs, bInputs)` private, incl. null-input ⇒ `0m` fold-to-residual.
  - **Satisfies:** package §A bucket 2; Open Decisions 2 & 7.
- [ ] **Task 2.4** Compute `adj = (decimal)gap − weighting − input`; build the three `BucketAttribution`s with `Explanation = ""` and `RefsFor(...)` refs.
  - **Satisfies:** package §A bucket 3.
- [ ] **Task 2.5** Add the **invariant guard**: `if (weighting + input + adj != (decimal)gap) throw new DomainInvariantException("attribution.sum==gap", ...)`.
  - **Satisfies:** Acceptance #1 (invariant); 03 (`DomainInvariantException`); P2.
- [ ] **Task 2.6** Implement private `RefsFor(a, b, kind)` for the `"weight"|"input"|"overlay"` evidence sets (dedup, ordinal-stable).
  - **Satisfies:** P3 provenance; package §A (`RefsFor` in skeleton).

### Phase 3 — Analysis: RedFlagEngine (P2 pure)

- [ ] **Task 3.1** Create `backend/FinancialServices.Api/Analysis/RedFlagEngine.cs` — `sealed class` with `Evaluate(Issuer, IReadOnlyList<ProviderRating>, FundamentalSnapshot latest, IReadOnlyList<PairDivergence> divergences) -> IReadOnlyList<RedFlag>`. Pure; no I/O/LLM `using`s.
  - **Satisfies:** package §B; Acceptance #5.
- [ ] **Task 3.2** `STALE_INPUT` (high) — per-rating `InputAsOfDate < latest.FilingDate`; `Rule` verbatim; refs `{issuerId}-{provider}` + `edgar:{cik}:{filingType}`; `Narrative=""`.
  - **Satisfies:** Acceptance #2 (NordStar money moment); P2/P3.
- [ ] **Task 3.3** `MISSING_COVERAGE` (medium) — provider absent from `ratings`; refs `{issuerId}`.
  - **Satisfies:** Acceptance #4 (Aster Bio).
- [ ] **Task 3.4** `OUTLIER_PROVIDER` (medium) + private `Median(...)` — `ratings.Count>=3` and `abs(notch−median)>=3`; refs `{issuerId}-{provider}`.
  - **Satisfies:** Acceptance #4 (Onyx).
- [ ] **Task 3.5** `METHODOLOGY_CONFLICT` (medium) — per `PairDivergence`, `abs(NotchGap)>=2` and `abs(MethodologyAdjustment)/abs(NotchGap) > 0.5`; refs `{issuerId}-{A}`,`{issuerId}-{B}`.
  - **Satisfies:** user scope item 2 (4th rule); Open Decision 4.

### Phase 4 — Analysis: ReconciliationScoring (P2 pure)

- [ ] **Task 4.1** Create `backend/FinancialServices.Api/Analysis/ReconciliationScoring.cs` — `static class`; `ConsensusSummary(ratings) -> string`.
  - **Satisfies:** user scope item 3; Acceptance #3 (consensus wording); Open Decision 5.
- [ ] **Task 4.2** Add `ConfidenceScore(ratings, flags) -> double` (coverage − severity penalties, clamped 0..1).
  - **Satisfies:** user scope item 3; Acceptance #3 (Cedar Grove high); Open Decision 6.

### Phase 5 — Tests (`FinancialServices.Tests/Analysis/`, pure — no network/LLM)

- [ ] **Task 5.1** Create `backend/FinancialServices.Tests/TestSupport/PrismFixtures.cs` — builders for `Issuer`, `ProviderRating` (params: provider, letter/notch, inputAsOfDate, factors), `FundamentalSnapshot`; named casts **NordStar** (MSCI stale), **Cedar Grove** (consensus, fresh), **Aster Bio** (missing provider), **Onyx** (3-rating outlier).
  - **Satisfies:** enables Acceptance #1–#4 offline (memory: constructed fixtures, corpus not indexed).
- [ ] **Task 5.2** Create `DivergenceDecomposerTests.cs` — **invariant property/table test**: fixed-seed `Random` (~200 pairs) over varied weights/scores/leverage incl. null `aInputs`/`bInputs`; assert `Σ Attribution.Notches == NotchGap` exactly. Plus directed cases: weighting-only (equal inputs), input-only (equal weights, one stale), overlay-only (extra factor ⇒ shows in `MethodologyAdjustment`), null-inputs ⇒ input `0m` & invariant holds.
  - **Satisfies:** Acceptance #1 (property test); 11 (property/table).
- [ ] **Task 5.3** In `DivergenceDecomposerTests.cs` — negative guard test: a synthetic non-residual path cannot break the sum (assert guard never throws on valid inputs; document the guard is defensive).
  - **Satisfies:** Task 2.5 / 03 (`DomainInvariantException`).
- [ ] **Task 5.4** Create `RedFlagEngineTests.cs` — **STALE_INPUT** fires **exactly once** on NordStar (MSCI card, `Severity=="high"`, refs cite MSCI `InputAsOfDate` + EDGAR filing date) and **zero** on Cedar Grove; asserts the `input` bucket sign (locks Open Decision 2).
  - **Satisfies:** Acceptance #2 & #3 (money moment / consensus).
- [ ] **Task 5.5** In `RedFlagEngineTests.cs` — **MISSING_COVERAGE** (Aster Bio ⇒ one flag, medium) and **OUTLIER_PROVIDER** (Onyx ⇒ one flag, medium, ≥3 notches from median).
  - **Satisfies:** Acceptance #4.
- [ ] **Task 5.6** In `RedFlagEngineTests.cs` — **METHODOLOGY_CONFLICT** fires when `gap>=2` & adj>50%; does **not** fire below threshold; `Rule` verbatim; `Narrative==""`.
  - **Satisfies:** user scope item 2; Open Decision 4.
- [ ] **Task 5.7** Create `ReconciliationScoringTests.cs` — `ConfidenceScore` Cedar Grove≈`1.0` (high) vs stale/missing lower; `ConsensusSummary` spread `0/1 ⇒ "consensus…"`, `3 ⇒ "3-notch split"`.
  - **Satisfies:** Acceptance #3; user scope item 3.

### Phase 6 — Verify

- [ ] **Task 6.1** `dotnet build -warnaserror` clean; `dotnet test` green (99 existing + new). Run `get_errors` on the four new `Api` files. No new `using` reaches `Connectors`/`Agents`/network.
  - **Satisfies:** 11 quality gates; Acceptance #5 (pure).

---

## Out-of-scope phases (mode phases 5–10 — deliberately deferred, not skipped)

Package 05 is the **pure backend deterministic core**. The Fin Task Planner frontend/workflow/architecture/settings phases do **not** apply here and are owned elsewhere:
- **Frontend types/hooks/components/pages** (`DivergenceBoard`, `DecompositionWaterfall`, `RuleModal`) → **pkg 09/10**.
- **Workflow-visualization nodes** (`divergence-decomposer`, `red-flag-engine`) → **pkg 10** (`WorkflowPage.tsx`).
- **Architecture/Settings pages** → no infra/config change in this package (BCL-only) → nothing to add.
- **API wiring / persistence** (controller calls decomposer+engine, dossier persist) → **pkg 08**.
- **LLM narration** filling `Explanation`/`Narrative` → **pkg 06**.

---

## File Change Summary

| File | Action | Description |
|---|---|---|
| `backend/FinancialServices.Api/Infrastructure/Errors/DomainInvariantException.cs` | Create | 500-mapped invariant-violation exception (03 taxonomy). |
| `backend/FinancialServices.Api/Analysis/DivergenceDecomposer.cs` | Create | `Decompose(...)` → 3 buckets + exact `sum==gap` invariant guard (P2). |
| `backend/FinancialServices.Api/Analysis/RedFlagEngine.cs` | Create | 4 deterministic rules (STALE_INPUT/MISSING_COVERAGE/OUTLIER_PROVIDER/METHODOLOGY_CONFLICT). |
| `backend/FinancialServices.Api/Analysis/ReconciliationScoring.cs` | Create | `ConfidenceScore` + `ConsensusSummary` (coverage + freshness). |
| `backend/FinancialServices.Tests/TestSupport/PrismFixtures.cs` | Create | Constructed NordStar/Cedar Grove/Aster Bio/Onyx casts (offline). |
| `backend/FinancialServices.Tests/Analysis/DivergenceDecomposerTests.cs` | Create | Invariant property/table + directed bucket tests. |
| `backend/FinancialServices.Tests/Analysis/RedFlagEngineTests.cs` | Create | STALE_INPUT×1/zero, MISSING_COVERAGE, OUTLIER_PROVIDER, METHODOLOGY_CONFLICT. |
| `backend/FinancialServices.Tests/Analysis/ReconciliationScoringTests.cs` | Create | Confidence + consensus values. |

**No files modified** — package 05 is purely additive.

---

## Acceptance Traceability (package doc → tasks)

| Package Acceptance item | Satisfied by |
|---|---|
| Attribution invariant `sum == gap` for every pair (property test) | Task 2.5 (guard) + Task 5.2 (property) |
| NordStar → **exactly one** `STALE_INPUT` high on MSCI card, citing MSCI `inputAsOfDate` + real EDGAR Q3 date | Task 3.2 + Task 5.1 + Task 5.4 |
| Cedar Grove (consensus) → **zero** flags, confidence high | Task 3.x + Task 4.2 + Task 5.4 + Task 5.7 |
| Aster Bio → `MISSING_COVERAGE`; Onyx → `OUTLIER_PROVIDER` | Task 3.3 + Task 3.4 + Task 5.5 |
| Decomposer + engine run **no network, no LLM** (pure) | Tasks 2.1/3.1/4.1 placement + Task 6.1 |
| *(user scope)* METHODOLOGY_CONFLICT deterministic rule | Task 3.5 + Task 5.6 |
| *(user scope)* ReconciliationScoring confidence + consensus | Task 4.1/4.2 + Task 5.7 |
| *(user scope)* `DomainInvariantException` in `Infrastructure/Errors/` | Task 1.1 |

---

## Open Decisions (recommended default in **bold** — implementor may override with a one-line note)

1. **Weighting notch-scale constant + score choice.** Map a weighted-score delta (0..1 units) to notches via `WeightingNotchScale`. **Default `= 5m` notches per unit, score = symmetric average `(scoreA+scoreB)/2`.** The package text says `(weightA−weightB) × normalizedScore` (single score) — average is the symmetric (Bennet) reading; alternative is `scoreA` or `scoreB`. *Does not affect the invariant* (residual absorbs any choice) — it only shifts mass between Weighting and MethodologyAdjustment; pick a value that makes the pkg-10 waterfall read sensibly.
2. **Leverage→notch constant + Input sign.** `LeverageNotchScale` maps a turn of `DebtToEbitda` to notches. **Default `= 1m` notch per 1.0× leverage; `input = inputContribution(b) − inputContribution(a)`** so the stale provider's bucket carries the divergence. Sign is **locked by the NordStar directed test (Task 5.4)** — if it reads backwards, flip the subtraction, not the constant.
3. **`RedFlagEngine.Evaluate` signature change.** Skeleton omits divergences, but METHODOLOGY_CONFLICT needs them. **Default: add `IReadOnlyList<PairDivergence> divergences` as the 4th parameter** (engine stays pure; caller runs the decomposer first). Alternative: a separate `EvaluatePairFlags(...)` method — rejected (splits the rule set the UI reads as one list).
4. **METHODOLOGY_CONFLICT threshold + severity.** **Default: `abs(NotchGap) >= 2` AND `abs(MethodologyAdjustment)/abs(NotchGap) > 0.5`, severity `medium`.** Package suggests "low/medium" — `medium` matches its sibling structural flags; drop to `low` if judges want STALE_INPUT to stand out more.
5. **Consensus wording at the boundary.** **Default: `0 ⇒ "full consensus"`, `1 ⇒ "consensus within 1 notch"`, `>=2 ⇒ "{spread}-notch split"`.** Package examples are `"consensus within 1 notch"` / `"3-notch split"` — confirm whether `spread==0` should read `"consensus within 1 notch"` too.
6. **ConfidenceScore formula/weights.** **Default: `clamp(coverage − Σ severityPenalty, 0, 1)` with `coverage = ratings/3`, penalties `high .30 / medium .15 / low .05`.** Cedar Grove ⇒ `1.0`, NordStar ⇒ `0.70`, Aster Bio ⇒ `~0.52`. Alternative: multiplicative `coverage × freshness` with an explicit staleness-days horizon (365d) — more literal to "coverage + freshness" but needs a horizon constant.
7. **Null `aInputs`/`bInputs` handling.** **Default: that side's `inputContribution = 0m`** (no own-vintage recompute possible), so the effect folds into the MethodologyAdjustment residual — honest (P1, never fabricate) and invariant-safe. Explicitly tested in Task 5.2.
8. *(minor)* **Display rounding.** **Default: none in pkg 05** — store exact decimals; round-to-half-notch is a pkg-10 frontend concern. Add a `static decimal RoundForDisplay(decimal)` only if pkg 08/10 needs it server-side.
