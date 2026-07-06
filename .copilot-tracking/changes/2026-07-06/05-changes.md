<!-- markdownlint-disable-file -->
# Implementation Changes: Package 05 — Divergence Decomposer & Red-Flag Rules

**Plan:** `.copilot-tracking/plans/2026-07-06/05-plan.md`
**Package:** `implementationPlan/05-divergence-decomposer-and-redflags.md`
**Date:** 2026-07-06

The deterministic heart of Prism — pure C# in `Analysis/` (P2: no LLM, no network, no `Connectors`/Azure
usings). Purely additive: **8 files created, 0 files modified.** Build `-warnaserror` **0 warnings**;
`dotnet test` **120 passed / 0 failed / 0 skipped** (was 99 → +21).

## Files Created

| File | Summary |
|---|---|
| `backend/FinancialServices.Api/Infrastructure/Errors/DomainInvariantException.cs` | 500-mapped `DomainInvariantException` (`ErrorCode = "DOMAIN_INVARIANT_VIOLATED"`, `Invariant` property), mirrors the sibling exception pattern (arch/03 taxonomy). |
| `backend/FinancialServices.Api/Analysis/DivergenceDecomposer.cs` | `Decompose(a,b,latest,aInputs,bInputs) → PairDivergence`: Weighting (`5m` scale, symmetric avg score over shared factors) + Input (`1m`/turn leverage, `contribution(b)−contribution(a)`, null⇒`0m`) + `adj = (decimal)gap − weighting − input`; exact `sum==gap` invariant guard throws `DomainInvariantException`; deterministic `RefsFor` provenance (weight/input/overlay). `Explanation=""`. |
| `backend/FinancialServices.Api/Analysis/RedFlagEngine.cs` | `Evaluate(issuer, ratings, latest, divergences)` → STALE_INPUT (high), MISSING_COVERAGE (medium), OUTLIER_PROVIDER (medium, ≥3 ratings & \|notch−median\|≥3), METHODOLOGY_CONFLICT (medium, \|gap\|≥2 & \|adj\|/\|gap\|>0.5). Verbatim `Rule`, `Narrative=""`, severity fixed by rule. |
| `backend/FinancialServices.Api/Analysis/ReconciliationScoring.cs` | `static` — `ConsensusSummary` (0⇒"full consensus", 1⇒"consensus within 1 notch", ≥2⇒"{n}-notch split", empty⇒"no coverage") + `ConfidenceScore` (`clamp(coverage − Σ penalty[.30/.15/.05], 0, 1)`). |
| `backend/FinancialServices.Tests/TestSupport/PrismFixtures.cs` | Constructed casts (offline, real `NotchLadder`): NordStar (MSCI stale), Cedar Grove (fresh consensus), Aster Bio (MSCI missing), Onyx (MSCI outlier). |
| `backend/FinancialServices.Tests/Analysis/DivergenceDecomposerTests.cs` | 200-seed invariant property test + directed weighting-only / input-sign / overlay-only / null-input / evidence-refs cases + guard throws-if-forced (8 tests). |
| `backend/FinancialServices.Tests/Analysis/RedFlagEngineTests.cs` | STALE_INPUT ×1 on NordStar / 0 on Cedar Grove, MISSING_COVERAGE, OUTLIER_PROVIDER, METHODOLOGY_CONFLICT fires/skips (6 tests). |
| `backend/FinancialServices.Tests/Analysis/ReconciliationScoringTests.cs` | ConfidenceScore Cedar Grove 1.0 / NordStar 0.70 / Aster Bio 2/3−.15; ConsensusSummary spread 0/1/3 + no-coverage (7 test cases). |

## Approved Decisions Applied
- **D1** `WeightingNotchScale = 5m`, symmetric `(sA+sB)/2` over shared factors.
- **D2** `LeverageNotchScale = 1m`; `input = contribution(b) − contribution(a)`; sign pinned by `Decompose_StaleProviderHigherLeverage_InputBucketPositiveAndCarriesGap` (b stale/higher-leverage ⇒ Input **+2**).
- **D3** `Evaluate(...)` extended with `IReadOnlyList<PairDivergence> divergences` (4th param).
- **D4** METHODOLOGY_CONFLICT `|gap|≥2` AND `|adj|/|gap|>0.5`, severity `medium`.
- **D5** consensus wording exactly as specified.
- **D6** `ConfidenceScore` penalties high .30 / medium .15 / low .05, coverage = rated/3.
- **D7** null `aInputs`/`bInputs` (or null latest leverage) ⇒ that side's contribution `0m` (never fabricated).

