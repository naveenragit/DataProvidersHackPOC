using FinancialServices.Api.Analysis;
using FinancialServices.Api.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Connectors;

/// <summary>
/// Moody's provider-ratings source — <b>pending real API</b>. The class, its options
/// (<see cref="PrismOptions.ProviderApiOptions.MoodysApi"/>) and its DI registration all exist so the
/// wiring is honest, but the product, endpoint, auth and response shape are not yet confirmed. Until
/// they are, <see cref="GetRatingAsOfAsync"/> returns <c>null</c> (→ <c>MISSING_COVERAGE</c>) and logs
/// a warning — it does <b>not</b> throw, so a parallel provider sweep degrades gracefully (ARC-03) and
/// never invents an HTTP integration (P1 — no fabrication).
/// </summary>
public sealed class MoodysRatingsClient(IOptions<PrismOptions> options, ILogger<MoodysRatingsClient> logger)
    : IProviderRatingsSource
{
    public Provider Provider => Provider.Moodys;

    public Task<ProviderRatingRecord?> GetRatingAsOfAsync(string issuerId, DateTimeOffset asOf, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerId);
        ct.ThrowIfCancellationRequested();

        logger.LogWarning(
            "Moody's provider API integration is pending (configured={Configured}); returning no coverage for {IssuerId}.",
            options.Value.ProviderApis.MoodysApi is not null,
            issuerId);

        return Task.FromResult<ProviderRatingRecord?>(null);
    }
}
