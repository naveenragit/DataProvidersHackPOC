using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using FinancialServices.Api.Analysis;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;

namespace FinancialServices.Api.Services;

/// <summary>
/// <see cref="SearchClient"/>-backed reader for the labeled-synthetic ratings corpus (pkg 03). Reads
/// are filtered by <c>docType</c>/<c>issuerId</c> (OData); the user-supplied issuerId is escaped
/// before it enters a filter (P6 — treat all inputs as hostile). Upstream failures fail loud as
/// <see cref="UpstreamServiceException"/> (never a fabricated result — P1).
/// </summary>
public sealed class SearchCorpus(SearchClient client, ILogger<SearchCorpus> logger) : ISearchCorpus
{
    private const int MaxCorpusRows = 1000; // the curated corpus is tiny; one page covers it.

    public async Task<IReadOnlyList<IssuerCorpusEntry>> GetIssuersAsync(CancellationToken ct)
    {
        IReadOnlyList<SearchCorpusRow> issuerRows = await QueryAsync("docType eq 'issuer'", ct);
        IReadOnlyList<SearchCorpusRow> cardRows = await QueryAsync("docType eq 'ratingCard'", ct);

        Dictionary<string, List<Provider>> coverage = BuildCoverage(cardRows);

        return issuerRows
            .Select(row => SearchCorpusMapper.ToIssuerEntry(
                row,
                coverage.TryGetValue(row.IssuerId, out List<Provider>? providers)
                    ? providers
                    : Array.Empty<Provider>()))
            .OrderBy(entry => entry.Issuer.IssuerId, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IssuerCorpusEntry?> GetIssuerAsync(string issuerId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerId);
        string escaped = Escape(issuerId);

        IReadOnlyList<SearchCorpusRow> issuerRows =
            await QueryAsync($"docType eq 'issuer' and issuerId eq '{escaped}'", ct);
        SearchCorpusRow? issuerRow = issuerRows.FirstOrDefault();
        if (issuerRow is null)
        {
            return null;
        }

        IReadOnlyList<SearchCorpusRow> cardRows =
            await QueryAsync($"docType eq 'ratingCard' and issuerId eq '{escaped}'", ct);
        Provider[] providers = cardRows
            .Where(SearchCorpusMapper.IsGradedCard) // R2: a non-grade (NR/WR/…) card is not coverage.
            .Select(card => SearchCorpusMapper.ParseProvider(card.Provider))
            .Distinct()
            .OrderBy(provider => (int)provider)
            .ToArray();

        return SearchCorpusMapper.ToIssuerEntry(issuerRow, providers);
    }

    public async Task<IReadOnlyList<ProviderRating>> GetProviderRatingsAsync(
        string issuerId, DateTimeOffset asOf, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerId);

        IReadOnlyList<SearchCorpusRow> cardRows =
            await QueryAsync($"docType eq 'ratingCard' and issuerId eq '{Escape(issuerId)}'", ct);

        IReadOnlyList<ProviderRating> mapped = SearchCorpusMapper.MapCards(cardRows, asOf);

        // R2/arch-07 observability: a non-grade (NR/WR/D/SD/RD) card is dropped and surfaces as
        // MISSING_COVERAGE downstream. Log the count (ids only, no PII) so operators can tell a
        // data-quality event apart from a provider that genuinely never rated the issuer.
        int eligible = cardRows.Count(card => card.AsOfDate <= asOf);
        int dropped = eligible - mapped.Count;
        if (dropped > 0)
        {
            logger.LogInformation(
                "Dropped {Dropped} non-grade rating card(s) for issuer {IssuerId} as of {AsOf:yyyy-MM-dd} — surfaced as MISSING_COVERAGE.",
                dropped, issuerId, asOf);
        }

        return mapped;
    }

    // Group the rating cards into an issuerId → distinct provider-set map (issuer coverage).
    private static Dictionary<string, List<Provider>> BuildCoverage(IReadOnlyList<SearchCorpusRow> cardRows)
    {
        var coverage = new Dictionary<string, List<Provider>>(StringComparer.Ordinal);
        foreach (SearchCorpusRow card in cardRows)
        {
            if (!SearchCorpusMapper.IsGradedCard(card))
            {
                continue; // R2: a non-grade (NR/WR/…) card is not coverage — it surfaces as MISSING_COVERAGE.
            }

            Provider provider = SearchCorpusMapper.ParseProvider(card.Provider);
            if (!coverage.TryGetValue(card.IssuerId, out List<Provider>? providers))
            {
                providers = new List<Provider>();
                coverage[card.IssuerId] = providers;
            }

            if (!providers.Contains(provider))
            {
                providers.Add(provider);
            }
        }

        foreach (List<Provider> providers in coverage.Values)
        {
            providers.Sort((a, b) => ((int)a).CompareTo((int)b));
        }

        return coverage;
    }

    private async Task<IReadOnlyList<SearchCorpusRow>> QueryAsync(string filter, CancellationToken ct)
    {
        var options = new SearchOptions { Filter = filter, Size = MaxCorpusRows };
        try
        {
            Response<SearchResults<SearchCorpusRow>> response =
                await client.SearchAsync<SearchCorpusRow>("*", options, ct);

            var rows = new List<SearchCorpusRow>();
            await foreach (SearchResult<SearchCorpusRow> result in response.Value.GetResultsAsync().WithCancellation(ct))
            {
                rows.Add(result.Document);
            }

            return rows;
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "AI Search query failed ({Status}) for filter {Filter}", ex.Status, filter);
            throw new UpstreamServiceException("Search", $"AI Search query failed ({ex.Status}).", ex);
        }
    }

    // Escape single quotes for an OData string literal (prevents filter injection — P6).
    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