## Plan Deviations
- **Task 2.5 (guard shape):** the invariant check is factored into a `public static void EnsureBucketsReconcile(weighting, input, adj, gap)` that `Decompose` calls inline, rather than a bare inline `if`. **Rationale:** the residual definition makes the inline throw unreachable through the public API, so this is the only way to satisfy the user's explicit "directed case proving the guard throws if forced" **without** modifying the `.csproj` (no `InternalsVisibleTo`) — keeping the change purely additive. Behaviour is identical; `Decompose` still enforces the exact `sum==gap` invariant on every call.
- **Task 2.3 (helper signature):** `InputContribution(a, b, latest, aInputs, bInputs)` is implemented as a leaner `LeverageContribution(snapshot?, latest)` called twice in `Decompose` (the `a`/`b` ratings are not needed for the leverage math). No behavioural change.

## Verification
- `dotnet build -warnaserror` → **Build succeeded, 0 Warning(s), 0 Error(s)**.
- `dotnet test` → **Passed! Failed: 0, Passed: 120, Skipped: 0, Total: 120**.
- No new `using` reaches `Connectors`/`Agents`/network/LLM (P2 pure). No P4 vocabulary anywhere.

## Completed Tasks
- [x] Phase 1 — `DomainInvariantException` (1/1)
- [x] Phase 2 — `DivergenceDecomposer` + invariant guard + `RefsFor` (6/6)
- [x] Phase 3 — `RedFlagEngine` 4 rules (5/5)
- [x] Phase 4 — `ReconciliationScoring` (2/2)
- [x] Phase 5 — Fixtures + 3 test files (7/7, 21 new tests)
- [x] Phase 6 — Verify (build 0-warn, 120 tests green)

## Corrections (post-adversarial-review)

Applied 7 adversarial fixes to the deterministic core (`Analysis/`) + tests only. Additive to the public
surface (2 new public statics: `ResidualShare`, `IsResidualDominated`); no new `Analysis` →
`Connectors`/network/LLM usings (P2 held). Build `-warnaserror` **0 warnings**; `dotnet test`
**123 passed / 0 failed / 0 skipped** (was 120 → +3 tests: same-UTC-day, rich-pair, letter-only). No test
was weakened — the tautological property test was **strengthened** into an independent recomputation.

