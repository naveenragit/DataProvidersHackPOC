# Adversarial Stack-Fit Review (RE-CHECK): EDGAR fundamentals path (pkg 04 corrections)

Scope: verify STK-01/02/03/04 corrections are genuinely fixed, not superficially patched.
Targets: `Analysis/FundamentalsCalculator.cs` (new pure engine), `Connectors/EdgarClient.cs` (now I/O-only),
`Tests/Analysis/FundamentalsCalculatorTests.cs`, `Tests/Connectors/EdgarClientTests.cs`.
State at review: build 0-warn, **95/95 tests pass** (verified `dotnet test`).

## Verification Log
- Flow concepts annual-only → `IsAnnual` (FundamentalsCalculator.cs:293) = `CY{fy}` frame (IsCalendarYearFrame, len==6, digits) OR duration ∈ [345,385]d → verified in code + `BestAnnual` (L211) filters it → OI/interest coherent.
- OI + D&A same accession → `FindSameAccnAnnual` (L265) matches `End==anchor.End && Accn==anchor.Accn` (L280–282) → no longer independent → **but does NOT assert IsAnnual on the D&A leg** (see NEW STK-R2).
- Annual-vs-quarter same `end` disambiguation → `Derive_AnnualAndQuarterShareEnd_PicksAnnual` asserts 500/80 (would be 500/25 if mixed) → verified real, would fail on regression.
- Period selection max end≤asOf then max filed → `SelectInstant` L218/224 + `BestAnnual` L245/253 → verified.
- Restated-old-beats-newer → `Derive_RestatedOldPeriodFiledAfterNewer_PicksNewerPeriod` asserts 1000/80 (would be 1000/40 under old max-filed) → strong adversarial fixture, verified.
- LatestFilingDate from submissions across all forms → `EdgarClient.ParseLatestFiling` L168, filter `filingDate>asOf` (L188), max (L193) over `recent.filingDate/form` → includes 8-K → verified by `..._IncludingEightK` (8-K carries no XBRL facts).
- Fundamentals FilingDate decoupled from issuer LatestFilingDate → `Derive` L118–122 uses `usedFilings` (debt/ebitda/interest/cash) MaxBy Filed, NOT `facts.LatestFilingDate` → verified in code; **not locked by any test** (see STK-03).

## STK Verdicts

### STK-01 — EBITDA / coverage duration coherence — **FIXED** (mainline) + new residual
- OperatingIncome now annual-only: `BestAnnual`+`IsAnnual` (FundamentalsCalculator.cs:211, 293).
- OI+D&A pinned to same accn/end: `FindSameAccnAnnual` (L173, L265, L280–282) — no longer independent.
- Interest pinned annual + same end as EBITDA: `SelectInterest` predicate `IsAnnual(f) && f.End==ebitdaAnchor.End` (L196) — the interest leg was ALSO independent before; now coherent. Credit.
- Regression test exists and is meaningful: `Derive_AnnualAndQuarterShareEnd_PicksAnnual` (would fail if the 3-mo Q4 were picked).
- Residual: the D&A leg trusts an accn+end proxy for "annual" and never checks IsAnnual → tracked as STK-R2.

### STK-02 — LatestFilingDate from submissions API — **FIXED**
- `EdgarClient.ParseLatestFiling` (EdgarClient.cs:168–201) reads `filings.recent.{filingDate,form}`, filters ≤ asOf, takes max across ALL forms (not the 4 concept tags).
- Tests real + adversarial: `GetFactsAsOf_DerivesLatestFilingFromSubmissions_IncludingEightK` (8-K after the newest 10-Q, and the 8-K has no XBRL facts so it can only come from submissions) and `..._ExcludesFutureSubmission` (as-of).

