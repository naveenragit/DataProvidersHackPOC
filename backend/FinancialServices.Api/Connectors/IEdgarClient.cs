using FinancialServices.Api.Analysis;

namespace FinancialServices.Api.Connectors;

/// <summary>Real SEC EDGAR fundamentals connector (companyfacts + submissions), I/O-only + as-of correct.</summary>
public interface IEdgarClient
{
    /// <summary>
    /// Fetches raw EDGAR data and returns it <b>without derivation</b> (P2): the us-gaap concept facts
    /// (unfiltered) plus the issuer's latest filing on or before <paramref name="asOf"/> from the
    /// submissions API. The pure <see cref="FundamentalsCalculator"/> owns the fundamentals math.
    /// Throws <see cref="Infrastructure.Errors.UpstreamServiceException"/> on upstream failure and
    /// <see cref="ArgumentOutOfRangeException"/> when <paramref name="asOf"/> is in the future — never a
    /// fabricated value.
    /// </summary>
    Task<EdgarCompanyFacts> GetFactsAsOfAsync(string cik, DateTimeOffset asOf, CancellationToken ct);
}
