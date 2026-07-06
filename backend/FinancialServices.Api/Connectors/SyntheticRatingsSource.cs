using FinancialServices.Api.Analysis;
using FinancialServices.Api.Infrastructure.Telemetry;
using FinancialServices.Api.Models;

namespace FinancialServices.Api.Connectors;

/// <summary>
/// SYNTHETIC / DEV — the labeled-synthetic <see cref="Provider.Msci"/> slot (MSCI has no hackathon
/// API). This is a <b>clearly labeled</b> synthetic source, <b>not</b> a silent runtime fake of a
/// real provider (P1): every record's <c>SourceRef</c> is prefixed <c>synthetic:</c> so provenance
/// is unmistakable. It doubles as the offline/dev source and is seeded from an in-code fixture; the
/// AI Search-backed corpus (pkg 03) supersedes this fixture once wired.
/// </summary>
public sealed class SyntheticRatingsSource : IProviderRatingsSource
{
    // Keyed by lowercase issuerId. RatingActionDate is deliberately older than the fundamentals'
    // filing date so the stale-input red flag (pkg 05) has a real, honest divergence to fire on.
    private static readonly IReadOnlyDictionary<string, ProviderRatingRecord> Seed =
        new Dictionary<string, ProviderRatingRecord>(StringComparer.OrdinalIgnoreCase)
        {
            ["nordstar"] = new ProviderRatingRecord(
                Provider: Provider.Msci,
                IssuerId: "nordstar",
                Letter: "BBB-",
                Notch: NotchLadder.ToNotch("BBB-"),
                AsOfDate: default,
                RatingActionDate: new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
                Factors: new RatingFactor[]
                {
                    new("Leverage", 0.40m, 55m, "synthetic:msci:nordstar#leverage"),
                    new("InterestCoverage", 0.30m, 60m, "synthetic:msci:nordstar#coverage"),
                    new("BusinessRisk", 0.30m, 58m, "synthetic:msci:nordstar#business-risk"),
                },
                SourceRef: "synthetic:msci:nordstar"),
        };

    public Provider Provider => Provider.Msci;

    public Task<ProviderRatingRecord?> GetRatingAsOfAsync(string issuerId, DateTimeOffset asOf, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerId);
        ct.ThrowIfCancellationRequested();

        using var activity = ConnectorTelemetry.Source.StartActivity("Synthetic.GetRatingAsOf");
        activity?.SetTag("issuerId", issuerId);
        activity?.SetTag("asOf", asOf);
        activity?.SetTag("provider", Provider.ToString());

        // Unknown issuer → no coverage (MISSING_COVERAGE). Known but action after asOf → as-of hidden.
        if (!Seed.TryGetValue(issuerId, out var record) || record.RatingActionDate > asOf)
        {
            return Task.FromResult<ProviderRatingRecord?>(null);
        }

        return Task.FromResult<ProviderRatingRecord?>(record with { AsOfDate = asOf });
    }
}