### STK-03 — leverage pinned; fundamentals FilingDate ≠ issuer LatestFilingDate — **FIXED in code / PARTIAL on tests**
- Code correct: `Derive` L118–122 stamps FilingDate/Type from the newest *used* fact, independent of `facts.LatestFilingDate` (submissions). Decoupling is real.
- Test rigor weak: NO test asserts `snapshot.FilingDate`/`FilingType`. Worse, the `Facts()` fixture stamps LatestFilingDate=2026-05-20/"8-K" while the used debt/cash facts are ALSO filed 2026-05-20 (form "10-Q") — a same-DATE collision, so only FORM differs and FORM is unasserted. A regression to `facts.LatestFilingType` would stay GREEN. Lenient fixture → does not lock the fix.

### STK-04 — max end≤asOf then max filed within — **FIXED**
- `SelectInstant` (L215–232) and `BestAnnual` (L242–263): eligible = end≤asOf ∧ filed≤asOf, then maxEnd, then MaxBy(filed) within maxEnd.
- Adversarial fixture is genuine: `Derive_RestatedOldPeriodFiledAfterNewer_PicksNewerPeriod` (restated FY2024 filed later must lose to FY2025) — would fail under old max-filed selection. Also `Derive_InstantConcept_ExcludesFactFiledAfterAsOf` proves the filed-filter is applied BEFORE maxEnd (a newer-end-but-future-filed fact does not hijack maxEnd).

## NEW Findings

### [STK-R1] Mixed-vintage TotalDebt: current + noncurrent selected on independent period-ends — Severity: **Major** (low–moderate probability, high impact, untested)
- **Target:** `Analysis/FundamentalsCalculator.cs` L140–142 (`SelectDebt`).
- **Claim under test:** the STK-01 remedy makes leverage figures come from a coherent window.
- **Reality:** `noncurrent = SelectInstant(DebtNoncurrentConcepts)` and `current = SelectInstant(DebtCurrentConcepts)` are two INDEPENDENT selections, each computing its own `maxEnd`. `total = noncurrent + (current ?? 0)` can therefore sum two DIFFERENT balance-sheet dates (e.g. latest 10-Q reports noncurrent but omits the current portion → `current` falls back to an older filing's value and is added to today's noncurrent → debt over-stated with a stale current portion). This is the STK-01 class defect on the headline Debt/EBITDA **numerator**. The filing-reconciliation at L143–146 only stamps a date; it does not make the VALUE coherent.
- **Coverage:** grep confirms NO test references `DebtCurrent`/`LongTermDebtCurrent` — the entire current-portion branch (the `+ current.Value` path) is unexecuted by the suite.
- **Fix:** require `current.End == noncurrent.End` (same balance sheet) before adding; otherwise treat current as absent. Add a test: noncurrent end=Q1'26, current only present with end=FY'25 → total == noncurrent (current dropped), not the stale sum.

### [STK-R2] D&A leg not annual-guarded — `FindSameAccnAnnual` enforces accn+end, not duration — Severity: **Major** (low probability, high impact, untested)
- **Target:** `Analysis/FundamentalsCalculator.cs` L265–283 (`FindSameAccnAnnual`); consumed at L173–174 (`SelectEbitda`).
- **Claim under test:** "Are OperatingIncome + D&A taken from the SAME period, never independently? Is there a test that would FAIL if durations were mixed again?"
- **Reality:** the OI leg is annual-guarded (`BestAnnual`/`IsAnnual`) but the D&A leg only matches `End==anchor.End && Accn==anchor.Accn`. The method is NAMED `...Annual` but never calls `IsAnnual`. A single 10-K accession can (uncommonly) carry a non-annual D&A sharing the FY end (e.g. a Q4 3-month D&A with the same accn+end) → EBITDA = 12-mo OI + 3-mo D&A → the exact STK-01 duration mix, now on the D&A leg. The OI-vs-quarter test does NOT cover this (there is no fixture with two D&A durations under one accn). So the answer to the reviewer's question is: YES for OI, **NO for D&A**.
- **Fix:** add `&& IsAnnual(f)` to the L279–282 predicate. Add a test: accn "fy25" has D&A annual (20, end 2025-12-31) AND D&A 3-mo (7, same end) → EBITDA uses 20 (Debt/EBITDA against OI+20), not OI+7.

