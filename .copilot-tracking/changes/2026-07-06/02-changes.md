<!-- markdownlint-disable-file -->
# Implementation Changes: Package 02 — Domain Model & Notch Ladder

**Plan:** `.copilot-tracking/plans/2026-07-06/02-plan.md`
**Work package:** `implementationPlan/02-domain-model-and-notch-ladder.md`
**Date:** 2026-07-06

## Files Created
| File | Description |
|---|---|
| `backend/FinancialServices.Api/Analysis/NotchLadder.cs` | Deterministic (P2, BCL-only, no LLM/network) `Provider` enum + `NotchLadder` static class: canonical 1–21 S&P/Fitch ladder, `ToNotch`/`ToLabel`/`Gap`/`CrossesIgHyBoundary`, and `BuildMap` with canonical + DBRS (high/mid/low) + Moody's (Aaa…C) aliases. Unknown label throws `ArgumentOutOfRangeException`. |
| `backend/FinancialServices.Api/Models/PrismModels.cs` | 8 sealed domain records: `RatingFactor`, `ProviderRating`, `Issuer`, `FundamentalSnapshot`, `BucketAttribution`, `PairDivergence`, `RedFlag`, `ReconciliationDossier`. Nullable-clean; no P4-forbidden vocabulary. |
| `backend/FinancialServices.Tests/Analysis/NotchLadderTests.cs` | xUnit + FluentAssertions matrix (59 cases): canonical round-trip, DBRS aliases (incl. `(mid)`), Moody's aliases (mixed case), tolerant whitespace, `Gap` sign, IG/HY boundary at `BBB-`, `ToLabel` clamp, unknown/blank throws. |

## Files Modified
| File | Changes |
|---|---|
| _(none — no existing runtime/csproj/Program.cs edits; package 02 needs no new packages or wiring)_ | — |

## Plan Deviations
- **Task 1.4 — `Normalize` (orchestrator override D6):** the plan's D6 kept `Normalize = s.Trim().ToUpperInvariant()` (package-exact). Per the **orchestrator's explicit D6 decision**, implemented a **tolerant** normalize: uppercase, trim, **and collapse all internal whitespace to nothing**, so `A (LOW)`, `A(LOW)`, and `A  (low)` all resolve to the same notch. Map keys are pre-normalized the same way; public label output stays canonical (`ToLabel`). Added a whitespace-tolerance test (`A(low)` == `A (low)` == `A  (low)`) to lock this in. No other deviations.

## Verification
- `get_errors` on all three new files: **0 diagnostics**.
- `dotnet build` (from `backend/`, `FinancialServices.slnx`): **Build succeeded — 0 Warning(s), 0 Error(s)** under nullable + `TreatWarningsAsErrors`.
- `dotnet test` (from `backend/`): **Passed! — Failed: 0, Passed: 60, Skipped: 0, Total: 60** (59 `NotchLadderTests` + 1 existing `ScaffoldTests`).

## Completed Tasks
- [x] Phase 1: Analysis / NotchLadder — 12/12 tasks
- [x] Phase 2: Models / PrismModels — 10/10 tasks
- [x] Phase 3: Tests / NotchLadderTests — 9/9 tasks
- [x] Phase 4: Build & quality gate — 3/3 tasks
- Workflow visualization: **N/A for package 02** (no runtime node/agent/service added; the engines that become workflow nodes ship in packages 05/06, which own the WorkflowPage update).

## Acceptance
| Acceptance | Result |
|---|---|
| A1 — `ToNotch` round-trips corpus labels | ✅ canonical + DBRS + Moody's cases pass |
| A2 — `Gap("A (low)","BBB-") == +3` | ✅ (and `-3` reversed, `0` equal) |
| A3 — DBRS + Moody's aliases; IG/HY boundary | ✅ IG floor at `BBB-` (notch 10) |
| A4 — records compile nullable + warnings-as-errors | ✅ clean build |
| T1 — labels map; IG/HY at `BBB-`; `Gap` sign | ✅ |

## Corrections (post-adversarial-review)

