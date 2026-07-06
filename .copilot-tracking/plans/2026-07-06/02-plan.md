<!-- markdownlint-disable-file -->
# Implementation Plan: Package 02 — Domain Model & Notch Ladder

**Work Package:** `implementationPlan/02-domain-model-and-notch-ladder.md`
**Governing standards:** `architecturalPlan/00-core-principles.md` (esp. P2 deterministic core, P4 language),
`01-naming-conventions.md`, `02-folder-structure.md`, `03-error-handling-and-propagation.md`,
`11-testing-and-quality.md`
**Financial Domain:** Capital Markets — corporate bond credit-rating reconciliation
**Azure Services:** NONE (pure C#, no LLM, no network, no new NuGet packages) — honors **P2**

---

## Package acceptance (source of truth — every task maps back to one of these)

- **A1** — `NotchLadder.ToNotch` round-trips every provider label used by the corpus.
- **A2** — `NotchLadder.Gap("A (low)","BBB-")` returns the expected signed integer (**+3**).
- **A3** — xUnit: notch map covers DBRS + Moody's aliases; IG/HY boundary test passes.
- **A4** — All records compile with nullable enabled + warnings-as-errors.
- **T1** — (`11-testing` must-have #1) every provider label maps incl. DBRS `(high)/(low)` + Moody's;
  IG/HY at `BBB-`; `Gap` sign correctness.

---

## Decisions (D1–D9) — ★ = needs your confirmation

| # | Decision | Choice | Rationale |
|---|---|---|---|
| D1 | Where `Provider` enum lives | In `Analysis/NotchLadder.cs` (second public type in file) | Package source + scope item 1 place it there; tiny enum tightly bound to the ladder. Minor, deliberate exception to "one type per file". |
| D2 | DBRS `(mid)` meaning ★ | `X (mid)` ≡ bare `X` (e.g. `A (mid)` → notch 6) | DBRS's middle grade is the bare letter; `(mid)` is an explicit alias for it. Confirm you want `(mid)` accepted (real DBRS labels omit it). |
| D3 | Moody's → S&P-anchored ladder | 1:1 by rank: `Aaa`→1, `Aa1`→2, `Aa2`→3, `Aa3`→4, `A1`→5 … `Caa3`→19, `Ca`→20, `C`→21 | Standard agency cross-walk; Moody's has 21 long-term grades matching the canonical 21 notches exactly. |
| D4 | Benign duplicate keys (`AAA`, `CC`, `C` appear in >1 alias set) | `BuildMap` uses **indexer assignment** `map[k]=n`, not `.Add()` | Overlaps are value-consistent; indexer is idempotent and avoids `ArgumentException` on duplicate `.Add`. |
| D5 | Dictionary comparer | `new Dictionary<string,int>(StringComparer.Ordinal)`; keys stored pre-normalized (UPPER, single space) | `ToNotch` already `Normalize()`s input to `ToUpperInvariant()`; ordinal match is deterministic + fast. |
| D6 | Whitespace robustness ★ | Keep package's `Normalize` = `Trim().ToUpperInvariant()` only; store DBRS keys with exactly one space (`"A (LOW)"`). Corpus must use single-space DBRS labels. | Avoids scope creep. Confirm you don't want extra tolerance for `A(low)` / `A  (low)`. (Fallback: collapse internal whitespace in `Normalize`.) |
| D7 | Unknown-label exception type | `ArgumentOutOfRangeException` (per package signature), **not** `DomainInvariantException` | Unknown label = caller argument error, not a broken internal invariant. `Infrastructure/Errors/` taxonomy (03) is introduced in a later package; 02 stays BCL-only, zero deps. |
| D8 | `ToLabel` out-of-range input | Clamp to 1–21 (package uses `Math.Clamp`), never throws | Defensive per package code; `ToLabel(0)`→`AAA`, `ToLabel(99)`→`C`. |
| D9 | Round-trip scope | Label→notch→label identity holds only for **canonical** S&P labels; alias→notch returns canonical label (not the alias) via `ToLabel` | `ToLabel` knows only the canonical array by design; tests assert alias→notch, and canonical round-trip identity separately. |

---

## Phase 1 — Analysis (deterministic core, `Analysis/NotchLadder.cs`)

- [x] **1.1** Create folder `backend/FinancialServices.Api/Analysis/` and file `NotchLadder.cs` with
  `namespace FinancialServices.Api.Analysis;`. *(folder per `02-folder-structure`; deterministic core P2)*
- [x] **1.2** Add `public enum Provider { Bloomberg, MorningstarDbrs, Msci }` at top of `NotchLadder.cs`.
  *(D1; naming `Provider.MorningstarDbrs` per `01-naming`)*
- [x] **1.3** Add `public static class NotchLadder` with the private `Canonical` string[21]
  (`AAA`…`C`, 1-indexed via `[notch-1]`). *(anchors A1/A2/A3)*
- [x] **1.4** Implement `Normalize(string) => s.Trim().ToUpperInvariant()`. *(D6 — package-exact)*
- [x] **1.5** Implement `ToNotch(string label)`: `Map.TryGetValue(Normalize(label), …)` else
  `throw new ArgumentOutOfRangeException(nameof(label), $"Unknown rating '{label}'")`. *(A1, D7)*
- [x] **1.6** Implement `ToLabel(int notch) => Canonical[Math.Clamp(notch,1,21)-1]`. *(D8)*
- [x] **1.7** Implement `Gap(string a, string b) => ToNotch(b) - ToNotch(a)` with `<summary>` noting
  "positive = b weaker/lower than a". *(A2, T1 Gap sign)*
- [x] **1.8** Implement `CrossesIgHyBoundary(a,b) => (ToNotch(a) <= 10) != (ToNotch(b) <= 10)` with comment
  "BBB- (10) is the IG floor". *(A3, T1 IG/HY)*
- [x] **1.9** Implement `BuildMap()` step 1 — seed canonical: loop `Canonical`, `map[Canonical[i]] = i+1`
  into `Dictionary<string,int>(StringComparer.Ordinal)`. *(A1; D4/D5)*
- [x] **1.10** `BuildMap()` step 2 — DBRS aliases: for each family base notch
  `{AA:3, A:6, BBB:9, BB:12, B:15, CCC:18}` set `map["{F} (HIGH)"]=base-1`, `map["{F} (MID)"]=base`,
  `map["{F} (LOW)"]=base+1`. *(A3 DBRS; D2)*
- [x] **1.11** `BuildMap()` step 3 — Moody's aliases (indexer assignment, D4): `AAA`→1,`AA1`→2,`AA2`→3,
  `AA3`→4,`A1`→5,`A2`→6,`A3`→7,`BAA1`→8,`BAA2`→9,`BAA3`→10,`BA1`→11,`BA2`→12,`BA3`→13,`B1`→14,`B2`→15,
  `B3`→16,`CAA1`→17,`CAA2`→18,`CAA3`→19,`CA`→20,`C`→21 (keys are post-Normalize UPPER). *(A3 Moody's; D3)*
- [x] **1.12** Wire `private static readonly Dictionary<string,int> Map = BuildMap();`. Confirm no
  `DomainInvariantException`/`Infrastructure` reference is introduced (BCL only). *(D7, P2)*

**Canonical notch table (reference for 1.3 / tests):**
`1 AAA · 2 AA+ · 3 AA · 4 AA- · 5 A+ · 6 A · 7 A- · 8 BBB+ · 9 BBB · 10 BBB- (IG floor) · 11 BB+ ·
12 BB · 13 BB- · 14 B+ · 15 B · 16 B- · 17 CCC+ · 18 CCC · 19 CCC- · 20 CC · 21 C`

---

## Phase 2 — Models (domain records, `Models/PrismModels.cs`)

- [x] **2.1** Create folder `backend/FinancialServices.Api/Models/` and file `PrismModels.cs` with
  `namespace FinancialServices.Api.Models;` and `using FinancialServices.Api.Analysis;` (for `Provider`).
  *(A4; single-file domain records per `02-folder-structure`)*
- [x] **2.2** Add `public sealed record RatingFactor(string Name, decimal Weight, decimal Score, string SourceRef);`
  — keep provenance comment on `SourceRef`. *(A4; P3 citation)*
- [x] **2.3** Add `public sealed record ProviderRating(Provider Provider, string Letter, int Notch,
  DateTimeOffset AsOfDate, DateTimeOffset InputAsOfDate, IReadOnlyList<RatingFactor> Factors,
  string MethodologyDocId);` — comment `InputAsOfDate` "drives stale flag". *(A4; P3 as-of)*
- [x] **2.4** Add `public sealed record Issuer(string IssuerId, string LegalName, string Ticker, string Cik,
  string Sector, string SampleBondIsin);`. *(A4)*
- [x] **2.5** Add `public sealed record FundamentalSnapshot(string IssuerId, DateTimeOffset FilingDate,
  string FilingType, decimal? DebtToEbitda, decimal? InterestCoverage, decimal? CashAndEquivalents);`
  — nullable decimals are intentional (absent XBRL field = data, not error, per 03). *(A4)*
- [x] **2.6** Add `public sealed record BucketAttribution(string Bucket, decimal Notches, string Explanation,
  IReadOnlyList<string> EvidenceRefs);` — `Bucket` ∈ `Weighting|Input|MethodologyAdjustment`. *(A4; naming 01)*
- [x] **2.7** Add `public sealed record PairDivergence(Provider A, Provider B, int NotchGap,
  IReadOnlyList<BucketAttribution> Attribution);`. *(A4)*
- [x] **2.8** Add `public sealed record RedFlag(string Code, string Severity, string Rule, string Narrative,
  IReadOnlyList<string> EvidenceRefs);` — `Code` ∈ `STALE_INPUT|MISSING_COVERAGE|OUTLIER_PROVIDER|METHODOLOGY_CONFLICT`.
  *(A4; naming 01; P3)*
- [x] **2.9** Add `public sealed record ReconciliationDossier(string Id, string IssuerId,
  DateTimeOffset AsOfDate, IReadOnlyList<ProviderRating> Ratings, FundamentalSnapshot? Fundamentals,
  IReadOnlyList<PairDivergence> Divergences, IReadOnlyList<RedFlag> Flags, string ConsensusSummary,
  double ConfidenceScore, DateTimeOffset GeneratedAt);` — `Fundamentals` nullable by design. *(A4)*
- [x] **2.10** Confirm no P4-forbidden vocabulary (buy/sell/hold/recommend/allocate/trade/alpha/signal)
  in any comment or member name. *(P4)*

---

## Phase 3 — Tests (`FinancialServices.Tests/Analysis/NotchLadderTests.cs`)

- [x] **3.1** Create folder `backend/FinancialServices.Tests/Analysis/` and file `NotchLadderTests.cs`,
  `namespace FinancialServices.Tests.Analysis;`, `using FinancialServices.Api.Analysis;`,
  `sealed class NotchLadderTests`. *(11-testing tooling; T1)*
- [x] **3.2** `[Theory]` over all 21 canonical labels → `ToNotch(label) == expectedNotch` **and**
  `ToLabel(expectedNotch) == label` (canonical round-trip). *(A1; D9)*
- [x] **3.3** `[Theory]` DBRS aliases → expected notch, incl.
  `("AA (high)",2)`,`("AA (mid)",3)`,`("AA (low)",4)`,`("A (high)",5)`,`("A (low)",7)`,`("BBB (low)",10)`,
  `("BB (high)",11)`,`("B (low)",16)`,`("CCC (low)",19)`. *(A3 DBRS; T1; D2)*
- [x] **3.4** `[Theory]` Moody's aliases (mixed-case input to prove `Normalize`) →
  `("Aaa",1)`,`("Aa1",2)`,`("Aa2",3)`,`("Aa3",4)`,`("A1",5)`,`("A2",6)`,`("A3",7)`,`("Baa1",8)`,`("Baa2",9)`,
  `("Baa3",10)`,`("Ba1",11)`,`("B1",14)`,`("Caa1",17)`,`("Ca",20)`,`("C",21)`. *(A3 Moody's; T1; D3)*
- [x] **3.5** `[Fact]` trim/case tolerance: `ToNotch("  a (low)  ") == 7`. *(A1; D6)*
- [x] **3.6** `[Fact]` `Gap` sign: `Gap("A (low)","BBB-") == 3`; `Gap("BBB-","A (low)") == -3`;
  `Gap("A","A") == 0`. *(**A2**; T1 Gap sign)*
- [x] **3.7** `[Fact]` IG/HY boundary: `ToNotch("BBB-") == 10`; `CrossesIgHyBoundary("BBB-","BB+")` true;
  `CrossesIgHyBoundary("A","BBB")` false; `CrossesIgHyBoundary("A (low)","BBB-")` false (both IG). *(A3; T1)*
- [x] **3.8** `[Theory]` `ToLabel` clamp: `1→"AAA"`, `10→"BBB-"`, `21→"C"`, `0→"AAA"`, `99→"C"`. *(D8)*
- [x] **3.9** `[Theory]` unknown/blank labels throw:
  `ToNotch("ZZZ")`, `ToNotch("")`, `ToNotch("A+X")` → `.Should().Throw<ArgumentOutOfRangeException>()`. *(D7)*

---

## Phase 4 — Build & quality gate

- [x] **4.1** `get_errors` on all three new files — zero diagnostics under `TreatWarningsAsErrors`. *(A4)*
- [x] **4.2** `dotnet build backend/FinancialServices.sln` — clean (nullable + warnings-as-errors). *(A4)*
- [x] **4.3** `dotnet test backend/FinancialServices.sln` — all `NotchLadderTests` pass alongside the
  existing `ScaffoldTests`. *(A1, A2, A3, T1)*

---

## Mandatory planner phases marked N/A (deliberate, not skipped)

| Mode phase | Status | Reason |
|---|---|---|
| Frontend types/hooks/components/pages | **N/A** | Package 02 ships no UI; DTO mirroring happens in package 08/09. |
| **Workflow Visualization** | **Deferred (N/A here)** | 02 adds no runtime node/agent/service. The deterministic engines that become workflow nodes (`divergence-decomposer`, `red-flag-engine`) are introduced in packages **05/06**, which own the WorkflowPage updates. Recorded so this required phase is consciously addressed. |
| Architecture Page / Settings | **N/A** | No new Azure service or config surface in 02. |
| Security & Compliance (Content Safety / audit / PII) | **N/A** | No user input, no persistence, no logging path in 02 (pure functions + records). |

---

## Acceptance mapping

| Acceptance | Satisfied by tasks |
|---|---|
| A1 (ToNotch round-trips corpus labels) | 1.5, 1.9–1.11, 3.2–3.5 |
| A2 (`Gap("A (low)","BBB-")` = +3) | 1.7, 3.6 |
| A3 (DBRS + Moody's aliases; IG/HY) | 1.8, 1.10–1.11, 3.3, 3.4, 3.7 |
| A4 (records compile nullable + WAE) | 2.1–2.9, 4.1–4.2 |
| T1 (labels map; IG/HY at BBB-; Gap sign) | 3.2–3.7 |

---

## File change summary

| File | Action | Description |
|---|---|---|
| `backend/FinancialServices.Api/Analysis/NotchLadder.cs` | Create | `Provider` enum + `NotchLadder` static class (canonical 21-notch ladder, `ToNotch`/`ToLabel`/`Gap`/`CrossesIgHyBoundary`, `BuildMap` with DBRS + Moody's aliases). Deterministic, BCL-only. |
| `backend/FinancialServices.Api/Models/PrismModels.cs` | Create | 8 sealed domain records: `RatingFactor`, `ProviderRating`, `Issuer`, `FundamentalSnapshot`, `BucketAttribution`, `PairDivergence`, `RedFlag`, `ReconciliationDossier`. |
| `backend/FinancialServices.Tests/Analysis/NotchLadderTests.cs` | Create | xUnit + FluentAssertions matrix: canonical round-trip, DBRS/Moody's aliases, trim/case, `Gap` sign, IG/HY boundary, `ToLabel` clamp, unknown-label throws. |

**No edits** to existing csproj/Program.cs/appsettings — package 02 needs no new packages or wiring.
