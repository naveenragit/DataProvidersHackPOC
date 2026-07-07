using System.Text.Json.Serialization;
using FinancialServices.Api.Analysis;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;

namespace FinancialServices.Api.Services;

/// <summary>
/// One row of the <c>prism-ratings</c> index, deserialized from a Search hit. Property names are
/// pinned to the index field names (camelCase) with <see cref="JsonPropertyNameAttribute"/> so the
/// mapping is serializer-policy independent. Issuer-only fields (added to the index in pkg 08) are
/// empty on <c>ratingCard</c> rows and vice-versa.
/// </summary>
public sealed class SearchCorpusRow
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("docType")] public string DocType { get; init; } = "";
    [JsonPropertyName("issuerId")] public string IssuerId { get; init; } = "";
    [JsonPropertyName("provider")] public string Provider { get; init; } = "";
    [JsonPropertyName("letter")] public string Letter { get; init; } = "";
    [JsonPropertyName("notch")] public int Notch { get; init; }
    [JsonPropertyName("dataClass")] public string DataClass { get; init; } = "";
    [JsonPropertyName("asOfDate")] public DateTimeOffset AsOfDate { get; init; }
    [JsonPropertyName("inputAsOfDate")] public DateTimeOffset InputAsOfDate { get; init; }

    // Issuer-only metadata + filing boundary (pkg-08 index extension; empty on ratingCard rows).
    [JsonPropertyName("legalName")] public string LegalName { get; init; } = "";
    [JsonPropertyName("ticker")] public string Ticker { get; init; } = "";
    [JsonPropertyName("cik")] public string Cik { get; init; } = "";
    [JsonPropertyName("sector")] public string Sector { get; init; } = "";
    [JsonPropertyName("sampleBondIsin")] public string SampleBondIsin { get; init; } = "";
    [JsonPropertyName("latestFilingDate")] public DateTimeOffset? LatestFilingDate { get; init; }
    [JsonPropertyName("filingType")] public string FilingType { get; init; } = "";
}

/// <summary>
/// A corpus issuer plus the deterministic reconciliation boundary derived from its doc: the latest
/// filing date/type (the as-of boundary the stale-input flag measures against) and the set of
/// providers that publish a rating card for it.
/// </summary>
public sealed record IssuerCorpusEntry(
    Issuer Issuer,
    DateTimeOffset LatestFilingDate,
    string FilingType,
    IReadOnlyList<Provider> Coverage);

/// <summary>
/// Pure projections from the <c>prism-ratings</c> corpus rows to Prism domain records (P2 — no I/O).
/// The notch is always recomputed from the letter via <see cref="NotchLadder"/> (the ladder is the
/// single source of truth, not the stored value). Isolated from the SDK so the money-moment mapping
/// is unit-testable without a live index.
/// </summary>
public static class SearchCorpusMapper
{
    /// <summary>
    /// Maps a <c>ratingCard</c> row to a domain <see cref="ProviderRating"/>. The card's
    /// <c>asOfDate</c> (the <b>rating-action date</b>) becomes <see cref="ProviderRating.InputAsOfDate"/>
    /// — the freshness boundary the stale-input flag compares to the issuer's latest filing (matches
    /// pkg-04 <c>RatingActionDate → InputAsOfDate</c>). The card's own <c>inputAsOfDate</c> (the
    /// financials <i>period-end</i>) is deliberately NOT used: it precedes the filing date for every
    /// provider and would false-flag them all. Factors are not projected into the index, so the rating
    /// is letter-only (empty factors) — the divergence is then honestly residual-dominated (pkg 05).
    /// </summary>
    public static ProviderRating ToProviderRating(SearchCorpusRow card) =>
        new(
            Provider: ParseProvider(card.Provider),
            Letter: card.Letter,
            Notch: NotchLadder.ToNotch(card.Letter),
            AsOfDate: card.AsOfDate,
            InputAsOfDate: card.AsOfDate,
            Factors: Array.Empty<RatingFactor>(),
            MethodologyDocId: card.Id);

    /// <summary>
    /// Filters rating-card rows to actions on or before <paramref name="asOf"/> (as-of correctness,
    /// P3 — no hindsight), maps each to a <see cref="ProviderRating"/>, and orders by provider. Pure,
    /// so the as-of gate is unit-testable without a live index.
    /// </summary>
    public static IReadOnlyList<ProviderRating> MapCards(IEnumerable<SearchCorpusRow> cards, DateTimeOffset asOf) =>
        cards
            .Where(card => card.AsOfDate <= asOf)
            .Select(ToProviderRating)
            .OrderBy(rating => (int)rating.Provider)
            .ToArray();

    /// <summary>Maps an <c>issuer</c> row + its computed coverage to an <see cref="IssuerCorpusEntry"/>.</summary>
    public static IssuerCorpusEntry ToIssuerEntry(SearchCorpusRow issuerRow, IReadOnlyList<Provider> coverage)
    {
        var issuer = new Issuer(
            issuerRow.IssuerId, issuerRow.LegalName, issuerRow.Ticker, issuerRow.Cik,
            issuerRow.Sector, issuerRow.SampleBondIsin);

        // The filing boundary drives the stale-input flag; fall back to the doc's asOfDate if the
        // (pkg-08) latestFilingDate field is not yet populated in the live index.
        DateTimeOffset filingBoundary = issuerRow.LatestFilingDate ?? issuerRow.AsOfDate;
        string filingType = string.IsNullOrWhiteSpace(issuerRow.FilingType) ? "filing" : issuerRow.FilingType;

        return new IssuerCorpusEntry(issuer, filingBoundary, filingType, coverage);
    }

    /// <summary>
    /// Parses a provider label to the <see cref="Provider"/> enum. An unrecognized value in the corpus
    /// is bad upstream data — fail loud (P1) as an upstream fault, never silently drop the row.
    /// </summary>
    public static Provider ParseProvider(string value) =>
        Enum.TryParse(value, ignoreCase: true, out Provider provider)
            ? provider
            : throw new UpstreamServiceException(
                "Search", $"Unrecognized provider '{value}' in the ratings corpus.");
}
