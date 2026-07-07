using FinancialServices.Api.Analysis;
using FinancialServices.Api.Models;

namespace FinancialServices.Api.Services;

/// <summary>
/// Read boundary over the <c>prism-ratings</c> Azure AI Search corpus (arch-08: SearchCorpus owns
/// Search reads). Controllers/agents/services depend on this, not on <c>SearchClient</c> directly.
/// </summary>
public interface ISearchCorpus
{
    /// <summary>The full issuer cast (each with its filing boundary + provider coverage).</summary>
    Task<IReadOnlyList<IssuerCorpusEntry>> GetIssuersAsync(CancellationToken ct);

    /// <summary>One issuer by id, or <c>null</c> if the corpus has no such issuer doc.</summary>
    Task<IssuerCorpusEntry?> GetIssuerAsync(string issuerId, CancellationToken ct);

    /// <summary>
    /// The provider rating cards for an issuer, mapped to <see cref="ProviderRating"/> and filtered to
    /// rating actions on or before <paramref name="asOf"/> (as-of correctness, P3). An empty list is
    /// data (no coverage), not an error.
    /// </summary>
    Task<IReadOnlyList<ProviderRating>> GetProviderRatingsAsync(string issuerId, DateTimeOffset asOf, CancellationToken ct);
}