**Date:** 2026-07-06 · **Trigger:** Prism Standards Adversary + Fin Adversary panel on package 02.
**Result:** `dotnet build` **0 Warning(s) / 0 Error(s)**; `dotnet test` **Passed! — Failed: 0, Passed: 65, Skipped: 0, Total: 65** (64 `NotchLadderTests` + 1 `ScaffoldTests`).

| # | Severity | Fix applied | Files |
|---|---|---|---|
| 1 | Critical | **`ToLabel` fail-loud (P1 + package 03):** removed `Math.Clamp`; now `throw new ArgumentOutOfRangeException(nameof(notch), notch, "Notch must be 1..21.")` when `notch < 1 or > 21`. Split the old clamp theory into `ToLabel_Valid_Notch_Returns_Canonical` (1→AAA, 10→BBB-, 21→C) and `ToLabel_Out_Of_Range_Throws` (0, 99, -1 → throw). Removed the clamp-codifying `0→AAA`/`99→C` assertions. | `Analysis/NotchLadder.cs`, `Tests/Analysis/NotchLadderTests.cs` |
| 2 | Medium | **Null/blank guard:** `ToNotch` now calls `ArgumentException.ThrowIfNullOrWhiteSpace(label)` first, so null/blank raises a clear `ArgumentException` (never `NullReferenceException`). Added `ToNotch_Null_Or_Blank_Throws_ArgumentException` (`null`, `"   "`). | `Analysis/NotchLadder.cs`, `Tests/Analysis/NotchLadderTests.cs` |
| 3 | Medium | **Correct exception type for unknown label:** unknown grades now throw `System.ArgumentException` (a bad value, not a range violation) instead of `ArgumentOutOfRangeException`. Updated `ToNotch_Unknown_Or_Blank_Throws` expected type to `ArgumentException`. `ArgumentOutOfRangeException` is retained **only** for the out-of-range notch in `ToLabel` (fix 1). | `Analysis/NotchLadder.cs`, `Tests/Analysis/NotchLadderTests.cs` |
| 4 | High | **FluentAssertions license ceiling:** pinned `FluentAssertions` to `[6.12.1,7.0.0)` (6.12.x = Apache-2.0 free; v8+ is commercially licensed) with an explanatory XML comment; bumped `Microsoft.NET.Test.Sdk` `17.11.1 → 17.12.0`. | `Tests/FinancialServices.Tests.csproj` |
| 5 | Medium | **Repo-wide quality gate:** created `backend/Directory.Build.props` setting `<Nullable>enable</Nullable>` + `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` so **both** projects (incl. tests) are gated. No new warnings surfaced in the test project — nothing to fix/suppress. Existing per-project settings left as-is (harmless duplication). | `backend/Directory.Build.props` (new) |
| 6 | Medium/Low | **Test rigor:** reworked `Moody_Aliases_Map_CaseInsensitive` to feed casing *different* from the stored key (e.g. `"baa1"→8`, `"AA1"→2`) so it genuinely proves case-folding; added `ToNotch_OutOfFamily_Alias_Throws_ArgumentException` for `"AAA (high)"` and `"Aa4"`. | `Tests/Analysis/NotchLadderTests.cs` |
| 7 | Low | **Comment:** noted at the DBRS `(mid)` mapping that `(mid)` is an INPUT alias only and is never emitted by `ToLabel` as a display label. | `Analysis/NotchLadder.cs` |

### Test count delta
59 → 64 `NotchLadderTests` cases (ToLabel valid 3 + ToLabel throws 3 + unknown 4 + null/blank 2 + out-of-family 2; Moody's count unchanged at 15). Suite total **60 → 65**.

## Residual risks (deferred)

Left exactly as-is per orchestrator decision; not in scope for this correction pass.

| Item | Rationale | Target package |
|---|---|---|
| NR/WR/D/SD/RD sentinel handling in `ToNotch` | Non-grade sentinels arrive with real provider feeds; handle at the ingestion boundary. | **04 — real-data connectors** |
| `IReadOnlyList` record value-equality + `decimal` JSON serialization | Persistence/serialization concern, not domain-math. | **08 — API & persistence** |
| `Provider` enum location (in `NotchLadder.cs`) | Kept per the package 02 spec; revisit only if a broader domain namespace emerges. | (spec — no change) |
