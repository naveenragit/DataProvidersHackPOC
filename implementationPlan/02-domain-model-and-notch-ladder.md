# 02 — Domain Model & Notch Ladder

**Purpose:** the C# records every other package shares, plus the **canonical notch ladder** that
makes "3-notch gap" precise and auditable. This is deterministic infrastructure — no LLM.

**Depends on:** 01. **Blocks:** 03, 05, 06, 08.

Create under `backend/FinancialServices.Api/Models/` and `.../Analysis/`.

---

## A. Rating scales & the notch ladder

Providers publish on different scales. Map them all to a single integer ladder (1 = best `AAA/Aaa`,
21 = `C`, higher = weaker). This is standard rating-scale mapping.

`Analysis/NotchLadder.cs`:

```csharp
namespace FinancialServices.Api.Analysis;

public enum Provider { Bloomberg, MorningstarDbrs, Msci }

public static class NotchLadder
{
    // Canonical long-term ladder (S&P/Fitch style anchor). 1 = AAA … 21 = C.
    private static readonly string[] Canonical =
    {
        "AAA","AA+","AA","AA-","A+","A","A-","BBB+","BBB","BBB-",
        "BB+","BB","BB-","B+","B","B-","CCC+","CCC","CCC-","CC","C"
    };

    // Provider-specific label → canonical notch. DBRS uses "(high)/(low)"; Moody's uses Aaa/Aa1…
    private static readonly Dictionary<string,int> Map = BuildMap();

    public static int ToNotch(string label) =>
        Map.TryGetValue(Normalize(label), out var n) ? n
        : throw new ArgumentOutOfRangeException(nameof(label), $"Unknown rating '{label}'");

    public static string ToLabel(int notch) => Canonical[Math.Clamp(notch, 1, 21) - 1];

    /// <summary>Signed notch gap b − a (positive = b is weaker/lower than a).</summary>
    public static int Gap(string a, string b) => ToNotch(b) - ToNotch(a);
    public static bool CrossesIgHyBoundary(string a, string b) =>
        (ToNotch(a) <= 10) != (ToNotch(b) <= 10); // BBB- (10) is the IG floor

    private static string Normalize(string s) => s.Trim().ToUpperInvariant();
    private static Dictionary<string,int> BuildMap() { /* Canonical + DBRS + Moody's aliases → notch */ }
}
```

Include, in `BuildMap`, alias rows for: **DBRS** (`A (HIGH)`→A+, `A`→A, `A (LOW)`→A-, etc.),
**Moody's** (`AAA`→1, `AA1`→2, `AA2`→3 …) so redistributed agency grades resolve. Unit-test the map.

---

## B. Core records

`Models/PrismModels.cs`:

```csharp
public sealed record RatingFactor(
    string Name,          // "Leverage", "InterestCoverage", "Profitability", "Liquidity",
                          // "BusinessRisk", "SectorEsgOverlay"
    decimal Weight,       // 0..1, sums to ~1 per provider
    decimal Score,        // 0..100 sub-score
    string SourceRef);    // AI Search doc id / EDGAR fact id — for citation

public sealed record ProviderRating(
    Provider Provider,
    string Letter,                 // provider's native label, e.g. "A (low)"
    int Notch,                     // NotchLadder.ToNotch(Letter)
    DateTimeOffset AsOfDate,
    DateTimeOffset InputAsOfDate,  // date of the financials the rating is built on (drives stale flag)
    IReadOnlyList<RatingFactor> Factors,
    string MethodologyDocId);

public sealed record Issuer(
    string IssuerId, string LegalName, string Ticker, string Cik,
    string Sector, string SampleBondIsin);

public sealed record FundamentalSnapshot(         // from EDGAR (package 04)
    string IssuerId, DateTimeOffset FilingDate, string FilingType,
    decimal? DebtToEbitda, decimal? InterestCoverage, decimal? CashAndEquivalents);

public sealed record BucketAttribution(
    string Bucket,        // "Weighting" | "Input" | "MethodologyAdjustment"
    decimal Notches,      // signed contribution to the pair gap
    string Explanation,   // filled by LLM narrator, cites SourceRef
    IReadOnlyList<string> EvidenceRefs);

public sealed record PairDivergence(
    Provider A, Provider B, int NotchGap,
    IReadOnlyList<BucketAttribution> Attribution);

public sealed record RedFlag(
    string Code,          // "STALE_INPUT" | "MISSING_COVERAGE" | "OUTLIER_PROVIDER" | "METHODOLOGY_CONFLICT"
    string Severity,      // "high" | "medium" | "low"
    string Rule,          // human-readable deterministic rule text, shown in the UI
    string Narrative,     // LLM narration
    IReadOnlyList<string> EvidenceRefs);

public sealed record ReconciliationDossier(
    string Id, string IssuerId, DateTimeOffset AsOfDate,
    IReadOnlyList<ProviderRating> Ratings,
    FundamentalSnapshot? Fundamentals,
    IReadOnlyList<PairDivergence> Divergences,
    IReadOnlyList<RedFlag> Flags,
    string ConsensusSummary,        // "consensus within 1 notch" | "3-notch split"
    double ConfidenceScore,         // 0..1, deterministic from coverage + freshness
    DateTimeOffset GeneratedAt);
```

DTOs for the API (package 08) are separate from these domain records.

---

## Acceptance for this package
- [ ] `NotchLadder.ToNotch` round-trips every provider label used by the corpus (package 03).
- [ ] `NotchLadder.Gap("A (low)","BBB-")` returns the expected signed integer.
- [ ] xUnit: notch map covers DBRS + Moody's aliases; IG/HY boundary test passes.
- [ ] All records compile with nullable enabled + warnings-as-errors.
