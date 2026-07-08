using FinancialServices.Api.Models;

namespace FinancialServices.Api.Services.MarketContext;

/// <summary>
/// Fetches live, attributed Morningstar analyst research for a real listed security, as a
/// <b>context-enrichment</b> feature alongside (never inside) Prism's rating reconciliation. Always
/// resolves to a renderable <see cref="MorningstarContextResponse"/> — provider off / not covered /
/// re-login / upstream failure are all conveyed as a <see cref="MarketContextStatus"/>, never thrown (P1).
/// </summary>
public interface IMorningstarContextService
{
    /// <summary>
    /// Looks <paramref name="identifier"/> (ticker, ISIN, or name) up in Morningstar, then fetches the
    /// latest analyst research for the matched security.
    /// </summary>
    Task<MorningstarContextResponse> GetResearchAsync(string identifier, CancellationToken cancellationToken);
}
