using FinancialServices.Api.Agents;
using FinancialServices.Api.Analysis;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using FinancialServices.Api.Services;

namespace FinancialServices.Api.Orchestration;

/// <summary>
/// The shared, transport-agnostic reconciliation steps (spec §A) — reused by both the SSE streaming
/// orchestrator and the live AG-UI agent tools so the two paths cannot drift (P8). Each step is a thin
/// composition over the pkg-03 corpus, the pkg-06 narrators and (for the authoritative dossier) the
/// pkg-08 <see cref="IReconciliationService"/>. It holds <b>no</b> deterministic math: the notch/gap/
/// attribution/flag numbers come from the pkg-05 <c>Analysis/</c> engines via the reconciliation
/// service (P2). All tool arguments are treated as hostile and re-authorized against the corpus (P6),
/// and <see cref="CancellationToken"/> is plumbed through every call (P7).
/// </summary>
public sealed class PrismSweepSteps(
    ISearchCorpus corpus,
    ProviderExplainerAgent providerExplainer,
    FundamentalsAgent fundamentalsAgent,
    ILogger<PrismSweepSteps> logger)
{
    /// <summary>Normalizes a model-supplied issuer id to the corpus key form (lower-case, trimmed).</summary>
    public static string NormalizeIssuerId(string? raw) =>
        (raw ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Resolves + re-authorizes a model-supplied issuer id against the corpus (P6). Throws
    /// <see cref="NotFoundException"/> for an unknown issuer — never trusts the model's argument.
    /// </summary>
    public async Task<IssuerCorpusEntry> ResolveIssuerAsync(string? issuerIdRaw, CancellationToken ct)
    {
        string issuerId = NormalizeIssuerId(issuerIdRaw);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuerId);

        return await corpus.GetIssuerAsync(issuerId, ct)
            ?? throw new NotFoundException("issuer", issuerId);
    }

    /// <summary>Parses + validates a model-supplied provider name (P6). Throws on an unknown provider.</summary>
    public static Provider ParseProvider(string? providerRaw)
    {
        if (Enum.TryParse((providerRaw ?? string.Empty).Trim(), ignoreCase: true, out Provider provider) &&
            Enum.IsDefined(provider))
        {
            return provider;
        }

        throw new ValidationException(
            "Unknown provider.", new Dictionary<string, object?> { ["provider"] = providerRaw });
    }

    /// <summary>
    /// The <c>retrieveProviderRating</c> step: fetches one provider's as-of rating card from the corpus
    /// and narrates it with the pkg-06 <see cref="ProviderExplainerAgent"/> (guarded). Returns
    /// <c>null</c> when the provider has no coverage for the issuer as of the date — that is data, not
    /// an error (the deterministic MISSING_COVERAGE flag covers it).
    /// </summary>
    public async Task<(ProviderRating Rating, ProviderExplanation Explanation)?> RetrieveProviderRatingAsync(
        Issuer issuer, Provider provider, DateTimeOffset asOf, CancellationToken ct)
    {
        IReadOnlyList<ProviderRating> ratings = await corpus.GetProviderRatingsAsync(issuer.IssuerId, asOf, ct);
        ProviderRating? rating = ratings.FirstOrDefault(r => r.Provider == provider);
        if (rating is null)
        {
            logger.LogInformation("No {Provider} coverage for {IssuerId} as of {AsOf:yyyy-MM-dd}",
                provider, issuer.IssuerId, asOf.UtcDateTime);
            return null;
        }

        // The rating is the deterministic corpus value; the narration is decorative. A transient
        // narration fault (429 / timeout / content-filter) must NOT abort the stream before the
        // authoritative dossier is assembled — fall back to an empty explanation and continue. A
        // permanent misconfiguration still fails loud (P1), mirroring the REST DossierNarrator.
        ProviderExplanation explanation = await BestEffortAsync(
            () => providerExplainer.ExplainAsync(issuer, rating, ct),
            new ProviderExplanation(string.Empty, Array.Empty<string>()),
            $"provider:{provider}", ct);
        return (rating, explanation);
    }

    /// <summary>
    /// The <c>groundFundamentals</c> step: builds the as-of filing snapshot from the corpus issuer doc
    /// (financial ratios are genuinely absent for the labeled-synthetic cast → null, never fabricated,
    /// P1) and narrates it with the pkg-06 <see cref="FundamentalsAgent"/>.
    /// </summary>
    public async Task<(FundamentalSnapshot Snapshot, FundamentalsSummary Summary)> GroundFundamentalsAsync(
        IssuerCorpusEntry entry, CancellationToken ct)
    {
        var snapshot = new FundamentalSnapshot(
            entry.Issuer.IssuerId, entry.LatestFilingDate, entry.FilingType,
            DebtToEbitda: null, InterestCoverage: null, CashAndEquivalents: null);

        FundamentalsSummary summary = await BestEffortAsync(
            () => fundamentalsAgent.SummarizeAsync(entry.Issuer, snapshot, ct),
            new FundamentalsSummary(string.Empty, Array.Empty<string>()),
            "fundamentals", ct);
        return (snapshot, summary);
    }

    // Runs a decorative card narration best-effort: a genuine cancellation (P7) and a permanent
    // ConfigurationException (P1 — fail loud, consistent with DossierNarrator) propagate; any other
    // (transient) fault falls back to the empty result so the sweep continues to the authoritative,
    // deterministic dossier. Never logs prompts or model output (P6).
    private async Task<T> BestEffortAsync<T>(
        Func<Task<T>> narrate, T fallback, string item, CancellationToken ct)
    {
        try
        {
            return await narrate();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (ConfigurationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex, "Card narration failed for {Item}; streaming the deterministic card without narrative", item);
            return fallback;
        }
    }
}
