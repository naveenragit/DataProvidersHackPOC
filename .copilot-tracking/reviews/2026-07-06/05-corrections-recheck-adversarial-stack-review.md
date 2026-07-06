# Adversarial Stack-Fit Review: Prism pkg 05 corrections re-check (decomposer + red-flags)

Target: the three corrections applied after the pkg-05 correctness review — residual-dump honesty,
property-test de-tautologization, STALE same-UTC-day boundary — plus refactor-regression sweep.

Method: read the corrected SUT + tests, hand-verified the arithmetic against the real `NotchLadder`,
then **mutation-tested** the two headline claims (flip Input sign; revert STALE to instant compare) to
prove the tests actually catch the regressions rather than passing cosmetically. Both mutations reverted;
tree restored to 0-warn / 123-pass.

## Verification Log

- Build `dotnet build -warnaserror` → **0 warning, 0 error** → green.
- Suite `dotnet test` → **Passed 123 / Failed 0 / Total 123** → green (matches corrections note).
- NotchLadder anchors (NotchLadder.cs:100-124): Baa1=8, Baa2=9, Baa3=10, A2=6, BBB-=10, BBB(mid)=9,
  A(mid)=6, Ba1=11, BB(high)=11 → all fixture comments verified against the production ladder.
- Halcyon math (by hand): Weighting = 5·[(0.50−0.30)·0.70 + (0.50−0.70)·0.80] = **−0.10**; Input =
  1·(4.5−3.0) − 1·(2.5−3.0) = **+2.0**; gap = 10−8 = 2; residual = 2 − (−0.10) − 2.0 = 0.10;
  ResidualShare = 0.10/2 = **0.05** → IsResidualDominated=false. Matches `Decompose_RichPair`.
- LetterOnly math: A2=6, BBB-=10, gap=4; empty factors → Weighting 0; null inputs → Input 0; adj=4;
  ResidualShare = 4/max(4,1) = **1.0** → IsResidualDominated=true. Matches `Decompose_LetterOnlyPair`.
- **Mutation A** — flipped Input sign in DivergenceDecomposer.cs:43 → property test failed **on
  iteration 0** ("Expected Input 5.69M … found -5.69M") + 3 directed Input oracles failed. Reverted.
- **Mutation B** — reverted STALE to `r.InputAsOfDate < latest.FilingDate` (instant) → **only**
  `Evaluate_SameUtcDayInput_DoesNotFireStale` failed (STALE_INPUT wrongly produced); NordStar money
  moment stayed green. Reverted.
- Wiring grep: `ResidualShare` consumed by RedFlagEngine.cs:87 (production). `Decompose(...)` has **no**
  production caller; `IsResidualDominated` consumed by tests only.

## Findings

### [STK-01] Residual-dump honesty (was C05-01 Critical) — Verdict: **FIXED** (honest degradation), Severity: resolved
- **Target:** `DivergenceDecomposer.cs` L84-96 (`ResidualShare`/`IsResidualDominated`), `RedFlagEngine.cs` L87,
  `PrismFixtures.cs` L138-166 (Halcyon/LetterOnly), `DivergenceDecomposerTests.cs` L206-233.
- **Claim under test:** the metrics correctly quantify residual dominance; Halcyon genuinely attributes
  mass (Weighting≠0 AND Input≠0) and is not rigged; LetterOnly proves IsResidualDominated.
- **Reality:** verified. `ResidualShare = |methodology| / max(|gap|,1)`; `IsResidualDominated` ≥ 0.8.
  Halcyon is a valid-domain fixture (weights sum to 1.00 on both sides, scores 0-100, plausible D/E)
  yet yields Weighting **−0.10** and Input **+2.0** — real mechanical attribution flowing through the
  production formula, **not rigged**. LetterOnly yields share **1.0** → dominated. `ResidualShare` is
  wired into the deterministic METHODOLOGY_CONFLICT flag (RedFlagEngine.cs:87), so the honesty metric
  drives a real decision, not just a label.
