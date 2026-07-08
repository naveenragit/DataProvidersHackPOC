using FinancialServices.Api.Analysis;

namespace FinancialServices.Api.Models;

/// <summary>
/// A provider's forward-looking rating outlook (direction, not level). Standard agency designation
/// that rides alongside the grade; <see cref="Unknown"/> = not supplied by the source (no fabrication).
/// </summary>
public enum RatingOutlook
{
    Unknown,
    Positive,
    Stable,
    Negative,
    Developing
}

/// <summary>One weighted rating factor with its sub-score and citation reference.</summary>
public sealed record RatingFactor(
    string Name,          // "Leverage", "InterestCoverage", "Profitability", "Liquidity",
                          // "BusinessRisk", "SectorEsgOverlay"
    decimal Weight,       // 0..1, sums to ~1 per provider
    decimal Score,        // 0..100 sub-score
    string SourceRef);    // AI Search doc id / EDGAR fact id — for citation

/// <summary>A single provider's rating for an issuer, with the factors and as-of dates behind it.</summary>
public sealed record ProviderRating(
    Provider Provider,
    string Letter,                 // provider's native label, e.g. "A (low)"
    int Notch,                     // NotchLadder.ToNotch(Letter)
    DateTimeOffset AsOfDate,
    DateTimeOffset InputAsOfDate,  // date of the financials the rating is built on (drives stale flag)
    IReadOnlyList<RatingFactor> Factors,
    string MethodologyDocId,
    RatingOutlook Outlook = RatingOutlook.Unknown, // forward-looking direction (optional; Unknown = absent)
    bool UnderReview = false);     // on CreditWatch / under review — an imminent-change indicator

/// <summary>A corporate bond issuer in the reconciliation cast.</summary>
public sealed record Issuer(
    string IssuerId, string LegalName, string Ticker, string Cik,
    string Sector, string SampleBondIsin);

/// <summary>Point-in-time issuer fundamentals from EDGAR (nullable fields = genuinely absent data).</summary>
public sealed record FundamentalSnapshot(         // from EDGAR (package 04)
    string IssuerId, DateTimeOffset FilingDate, string FilingType,
    decimal? DebtToEbitda, decimal? InterestCoverage, decimal? CashAndEquivalents);

/// <summary>One attribution bucket's signed contribution to a provider pair's notch gap.</summary>
public sealed record BucketAttribution(
    string Bucket,        // "Weighting" | "Input" | "MethodologyAdjustment"
    decimal Notches,      // signed contribution to the pair gap
    string Explanation,   // filled by LLM narrator, cites SourceRef
    IReadOnlyList<string> EvidenceRefs);

/// <summary>The notch gap between two providers, decomposed into attribution buckets.</summary>
public sealed record PairDivergence(
    Provider A, Provider B, int NotchGap,
    IReadOnlyList<BucketAttribution> Attribution);

/// <summary>A deterministic red flag with its verbatim rule text and supporting evidence.</summary>
public sealed record RedFlag(
    string Code,          // "STALE_INPUT" | "IG_HY_BOUNDARY" | "MISSING_COVERAGE" | "OUTLIER_PROVIDER"
                          // | "PROVIDER_UNDER_REVIEW" | "METHODOLOGY_CONFLICT"
    string Severity,      // "high" | "medium" | "low"
    string Rule,          // human-readable deterministic rule text, shown in the UI
    string Narrative,     // LLM narration
    IReadOnlyList<string> EvidenceRefs);

/// <summary>The full reconciliation result for one issuer: ratings, divergences, flags, and consensus.</summary>
public sealed record ReconciliationDossier(
    string Id, string IssuerId, DateTimeOffset AsOfDate,
    IReadOnlyList<ProviderRating> Ratings,
    FundamentalSnapshot? Fundamentals,
    IReadOnlyList<PairDivergence> Divergences,
    IReadOnlyList<RedFlag> Flags,
    string ConsensusSummary,        // "consensus within 1 notch" | "3-notch split"
    double ConfidenceScore,         // 0..1, deterministic from coverage + freshness
    DateTimeOffset GeneratedAt);
