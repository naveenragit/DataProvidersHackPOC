using FinancialServices.Api.Analysis;
using FinancialServices.Api.Models;

namespace FinancialServices.Api.Connectors;

/// <summary>
/// Raw provider-ratings record straight from a source (D12/D13). Kept deliberately separate from the
/// domain <see cref="ProviderRating"/> (pkg 02): this carries the source's real
/// <see cref="RatingActionDate"/> + <see cref="SourceRef"/>; the reconciliation model carries
/// <c>InputAsOfDate</c> + <c>MethodologyDocId</c>. The merge happens in pkg 05.
/// </summary>
public sealed record ProviderRatingRecord(
    Provider Provider,
    string IssuerId,
    string Letter,
    int Notch,
    DateTimeOffset AsOfDate,
    DateTimeOffset RatingActionDate,
    IReadOnlyList<RatingFactor> Factors,
    string SourceRef)
{
    /// <summary>
    /// The single, tested conversion to the domain <see cref="ProviderRating"/> (pkg 05 must go through
    /// this so no field is silently dropped). Field mapping: <see cref="RatingActionDate"/> →
    /// <c>InputAsOfDate</c> (the date that drives the stale-input flag, P3) and <see cref="SourceRef"/>
    /// → <c>MethodologyDocId</c> (the citation ref carried through). <see cref="IssuerId"/> has no
    /// target because <see cref="ProviderRating"/> is nested under a dossier that already carries it.
    /// </summary>
    public ProviderRating ToProviderRating() =>
        new(
            Provider: Provider,
            Letter: Letter,
            Notch: Notch,
            AsOfDate: AsOfDate,
            InputAsOfDate: RatingActionDate,
            Factors: Factors,
            MethodologyDocId: SourceRef);
}

/// <summary>
/// Source-agnostic provider-ratings boundary (architecturalPlan/02 Connectors). The decomposer
/// (pkg 05) and provider agents (pkg 06) depend on this, not on a concrete API.
/// </summary>
public interface IProviderRatingsSource
{
    /// <summary>Which provider this source speaks for.</summary>
    Provider Provider { get; }

    /// <summary>
    /// The provider's rating as of <paramref name="asOf"/>, enforcing
    /// <c>ratingActionDate &lt;= asOf</c> (P3). Returns <c>null</c> on no coverage →
    /// <c>MISSING_COVERAGE</c> (P1 — never fabricate a rating); <c>null</c> also covers a provider
    /// whose real API integration is not yet configured, so a parallel sweep degrades gracefully
    /// rather than faulting (ARC-03).
    /// </summary>
    Task<ProviderRatingRecord?> GetRatingAsOfAsync(string issuerId, DateTimeOffset asOf, CancellationToken ct);
}