- **Caveat (important, not a defect):** this is **honest degradation, not a rich demo waterfall.** Even
  the deliberately-rich Halcyon pair puts ~all mass in **Input** (vintage leverage); Weighting is capped
  at tenths of a notch because weights sum to 1 (Σ(wA−wB)=0 ⇒ score-weighted zero-sum) — the original
  structural finding still holds. The **demo cast** (NordStar/CedarGrove/AsterBio/Onyx) has empty
  `Factors` and no per-provider vintage snapshots, so its waterfall remains one-bar-residual **by data
  reality** — now *detected* (IsResidualDominated) instead of dishonestly presented. Halcyon is a
  test-only fixture, never rendered.
- **Fix already correct; residual work is forward-integration:** pkg 07 must pass real per-provider
  factor cards + vintage snapshots into `Decompose` for a multi-bar waterfall; pkg 10 must consume
  `IsResidualDominated` to lead with the STALE story. Until then the "not a one-bar waterfall" behavior
  is unrealized in any running surface.

### [STK-02] Property test was tautological (was C05-03 High) — Verdict: **FIXED**, Severity: resolved
- **Target:** `DivergenceDecomposerTests.cs` L27-92 (property), L253-283 (`RecomputeWeighting`/`RecomputeInputLeg`).
- **Claim under test:** the 200-seed test now recomputes Weighting and Input independently and would
  fail on a sign flip or scale change (not a bare `sum==gap` identity).
- **Reality (empirically proven):** Mutation A (flip Input sign) failed the property test **on iteration
  0** — impossible for the old tautology, since `adj` is defined as the residual so `sum==gap` holds for
  any bucket values. The recompute genuinely constrains each bucket.
- **Honest limitation:** the recompute is a **parallel reimplementation (mirror)** of the SUT formula,
  not a separate oracle — a *simultaneous dual-edit* of both SUT and test would drift together
  undetected. This is back-stopped by the directed **hand-oracles** (`Decompose_EqualLeverageDifferentWeights`
  hardcodes Weighting **−0.10**, locking the 5m scale; `Decompose_StaleProviderHigherLeverage` hardcodes
  Input **+2**, locking the 1m scale and sign), which are true oracles a copy-paste cannot silence. Net:
  tautology is definitively broken; suite catches SUT-only drift; scale/sign pinned by constants.

### [STK-03] STALE same-UTC-day boundary (was C05-02 High) — Verdict: **FIXED**, Severity: resolved
- **Target:** `RedFlagEngine.cs` L30/L33/L34 (`.UtcDateTime.Date` both sides), L38 (Rule InvariantCulture);
  `PrismFixtures.cs` L108-131 (SameDay), `RedFlagEngineTests.cs` L50-60.
- **Claim under test:** both sides normalize to UTC calendar day; the same-UTC-day fixture asserts NO
  flag and would fail under the old instant compare; money moment intact.
- **Reality (empirically proven):** both operands use `.UtcDateTime.Date`. Mutation B (revert to instant
  `<`) failed **only** `Evaluate_SameUtcDayInput_DoesNotFireStale` — the Moody's action
  `2025-11-04T21:00-05:00` (= `2025-11-05T02:00Z`, same UTC day, earlier instant) wrongly tripped STALE
  under the old code. NordStar (`2025-08-20` < `2025-11-05`) still fires exactly one STALE on MSCI;
  money moment untouched. Rule text is `string.Create(CultureInfo.InvariantCulture, …)` with
  `yyyy-MM-dd` on both dates.

## InvariantCulture sweep — Verdict: **FIXED** (for its actual scope)
- Wrapped (all numeric/date interpolations): STALE Rule (RedFlagEngine.cs:38), OUTLIER Rule (:65),
  METHODOLOGY_CONFLICT Rule (:92), `ConsensusSummary` split (ReconciliationScoring.cs:29),
  `EnsureBucketsReconcile` message (DivergenceDecomposer.cs:73).
- **Only unwrapped:** MISSING_COVERAGE Rule (RedFlagEngine.cs:~51) — interpolates a `Provider` enum and a
  string only, **no numeric/date content**, so culture-safe. Harmless but inconsistent with the "wrap
  everything" intent (see STK-N1).

