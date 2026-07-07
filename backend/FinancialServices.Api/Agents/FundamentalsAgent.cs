using System.Globalization;
using System.Text;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Models;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Agents;

/// <summary>
/// States an issuer's ground-truth fundamentals + the real filing as-of date from the EDGAR/FRED
/// snapshot (pkg 04/06 §A). It narrates only what the deterministic snapshot contains and is honest
/// about absent figures (P1 — nullable ratios are genuinely missing for the labeled-synthetic cast,
/// never fabricated). Cites the EDGAR filing reference; output post-validated by
/// <see cref="NarrationGuard"/>.
/// </summary>
public sealed class FundamentalsAgent(
    IAgentTextRunner runner,
    IOptions<PrismOptions> options,
    ILogger<FundamentalsAgent> logger)
{
    private const string AgentName = "FundamentalsAgent";

    private const string Instructions =
        "You are Prism's fundamentals reporter for corporate-bond credit-rating reconciliation — a " +
        "data-quality and provenance tool, not an investment tool. State the issuer's reported figures " +
        "and the as-of filing date in one or two sentences, using ONLY the facts provided. Cite the " +
        "EDGAR reference verbatim. If a figure is marked unavailable, say it is unavailable — never " +
        "estimate or invent one. Do not introduce any number or date not in the facts. Never give " +
        "investment advice and never use words like buy, sell, hold, recommend, allocate, trade, " +
        "alpha, or signal.";

    public async Task<FundamentalsSummary> SummarizeAsync(
        Issuer issuer, FundamentalSnapshot snapshot, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(snapshot);

        string edgarRef = string.Create(CultureInfo.InvariantCulture, $"edgar:{issuer.Cik}:{snapshot.FilingType}");
        string facts = BuildFacts(issuer, snapshot, edgarRef);

        string raw = await runner.RunAsync(options.Value.Models.Fundamentals, AgentName, Instructions, facts, ct);
        string safe = NarrationGuard.Sanitize(raw, facts, new[] { edgarRef });

        logger.LogInformation(
            "Fundamentals for {IssuerId}: narration {Status}",
            issuer.IssuerId, safe.Length == 0 ? "dropped" : "accepted");

        return new FundamentalsSummary(
            safe,
            safe.Length == 0 ? Array.Empty<string>() : new[] { edgarRef });
    }

    private static string BuildFacts(Issuer issuer, FundamentalSnapshot snapshot, string edgarRef)
    {
        var facts = new StringBuilder();
        facts.Append(CultureInfo.InvariantCulture,
            $"Issuer: {issuer.LegalName} ({issuer.Ticker}). ");
        facts.Append(CultureInfo.InvariantCulture,
            $"Latest filing: {snapshot.FilingType} dated {snapshot.FilingDate.UtcDateTime.Date:yyyy-MM-dd}. ");
        facts.Append(CultureInfo.InvariantCulture,
            $"Debt/EBITDA: {Figure(snapshot.DebtToEbitda)}. ");
        facts.Append(CultureInfo.InvariantCulture,
            $"Interest coverage: {Figure(snapshot.InterestCoverage)}. ");
        facts.Append(CultureInfo.InvariantCulture,
            $"Cash and equivalents: {Figure(snapshot.CashAndEquivalents)}. ");
        facts.Append(CultureInfo.InvariantCulture, $"EDGAR reference: {edgarRef}.");
        return facts.ToString();
    }

    // A present figure renders as an invariant number; an absent one is stated as unavailable (P1).
    private static string Figure(decimal? value) =>
        value is decimal number
            ? number.ToString("0.###", CultureInfo.InvariantCulture)
            : "unavailable";
}
