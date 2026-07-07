using System.Globalization;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Models;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Agents;

/// <summary>
/// Explains one provider's assessment of an issuer in that provider's terms, grounded on the
/// already-retrieved rating card (pkg 03/08) and citing its source doc id (pkg 06 §A). It
/// <b>retrieves and narrates</b> — it never computes a notch or fires a flag (P2). The grounding is
/// passed in code (arch-04: the model never free-roams) and the output is post-validated by
/// <see cref="NarrationGuard"/> so it cannot invent a number or drop the citation.
/// </summary>
public sealed class ProviderExplainerAgent(
    IAgentTextRunner runner,
    IOptions<PrismOptions> options,
    ILogger<ProviderExplainerAgent> logger)
{
    private const string AgentName = "ProviderExplainerAgent";

    private const string Instructions =
        "You are Prism's provider-explainer for corporate-bond credit-rating reconciliation — a " +
        "data-quality and provenance tool, not an investment tool. Explain, in one or two sentences, " +
        "what the given provider's rating says about the issuer, using ONLY the facts provided. Cite " +
        "the source document id verbatim. Do not introduce any number, date, or letter grade that is " +
        "not in the facts. Never give investment advice and never use words like buy, sell, hold, " +
        "recommend, allocate, trade, alpha, or signal.";

    public async Task<ProviderExplanation> ExplainAsync(Issuer issuer, ProviderRating rating, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(rating);

        string sourceRef = rating.MethodologyDocId;
        string facts = string.Create(CultureInfo.InvariantCulture,
            $"Issuer: {issuer.LegalName} ({issuer.Ticker}). Provider: {rating.Provider}. " +
            $"Rating: {rating.Letter} (notch {rating.Notch}). " +
            $"Rating action date: {rating.AsOfDate.UtcDateTime.Date:yyyy-MM-dd}. " +
            $"Source document id: {sourceRef}.");

        string raw = await runner.RunAsync(options.Value.Models.Provider, AgentName, Instructions, facts, ct);
        string safe = NarrationGuard.Sanitize(raw, facts, new[] { sourceRef });

        logger.LogInformation(
            "ProviderExplainer {Provider} for {IssuerId}: narration {Status}",
            rating.Provider, issuer.IssuerId, safe.Length == 0 ? "dropped" : "accepted");

        return new ProviderExplanation(
            safe,
            safe.Length == 0 ? Array.Empty<string>() : new[] { sourceRef });
    }
}
