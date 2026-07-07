using System.Globalization;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Models;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Agents;

/// <summary>
/// Narrates one <b>already-computed</b> <see cref="RedFlag"/> (pkg 05) in prose (pkg 06 §A/§C). It adds
/// no new claims: it must cite <b>every</b> <see cref="RedFlag.EvidenceRefs"/> entry and may use only
/// the numbers/dates already in the deterministic <see cref="RedFlag.Rule"/>. If it invents or alters a
/// number, <see cref="NarrationGuard"/> drops the narration and the UI keeps the verbatim rule text
/// (P2 — the deterministic value is authoritative).
/// </summary>
public sealed class RedFlagNarratorAgent(
    IAgentTextRunner runner,
    IOptions<PrismOptions> options,
    ILogger<RedFlagNarratorAgent> logger)
{
    private const string AgentName = "RedFlagNarratorAgent";

    private const string Instructions =
        "You are Prism's red-flag narrator for corporate-bond credit-rating reconciliation — a " +
        "data-quality and provenance tool, not an investment tool. Restate the given data-quality flag " +
        "in one or two plain sentences for an analyst, using ONLY the facts in the rule. You MUST cite " +
        "every evidence reference verbatim. Do not introduce, change, or recompute any number or date, " +
        "and add no claim beyond the rule. Never give investment advice and never use words like buy, " +
        "sell, hold, recommend, allocate, trade, alpha, or signal.";

    public async Task<string> NarrateAsync(Issuer issuer, RedFlag flag, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(flag);

        string evidence = string.Join(", ", flag.EvidenceRefs);
        string facts = string.Create(CultureInfo.InvariantCulture,
            $"Flag: {flag.Code} (severity {flag.Severity}). Rule: {flag.Rule} Evidence references: {evidence}.");

        string raw = await runner.RunAsync(options.Value.Models.RedFlag, AgentName, Instructions, facts, ct);
        string safe = NarrationGuard.Sanitize(raw, facts, flag.EvidenceRefs);

        logger.LogInformation(
            "RedFlagNarrator {Code} for {IssuerId}: narration {Status}",
            flag.Code, issuer.IssuerId, safe.Length == 0 ? "dropped" : "accepted");

        return safe;
    }
}
