# Adversarial Correctness Review: Prism Package 05 — Deterministic Decomposition + Red-Flag Math

Target: `backend/FinancialServices.Api/Analysis/DivergenceDecomposer.cs`, `RedFlagEngine.cs`,
`ReconciliationScoring.cs` + tests + `TestSupport/PrismFixtures.cs`.
Context: pure C#, `decimal` arithmetic, `NotchGap:int`, `BucketAttribution.Notches:decimal`, warnings-as-errors.
Callers in production code: **none yet** (grep: only tests call `Decompose`/`Evaluate`/`ConfidenceScore`).

## Verification Log (attacks run, with verdict)

- Invariant `Weighting+Input+Adj==gap` → `adj = gap − weighting − input` (DivergenceDecomposer.cs:39) → **TAUTOLOGY** (true by construction; guard unreachable on terminating decimals).
- METHODOLOGY_CONFLICT divide-by-zero → `absGap<2 continue` (L75) evaluated before `/absGap` (L81) → **SAFE** (no div-by-zero).
- ConfidenceScore divide-by-zero at 0 providers → denominator is `Enum.GetValues<Provider>().Length` = 3, not `ratings.Count` (ReconciliationScoring.cs:39) → **SAFE**.
- Even-count median int-truncation → `(sorted[mid-1]+sorted[mid]) / 2m` uses decimal literal (RedFlagEngine.cs:104) → **SAFE** (no truncation); branch also near-unreachable (only 3 providers exist).
- OUTLIER off-by-one at exactly 3 → `>= 3m` includes 3 (L59); Onyx {6,6,10} median 6, |10−6|=4 flags MSCI only → **CORRECT for the fixture**, but see [C05-05] double-flag.
- Input-bucket sign → locked by one directed test (`Decompose_StaleProviderHigherLeverage` +2) → **PINNED** (thinly).
- P4 vocabulary (buy/sell/hold/alpha/signal…) → absent from all three files → **CLEAN**.
- Determinism/no-network/no-LLM → confirmed pure; property seed fixed `new Random(20260706)` → **REPRODUCIBLE**.

## Findings

### [C05-01] Decomposition is a residual dump for the demo cast — Severity: **Critical**
- **Target:** `DivergenceDecomposer.cs:37-39`, `:80-86`, `:92-100`; `PrismFixtures.cs:56-62` (NordStarRatings).
- **Claim under test:** the Weighting/Input/MethodologyAdjustment waterfall is an *analytically meaningful* attribution.
- **Reality:** `adj = gap − weighting − input` is the residual (L39), and the other two are structurally tiny:
  - **Weighting** (L80-86) = `5 × Σ(wA−wB)·avgScore` over shared factors. Weights sum to ~1 on each side ⇒ `Σ(wA−wB)=0`, so the sum is a *score-weighted zero-sum delta* (a covariance). For realistic score bands it is capped at a few tenths of a notch. The project's OWN directed test proves it: gap 3 → Weighting **−0.10 (−3%)**, Adj **3.10 (103%)** (`DivergenceDecomposerTests.cs:99-105`).
  - **Input** (L92-100) is `0m` unless BOTH per-provider own-vintage snapshots exist with `DebtToEbitda` AND `latest.DebtToEbitda` is present. **No such per-provider vintage source exists**: pkg-04 `FundamentalsCalculator` yields ONE `latest` snapshot; no orchestration supplies `aInputs`/`bInputs` (grep-confirmed no caller). So Input ≈ 0 in the real pipeline.
  - The named-cast fixtures make it concrete: `NordStarRatings()` have **empty `Factors`** and no per-provider input snapshots ⇒ Weighting 0, Input 0, **Adj = 100% of the gap**.
- **Why it matters (money moment):** the divergence *waterfall* is half the on-stage reveal. For a NordStar-style pair it renders as a single undifferentiated **~100% "MethodologyAdjustment"** bar — cosmetically valid (sums to gap) but analytically empty: it says "the difference is methodology," which is a tautological restatement of "the numbers differ and we couldn't attribute them." An explainer that doesn't explain is a credibility hit with judges.
- **Fix:** (1) Author factor sets + per-provider vintage snapshots in `PrismFixtures` for NordStar and add a decomposition test that asserts Weighting/Input carry real, non-trivial mass (not just `sum==gap`). (2) Provide a real per-provider vintage source OR honestly relabel the third bucket "Unexplained / methodology residual" and cap the narration. (3) Suppress the waterfall (fall back to the STALE flag) whenever residual > ~80% of the gap, so the demo never shows an all-residual bar.