## New / residual issues

### [STK-N1] MISSING_COVERAGE rule string not wrapped in InvariantCulture — Severity: Minor
- `RedFlagEngine.cs:~51`. No numeric/date tokens today ⇒ no reproducibility risk, but a future edit that
  adds a count/date to this string would silently reintroduce the H2 class of bug. One-line consistency
  fix; non-blocking.

### [STK-N2] `ResidualShare` gap-0 flooring can report share > 1 — Severity: Minor (benign)
- `DivergenceDecomposer.cs:87` `max(|gap|,1)`. A zero-gap divergence with offsetting buckets
  (e.g. w=+1, i=+1, adj=−2) yields share 2.0. **Benign:** the only production consumer
  (METHODOLOGY_CONFLICT) gates on `absGap >= 2` *before* calling `ResidualShare` (RedFlagEngine.cs:83),
  so |gap|∈{0,1} never reaches it. No div-by-zero (floor=1). Consider documenting that share is only
  meaningful for |gap|≥2.

### [STK-N3] `MethodologyAdjustmentOf` silent-0 on malformed divergence — Severity: Low (pre-existing)
- `DivergenceDecomposer.cs:99-104` uses `FirstOrDefault()` keyed on the `"MethodologyAdjustment"` magic
  string; a hand-built `PairDivergence` lacking that bucket yields 0m (share 0) silently. Production
  `Decompose` always emits all three buckets, so unreachable in the real path; string-coupling only.

### [STK-N4] `.UtcDateTime.Date` vs EDGAR filing-day timezone — Severity: Minor / **Unverified**
- The boundary is correctly **consistent** (UTC on both operands). But EDGAR `filed` is a *date-only*
  field; how pkg 04 stamps `FundamentalSnapshot.FilingDate`'s offset determines whether `.UtcDateTime.Date`
  equals the intended US-Eastern filing calendar day (a real filing at `23:00 ET` = `04:00Z` next day
  shifts +1). Fixtures use `TimeSpan.Zero`, so tests are deterministic; the live-data day alignment is a
  separate deferred data-vintage concern, not a defect in this fix.

## Refactor-regression sweep (all clear)
- **Tolerance guard** (`> 0.0001m`, DivergenceDecomposer.cs:70): residual is exact-decimal 0 on valid
  data ⇒ never fires; smallest real algebra break (~0.025-notch weighting step) is >> tolerance ⇒ not
  hidden. Forced-throw test green.
- **Confidence change** (skip MISSING_COVERAGE in penalty): all three consumers pass — CedarGrove 1.0,
  NordStar 0.70, AsterBio **2/3** (only AsterBio carries MISSING_COVERAGE, and it is its sole flag ⇒ no
  cross-test contamination).
- No div-by-zero introduced; no other test's assumption broken.

## Unverified (needs live confirmation)
- STK-N4 real-EDGAR filing-day timezone alignment (fixtures are UTC-anchored).
- Multi-bar waterfall and IsResidualDominated-driven UI are **not exercised anywhere yet** — depends on
  pkg 07 (`Decompose` caller) and pkg 10 (frontend consumer).

## Top 3 Won't-Compile / Won't-Run Risks
1. **None for the pkg-05 core** — builds 0-warn, 123/123, mutation-verified.
2. **pkg 07 integration:** the honest waterfall requires the orchestrator to feed `Decompose` real
   per-provider `Factors` + vintage snapshots; with today's empty-factor demo cards every real waterfall
   is one-bar-residual (correctly labeled, but visually flat) until then.
3. **pkg 10 consumption:** `IsResidualDominated` is dead code until the frontend uses it to switch the
   money-moment narrative away from the flat waterfall.

## Verdict
**GO** on the money-moment math (decomposition reconciles exactly and is now honest about residual
dominance; STALE fires on the genuine earlier-day case and is immune to same-UTC-day cross-source
timestamps). The three corrections are **genuinely fixed, mutation-proven, not cosmetic.** Remaining
items are forward-integration wiring (pkg 07/10), not pkg-05 defects.