### [STK-R3] EBITDA silently degrades to EBIT when D&A absent, no basis marker — Severity: **Minor** (pre-existing, deferred, restated)
- **Target:** `FundamentalsCalculator.cs` L174 `+ (depreciation?.Value ?? 0m)`.
- **Reality:** when D&A is genuinely absent, `DebtToEbitda`/`InterestCoverage` are computed against EBIT and mislabeled as EBITDA-based, with no `basis` field on `FundamentalSnapshot`. Understates coverage / overstates leverage silently. Acknowledged in the code comment and prior review; not regressed, but still unmarked (P1/P3 traceability).

### [STK-R4] STK-03 fixture is degenerate (test rigor) — Severity: **Minor**
- **Target:** `Tests/Analysis/FundamentalsCalculatorTests.cs` `Facts()` helper + `Derive_AnnualCoherent_ComputesRatios`.
- **Reality:** as in STK-03 above — LatestFilingDate and the newest used fact share the date 2026-05-20; FilingDate/FilingType are never asserted. Green ≠ locked.
- **Fix:** stamp `Facts()` LatestFilingDate strictly later than any used fact (e.g. 8-K 2026-06-15) and assert `snapshot.FilingDate == <newest used, 05-20>` and `snapshot.FilingType == "10-Q"` (distinct from issuer "8-K").

### [STK-R5] `DebtCurrent` in the current-portion list is broader than current-portion-of-LTD — Severity: **Minor** (scoping)
- **Target:** `FundamentalsCalculator.cs` L64 `DebtCurrentConcepts = { "LongTermDebtCurrent", "DebtCurrent" }`.
- **Reality:** `DebtCurrent` can include short-term borrowings / revolver / CP beyond the current portion of long-term debt; adding it to `LongTermDebtNoncurrent` may over-scope TotalDebt for some issuers. Ordered after the correct tag, so only a fallback, but worth a labeled decision.

### Low / nits
- `Derive` L121: if there are zero used facts AND `facts.LatestFilingDate` is null, `FilingDate` becomes `default` (DateTimeOffset.MinValue). Degenerate but a MinValue stamp rather than an explicit "no filing".
- `FindSameAccnAnnual` returns `FirstOrDefault` (no latest-filed preference) — acceptable because one accn = one filing = one filed date.

## Unverified (needs live confirmation)
- Whether real 10-K accessions in EDGAR companyfacts ever carry a non-annual D&A fact sharing the FY end (drives STK-R2 probability). Not verified against a live filing; guard is cheap regardless.
- Whether target demo issuers ever omit the current debt portion in the latest quarter while reporting it earlier (drives STK-R1 probability).

## Top 3 Won't-Run / Wrong-Number Risks
1. **STK-R1** wrong Debt/EBITDA numerator when current & noncurrent debt latest-ends diverge (untested path).
2. **STK-R2** EBITDA duration mix on the D&A leg — the STK-01 remedy is incomplete/asymmetric and unguarded.
3. **STK-R4/STK-03** the FilingDate-decoupling fix is not locked by a test; a regression ships green.

## Verdict
STK-01 **FIXED** (mainline; residual STK-R2), STK-02 **FIXED**, STK-03 **FIXED-in-code / PARTIAL-on-tests**, STK-04 **FIXED**.
The prior NO-GO cause (3-mo/12-mo mix that fires on the FIRST real issuer) is genuinely resolved for the common annual+quarterly shape. New Majors (STK-R1/R2) are edge-triggered and each closes with a one-line guard + one test.

**GO (conditional):** the EDGAR fundamentals as-of + selection spine is now real-data-correct for standard issuers; add the debt current-end coherence guard, the D&A `IsAnnual` guard, and the three missing tests (current-portion, D&A duration, FilingDate distinctness) before pointing it at un-curated issuers.