### [C05-02] STALE_INPUT same-day boundary: instant-based `<` on cross-source `DateTimeOffset` — Severity: **High**
- **Target:** `RedFlagEngine.cs:29` — `if (r.InputAsOfDate < latest.FilingDate)`.
- **Claim under test:** same-day input is deterministically "not stale"; the rule fires only when the provider truly predates the filing.
- **Reality:** `<` compares absolute *instants*. Fixtures use midnight-UTC (`D(...)`) so tests are clean, but in the live pipeline `InputAsOfDate` (Moody's/DBRS ratingActionDate) and `latest.FilingDate` (EDGAR `filed`, parsed in pkg 04) come from **different parsers/time zones**. Two values on the *same calendar day* with different offsets/time-of-day (e.g. input `2025-11-05T00:00Z` vs filing `2025-11-05T05:00Z`) make `<` **fire a false STALE** — or suppress a real one in the other direction. The intended "same day = not stale" semantics only holds under a date-only comparison.
- **Why it matters (money moment):** STALE_INPUT is *the* headline flag. A tz/time-of-day artifact flipping it on a same-day input corrupts the exact rule being showcased, live.
- **Fix:** compare calendar dates: `r.InputAsOfDate.UtcDateTime.Date < latest.FilingDate.UtcDateTime.Date` (or `DateOnly`); state the rule as "input dated at least one day before the filing." Add a same-day fixture (input `…T00:00Z`, filing `…T14:00-05:00`) asserting **no** flag.

### [C05-03] The 200-seed "invariant" property test is a tautology (false confidence) — Severity: **High**
- **Target:** `DivergenceDecomposerTests.cs:26-73`.
- **Claim under test:** the property test proves the attribution math is correct over varied inputs.
- **Reality:** because `adj ≡ gap − weighting − input` (DivergenceDecomposer.cs:39) and magnitudes never approach decimal's 28–29 digit precision, `weighting + input + adj == gap` is true **for every seed and every possible implementation** of Weighting/Input. A sign flip (`wB−wA`), a wrong scale, or literally returning random numbers for Weighting would still sum to gap and pass. It validates only "decimal doesn't drift," never attribution correctness. The three directed cases are the *only* real checks, and one of them (input sign) is a single data point.
- **Why it matters:** this is presented as the rigor behind the money-moment math; it cannot catch a reconciliation or sign bug in the buckets it claims to protect.
- **Fix:** inside the loop, independently recompute expected Weighting and Input (mirror the formula) and assert **each bucket** equals expected — not just `Sum == gap`. Keep a separately-named `decimal-exactness` assertion for the sum.

### [C05-04] `latest` cancels out of the Input bucket — staleness-vs-reality is not computed — Severity: **Medium**
- **Target:** `DivergenceDecomposer.cs:38` + `:92-100`.
- **Reality:** `input = LC(bInputs,latest) − LC(aInputs,latest)` with `LC = scale×(providerLev − latestLev)` ⇒ `input = scale×(bLev − aLev)`. `latestLev` **cancels** and acts only as a null-gate. So the Input bar measures provider-vs-provider vintage leverage, not "how stale each provider is vs reality" (a provider on 5.0x contributes identically whether reality is 3.0x or 10.0x). Since providers usually rate off the same public financials, `aLev≈bLev ⇒ Input≈0`, compounding [C05-01].
- **Why it matters:** the doc-comment (L90-92) and any narration promising "the Input bar shows stale-financials impact vs the latest filing" describe math the code does not perform.
- **Fix:** to express staleness magnitude, attribute a signed per-provider `(providerLev − latestLev)` to the stale side rather than differencing two such terms; otherwise correct the comment/narration to "difference in the providers' own-vintage leverage."

### [C05-05] OUTLIER mislabels high-dispersion triples as double outliers — Severity: **Medium**
- **Target:** `RedFlagEngine.cs:54-63`.
- **Reality:** with 3 ratings like `{2,10,18}` the median is 10 and BOTH extremes are ≥3 from it ⇒ **two** OUTLIER_PROVIDER flags, when the reality is broad dispersion with no single outlier. The "one deviating provider" story breaks.
- **Why it matters:** on a genuine 3-way split the UI tags two of three providers as outliers — analytically wrong and easy for a judge to poke.
- **Fix:** flag only when exactly one rating exceeds the threshold, or use distance from the *other* providers (median-absolute-deviation / peer mean) and require a unique max deviation.

### [C05-06] Missing coverage is penalized twice (denominator + medium flag) — Severity: **Medium**
- **Target:** `ReconciliationScoring.cs:39-53`; pinned by `ReconciliationScoringTests.cs` `2/3 − 0.15`.
- **Reality:** an absent provider lowers `coverage` (2/3) AND emits a MISSING_COVERAGE medium flag that subtracts another 0.15 — the same fact docked on both axes.
- **Why it matters:** the confidence number is shown on stage; "why does one missing provider cost both coverage and a penalty?" exposes a double-count.
- **Fix:** choose one axis — exclude MISSING_COVERAGE from the penalty sum, or keep coverage informational and let the flag carry the penalty.

### [C05-07] Factor matching is `Ordinal` (case/space-sensitive) ⇒ real taxonomies won't overlap — Severity: **Low**
- **Target:** `DivergenceDecomposer.cs:80-86`, `:150-152` (`FindFactor`, Ordinal); `LeverageFactorName` (L21) also Ordinal.
- **Reality:** Moody's "Leverage" vs MSCI "Financial Leverage"/"leverage" match nothing ⇒ every factor becomes overlay ⇒ Weighting 0, all mass to residual (compounds [C05-01]); an Input value can also exist with no leverage citation if the factor isn't exactly "Leverage" (P3 uncitable).
- **Fix:** normalize + alias factor names (case-insensitive + synonym map) before matching; test with differently-cased shared factors.

### [C05-08] "Can never fire" reconcile guard CAN throw 500 on non-terminating weights — Severity: **Low**
- **Target:** `DivergenceDecomposer.cs:58-66`.
- **Reality:** with 2-dp weights `w+i+(g−w−i)` is exact; if a future weight is computed as e.g. `1m/3m`, decimal rounding at ~28 digits can leave a sub-ulp residual and `EnsureBucketsReconcile` throws `DomainInvariantException` (500) on legitimate input.
- **Fix:** compare with tolerance (`Math.Abs(w+i+adj−gap) > 1e-9m`) or guarantee terminating-decimal weights at the boundary; document the precondition.

### [C05-09] Just-over-50% conflict displays "50%", contradicting the ">50% / most" rule — Severity: **Low**
- **Target:** `RedFlagEngine.cs:81-84`.
- **Reality:** threshold `>0.5m`; the smallest firing ratio rounds to 50% ⇒ flag text reads "50% of the gap explained by methodology" while asserting methodology explains "most."
- **Fix:** display `Math.Ceiling`, floor the threshold at 51%, or reword to "more than half."

### [C05-10] Null/`default` `latest` silently no-ops STALE (opposite of C05-02) — Severity: **Low**
- **Target:** `RedFlagEngine.cs:19-23` (non-null `latest`) vs `ReconciliationDossier.Fundamentals` nullable (`PrismModels.cs`).
- **Reality:** if orchestration lacks EDGAR data and passes a `default`-ish snapshot, `FilingDate = 0001-01-01` ⇒ no rating is ever stale (silent miss).
- **Fix:** guard `latest` — skip/mark STALE-inapplicable when fundamentals are absent — so the money-moment rule can't silently vanish.

## Unverified (needs live confirmation)
- Real time-zone handling of `latest.FilingDate` and provider `InputAsOfDate` in the assembled pipeline (pkg 06/07 not built) — [C05-02] severity assumes cross-source tz drift; confirm once the orchestrator wires EDGAR + provider dates.
- Whether any future orchestration fabricates per-provider `aInputs`/`bInputs` (would be a P1 violation) or leaves them null (confirms [C05-01] residual dump).

## Top 3 Won't-Explain / Won't-Convince Risks
1. **Waterfall = one 100% "MethodologyAdjustment" bar** for the demo cast (empty factors, no per-provider vintage) — [C05-01].
2. **STALE flag flips on a same-day tz artifact** from two different date sources — [C05-02].
3. **Property test proves nothing** about attribution correctness (tautology) — [C05-03].

## GO / NO-GO on the money-moment math
**PARTIAL GO:** the STALE_INPUT reveal is sound and compelling **once the same-day date-only fix ([C05-02]) lands** — but the divergence **waterfall is NO-GO as an analytical artifact** (residual dump, [C05-01]) until Weighting/Input are given real mass or the third bucket is honestly reframed.
