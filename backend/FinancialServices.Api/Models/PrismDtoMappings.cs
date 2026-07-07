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
            dossier.Ratings.Select(ToVerdict).ToArray(),
            dossier.Divergences.Select(ToDivergence).ToArray(),
            dossier.Flags.Select(ToFlag).ToArray(),
            dossier.ConsensusSummary,
            dossier.ConfidenceScore);

    private static ProviderVerdictDto ToVerdict(ProviderRating r) =>
        new(r.Provider, r.Letter, r.Notch, r.AsOfDate, r.InputAsOfDate, r.MethodologyDocId);

    private static PairDivergenceDto ToDivergence(PairDivergence d) =>
        new(d.A, d.B, d.NotchGap, d.Attribution.Select(ToBucket).ToArray());

    private static BucketAttributionDto ToBucket(BucketAttribution b) =>
        new(b.Bucket, b.Notches, b.Explanation, b.EvidenceRefs);

    private static RedFlagDto ToFlag(RedFlag f) =>
        new(f.Code, f.Severity, f.Rule, f.Narrative, f.EvidenceRefs);
}
