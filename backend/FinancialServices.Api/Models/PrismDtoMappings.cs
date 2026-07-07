namespace FinancialServices.Api.Models;

/// <summary>
/// Pure domain-record → wire-DTO projections (arch-09). No I/O, no business logic — just shape
/// mapping, kept in one place so the REST contract and the frontend types stay in lock-step.
/// </summary>
public static class PrismDtoMappings
{
    /// <summary>Projects an <see cref="Issuer"/> plus its computed coverage into the list/detail DTO.</summary>
    public static IssuerListItem ToListItem(this Issuer issuer, IReadOnlyList<Analysis.Provider> coverage) =>
        new(issuer.IssuerId, issuer.LegalName, issuer.Ticker, issuer.Cik, issuer.Sector,
            issuer.SampleBondIsin, coverage);

    /// <summary>Projects a full <see cref="ReconciliationDossier"/> into the response body.</summary>
    public static DossierResponse ToResponse(this ReconciliationDossier dossier) =>
        new(
            dossier.Id,
            dossier.IssuerId,
            dossier.AsOfDate,
            dossier.Ratings.Select(ToVerdictDto).ToArray(),
            dossier.Divergences.Select(ToDivergenceDto).ToArray(),
            dossier.Flags.Select(ToFlagDto).ToArray(),
            dossier.ConsensusSummary,
            dossier.ConfidenceScore);

    /// <summary>Projects one provider rating into its verdict DTO (reused by the pkg-07 stream events).</summary>
    public static ProviderVerdictDto ToVerdictDto(this ProviderRating r) =>
        new(r.Provider, r.Letter, r.Notch, r.AsOfDate, r.InputAsOfDate, r.MethodologyDocId);

    /// <summary>Projects one decomposed pair divergence into its DTO (reused by the pkg-07 stream events).</summary>
    public static PairDivergenceDto ToDivergenceDto(this PairDivergence d) =>
        new(d.A, d.B, d.NotchGap, d.Attribution.Select(ToBucketDto).ToArray());

    /// <summary>Projects one attribution bucket into its DTO.</summary>
    public static BucketAttributionDto ToBucketDto(this BucketAttribution b) =>
        new(b.Bucket, b.Notches, b.Explanation, b.EvidenceRefs);

    /// <summary>Projects one red flag into its DTO (reused by the pkg-07 stream events).</summary>
    public static RedFlagDto ToFlagDto(this RedFlag f) =>
        new(f.Code, f.Severity, f.Rule, f.Narrative, f.EvidenceRefs);
}
