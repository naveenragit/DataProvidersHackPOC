# 14 — Deterministic-core completeness · Plan (2026-07-08)

**Goal:** close the seven gaps found reviewing the deterministic rating-divergence core
(`Analysis/`). Every change stays P2 (plain C#, no LLM/network), P4 (no buy/sell/recommend/
allocate/trade/alpha/**signal** vocabulary), and preserves the pinned demo acceptance:
NordStar → one `STALE_INPUT` high, Cedar Grove → zero flags, Aster Bio → `MISSING_COVERAGE`,
Onyx → one `OUTLIER_PROVIDER`.

New red-flag emission order (stable):
`STALE_INPUT → IG_HY_BOUNDARY → MISSING_COVERAGE → OUTLIER_PROVIDER → PROVIDER_UNDER_REVIEW → METHODOLOGY_CONFLICT`.

---

## R1 — IG/HY boundary straddle flag (HIGH)

- `NotchLadder`: add `public const int IgHyFloorNotch = 10;` and
  `public static bool IsInvestmentGrade(int notch) => notch <= IgHyFloorNotch;`. Refactor
  `CrossesIgHyBoundary` to reuse `IsInvestmentGrade` (single source of truth for the line).
- `RedFlagEngine`: after the STALE loop, if the rating set contains **both** an IG and an HY
  provider, emit one `IG_HY_BOUNDARY` (high). Rule text names which providers are IG vs HY and the
  factual consequences (index membership, NAIC/Solvency II capital, collateral eligibility; index
  rules resolve splits by convention — middle of three / lower of two). Evidence = issuer + every
  provider card. **No P4 forbidden words.**
- Tests: fires once for a straddling set (BBB-/notch10 + BB+/notch11); does **not** fire for all-IG
  ({9,9,10}) or all-HY ({11,11}). Confirms NordStar/Onyx/Cedar unaffected.

## R2 — Non-grade sentinel + outlook-suffix handling (HIGH, latent crash)

- `NotchLadder`: add `TryToNotch(string? label, out int notch)` that (1) tries the normalized key,
  (2) on miss strips a **known** outlook/watch decoration set (`(NEGATIVE|POSITIVE|STABLE|DEVELOPING)`,
  trailing `*-`/`*+`/`*`, `,NEGATIVE`…, leading `(P)`) and retries, (3) returns `false` for blank or a
  non-grade status (`NR, WR, D, SD, RD, LD`). DBRS `(HIGH|MID|LOW)` resolve on the first try and are
  never stripped. `ToNotch` delegates to `TryToNotch` and still throws `ArgumentException` on failure
  (existing contract preserved). Add `IsNonGradeStatus`.
- `SearchCorpusMapper`: add `ToProviderRatingOrNull` (tolerant) and switch `MapCards` to drop cards
  that don't resolve to a grade → they become `MISSING_COVERAGE` naturally instead of a 500. Keep the
  strict `ToProviderRating` for callers that require a grade.
- Tests: `TryToNotch` resolves `"A (low), Negative"`/`"BBB *-"`; returns false for `NR/WR/D/SD/RD`;
  `ToNotch("NR")` throws; `MapCards` drops a `WR` card and keeps the rest.

## R3 — `METHODOLOGY_CONFLICT` discrimination (MEDIUM)

- In the letter-only production path every gap is 100 % residual, so the `residualShare > 0.5` gate
  always passes. Track the `staleProviders` set from the STALE loop and **skip** `METHODOLOGY_CONFLICT`
  for any pair where either provider is stale — a stale-input flag already explains that gap. Keep the
  residual + `|gap| ≥ 2` gates for the remaining pairs.
- Tests: a wide gap between a stale and a fresh provider → no `METHODOLOGY_CONFLICT` (STALE explains
  it); a wide residual gap between two fresh providers → still fires.

## R4 — `OUTLIER_PROVIDER` symmetric-spread fix (MEDIUM)

- Replace "distance ≥ 3 from median" with "the **unique** most-distant provider, ≥ 3 from median":
  compute each provider's distance, find the max; fire only if exactly one provider holds it and it is
  ≥ 3. `{6,9,12}` (tie at 3) → no flag; `{6,6,10}` (Onyx, unique 4) → one flag. Still requires ≥ 3
  ratings; 2-provider wide splits are intentionally covered by `METHODOLOGY_CONFLICT` / `IG_HY_BOUNDARY`
  (documented — no redundant new flag).
- Tests: `{6,9,12}` no outlier; Onyx still one outlier.

## R5 — `STALE_INPUT` materiality window (MEDIUM)

- Add `StaleMaterialityDays = 45`. Fire only when the filing post-dates the action by **> 45 days**
  (whole UTC days). NordStar (77 d) still fires high; a 10-day gap no longer fires. Keeps the
  money-moment high severity (band stays binary to preserve the pinned NordStar `high` assertion).
- Tests: 10-day-stale rating → no flag; NordStar 77-day → still one high.

## R6 — Model Outlook / CreditWatch (LOW/MEDIUM)

- `Analysis`: add `enum RatingOutlook { Unknown, Positive, Stable, Negative, Developing }`.
- `ProviderRating` + `ProviderRatingRecord`: add **optional** trailing `RatingOutlook Outlook =
  Unknown, bool UnderReview = false` (positional back-compat preserved). Thread through
  `ToProviderRating`.
- `ProviderVerdictDto` + `ToVerdictDto` + `frontend/src/types/prism.ts` + `ProviderVerdictCard`: carry
  and display the outlook when not `Unknown` / when under review.
- `RedFlagEngine`: emit one `PROVIDER_UNDER_REVIEW` (low) per provider with `UnderReview == true`
  (on CreditWatch — an imminent-change indicator that lowers confidence). Real mapper defaults to
  `Unknown/false` (honest — no fabrication); a dedicated fixture drives the test.
- Tests: a provider with `UnderReview` → one low flag; default corpus → none.

## R7 — Confidence penalty de-duplication (LOW)

- `ReconciliationScoring.ConfidenceScore`: compute penalty per **distinct code** using the max
  severity for that code (so N per-pair `METHODOLOGY_CONFLICT` / two `OUTLIER` ends count once).
  `MISSING_COVERAGE` still excluded (already in the coverage fraction).
- Tests: two `METHODOLOGY_CONFLICT` flags penalize once; existing NordStar 0.70 / Cedar 1.0 / Aster
  2/3 unchanged.

---

## Cross-cutting

- Update `frontend/src/types/prism.ts` `RedFlagCode` union: add `IG_HY_BOUNDARY`,
  `PROVIDER_UNDER_REVIEW`; add `RatingOutlook` + verdict fields.
- Build (`dotnet build` warnings-as-errors) + `dotnet test` + frontend `vitest`/`tsc` all green.
- Adversarial review: **Prism Standards Adversary** (conformance to P1–P8 + acceptance) and
  **Fin Adversary Architect** / **Fin Adversary Stack Critic**. Resolve all Critical/High.
- New `prism-demo-deck.html` slide explaining the deterministic logic.
- Track in `architecturalPlan/TASKS.md` (new "Phase 6 — Deterministic completeness" block).
