using System.Globalization;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Models;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Agents;

/// <summary>
/// Narrates one <b>already-computed</b> attribution bucket of a <see cref="PairDivergence"/> (pkg 05)
/// into its <see cref="BucketAttribution.Explanation"/> (pkg 06 §A/§C). It cites the bucket's evidence
/// refs and may use only the numbers already in the deterministic decomposition; any invented number is
/// dropped by <see cref="NarrationGuard"/>, keeping the raw signed contribution authoritative (P2).
/// Uses the same <c>gpt-5-mini</c>-class model slot as the red-flag narrator (arch-04).
/// </summary>
public sealed class DivergenceNarratorAgent(
    IAgentTextRunner runner,
    IOptions<PrismOptions> options,
    ILogger<DivergenceNarratorAgent> logger)
{
    private const string AgentName = "DivergenceNarratorAgent";

    private const string Instructions =
        "You are Prism's divergence narrator for corporate-bond credit-rating reconciliation — a " +
        "data-quality and provenance tool, not an investment tool. Explain, in one plain sentence, how " +
        "the given attribution bucket contributes to the notch gap between the two providers, using " +
        "ONLY the facts provided. Cite every evidence reference verbatim. Do not introduce, change, or " +
        "recompute any number. Never give investment advice and never use words like buy, sell, hold, " +
        "recommend, allocate, trade, alpha, or signal.";

    public async Task<string> NarrateAsync(
        Issuer issuer, PairDivergence divergence, BucketAttribution bucket, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(divergence);
        ArgumentNullException.ThrowIfNull(bucket);

        string evidence = bucket.EvidenceRefs.Count == 0 ? "none" : string.Join(", ", bucket.EvidenceRefs);
        string facts = string.Create(CultureInfo.InvariantCulture,
            $"Providers: {divergence.A} vs {divergence.B}. Notch gap: {divergence.NotchGap}. " +
            $"Attribution bucket: {bucket.Bucket}. Signed contribution: {bucket.Notches} notches. " +
            $"Evidence references: {evidence}.");

        string raw = await runner.RunAsync(options.Value.Models.RedFlag, AgentName, Instructions, facts, ct);
        string safe = NarrationGuard.Sanitize(raw, facts, bucket.EvidenceRefs);

        logger.LogInformation(
            "DivergenceNarrator {A}/{B} {Bucket} for {IssuerId}: narration {Status}",
            divergence.A, divergence.B, bucket.Bucket, issuer.IssuerId,
            safe.Length == 0 ? "dropped" : "accepted");

        return safe;
    }
}