| # | Fix | Severity | Status | What changed |
|---|---|---|---|---|
| 1 | STALE_INPUT date-only UTC boundary | High | ✅ done | `RedFlagEngine` now compares `r.InputAsOfDate.UtcDateTime.Date < latest.FilingDate.UtcDateTime.Date` (same UTC day ⇒ **not** stale). Rule reworded to *"Rating action dated {date} predates the issuer's latest filing ({FilingType}) on {date}."* New `SameDay*` fixture (Moody's action `2025-11-04T21:00-05:00` == `2025-11-05T02:00Z` — same UTC day, earlier instant, different offset) + `Evaluate_SameUtcDayInput_DoesNotFireStale`; genuinely-stale NordStar test still asserts exactly one. |
| 2 | InvariantCulture on all rule/summary strings | High | ✅ done | Every date/number interpolation in `RedFlagEngine` (STALE / OUTLIER / METHODOLOGY) and `ReconciliationScoring.ConsensusSummary` now uses `string.Create(CultureInfo.InvariantCulture, $"…")`. `using System.Globalization;` added to `RedFlagEngine`, `ReconciliationScoring`, and `DivergenceDecomposer` (exception message). |
| 3 | Honest decomposition + residual-dominance | Critical | ✅ done | `DivergenceDecomposer`: new `public static decimal ResidualShare(PairDivergence)` = `|methodology|/max(|gap|,1)` and `public static bool IsResidualDominated(d, threshold = 0.8m)` (for pkg 10 to lead with the STALE/methodology-driven story). MethodologyAdjustment doc reworded to *"the residual gap NOT mechanically attributable to factor weighting or input timing"* (no precise-measurement claim). METHODOLOGY_CONFLICT computes via `ResidualShare`, keeps both-provider `EvidenceRefs`, and states the residual share honestly. |
| 4 | Kill the tautological property test | High | ✅ done | `DivergenceDecomposerTests`: the 200-seed test now **recomputes Weighting and Input independently** (test-side `RecomputeWeighting`/`RecomputeInputLeg`, pinning the `5m`/`1m` scales + b−a direction) and asserts each returned bucket == recomputation AND `MethodologyAdjustment == gap − weighting − input`. Fixed seed retained (`Random(20260706)`). |
| 5 | Rich fixtures proving attribution | Critical/High | ✅ done | `PrismFixtures`: **Halcyon** (shared factors, different weights **and** scores + per-provider vintage snapshots with different leverage) → `Decompose_RichPair_AttributesBothWeightingAndInput` asserts `Weighting ≠ 0 AND Input ≠ 0` (and *not* residual-dominated). **LetterOnly** (empty factors, null inputs) → `Decompose_LetterOnlyPair_ResidualDominatedAndHonest` asserts `IsResidualDominated == true`. |
| 6 | ConfidenceScore no double-penalty | Medium | ✅ done | `ReconciliationScoring.ConfidenceScore` skips `MISSING_COVERAGE` flags in the severity penalty (coverage fraction already reflects them). AsterBio test expectation updated `2/3 − 0.15` → **`2/3`**, renamed `…MissingCoverageNotDoublePenalized`. |
| 7 | Reconciliation tolerance | Low | ✅ done | `EnsureBucketsReconcile` compares `Math.Abs((w + i + adj) − gap) > 0.0001m` instead of exact `!=`; still throws `DomainInvariantException` on real (whole-notch) violations. Valid + forced-throw guard tests unchanged and green. |

### Files modified (corrections)

| File | Change |
|---|---|
| `Analysis/DivergenceDecomposer.cs` | +`ResidualShare` / `IsResidualDominated` / private `MethodologyAdjustmentOf`; honest residual doc; tolerance guard; `using System.Globalization`. |
| `Analysis/RedFlagEngine.cs` | Date-only UTC stale compare + reworded Rule; InvariantCulture on all interpolations; METHODOLOGY_CONFLICT via `ResidualShare`; removed local `MethodologyAdjustmentOf`. |
| `Analysis/ReconciliationScoring.cs` | Exclude `MISSING_COVERAGE` from penalty; InvariantCulture on split string; doc note. |
| `Tests/TestSupport/PrismFixtures.cs` | +`SameDay*`, +`Halcyon*`, +`LetterOnly*` fixtures. |
| `Tests/Analysis/DivergenceDecomposerTests.cs` | Property test → independent recomputation; +rich-pair, +letter-only tests; +recompute helpers. |
| `Tests/Analysis/RedFlagEngineTests.cs` | +`Evaluate_SameUtcDayInput_DoesNotFireStale`. |
| `Tests/Analysis/ReconciliationScoringTests.cs` | AsterBio expected `2/3` (no double-penalty). |

## Residual risks (deferred)

- **OUTLIER flags both extremes** — by design; both genuinely diverge from the median. Left as-is
  (defensible for a reconciliation/data-quality tool). *Not changed* (per instruction).
- **Factor-name normalization** ("Leverage" vs "Financial Leverage", Ordinal match) — real provider
  factor taxonomies won't share exact names, so Weighting can fall to `0m` and fold into the residual.
  Tracked for when the real Moody's / Morningstar DBRS factor taxonomies are known; `IsResidualDominated`
  already surfaces this honestly to the UI. *Not changed* (per instruction).
- **Display rounding / copy** — `ResidualShare` / `IsResidualDominated` give pkg 10 what it needs to lead
  with the STALE / "methodology-driven" narrative instead of a one-bar waterfall; the actual UI labels and
  rounding are a pkg-10 concern.
- **Stale-rule display date** — the reworded Rule shows the **UTC** action/filing calendar dates, kept
  consistent with the UTC-date comparison so the shown dates always match the flag logic (even for real
  data with non-zero offsets).
