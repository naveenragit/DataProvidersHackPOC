namespace FinancialServices.Api.Connectors;

/// <summary>Real FRED (St. Louis Fed) market-context connector — credit spreads + rates, as-of correct.</summary>
public interface IFredClient
{
    /// <summary>
    /// Returns the latest observation for <paramref name="seriesId"/> dated on or before
    /// <paramref name="asOf"/>. Throws <see cref="Infrastructure.Errors.UpstreamServiceException"/>
    /// when no observation qualifies or the upstream call fails — never a fabricated value.
    /// </summary>
    Task<decimal> GetLatestOnOrBeforeAsync(string seriesId, DateTimeOffset asOf, CancellationToken ct);
}
