using System.Globalization;
using FinancialServices.Api.Models;

namespace FinancialServices.Api.Analysis;

/// <summary>
/// Deterministic reconciliation scoring (P2, no LLM). Turns a provider set plus its red flags into the
/// dossier's consensus wording and confidence — reproducible from coverage and flag severity alone.
/// </summary>
public static class ReconciliationScoring
{
    /// <summary>
    /// One-line consensus summary from the notch spread (<c>max − min</c>): <c>0 ⇒ "full consensus"</c>,
    /// <c>1 ⇒ "consensus within 1 notch"</c>, <c>≥ 2 ⇒ "{spread}-notch split"</c>, empty ⇒
    /// <c>"no coverage"</c>.
    /// </summary>
    public static string ConsensusSummary(IReadOnlyList<ProviderRating> ratings)
    {
        if (ratings.Count == 0)
        {
            return "no coverage";
        }

        int spread = ratings.Max(r => r.Notch) - ratings.Min(r => r.Notch);
        return spread switch
        {
            0 => "full consensus",
            1 => "consensus within 1 notch",
            _ => string.Create(CultureInfo.InvariantCulture, $"{spread}-notch split"),
        };
    }

    /// <summary>
    /// Confidence in <c>[0, 1]</c>: coverage (rated providers ÷ total providers) minus severity
    /// penalties (<c>high 0.30 / medium 0.15 / low 0.05</c>), clamped. <c>MISSING_COVERAGE</c> flags are
    /// excluded from the penalty — the coverage fraction already reflects them (no double-counting).
    /// Full fresh consensus ⇒ <c>1.0</c>; one high flag over full coverage ⇒ <c>0.70</c>.
    /// </summary>
    public static double ConfidenceScore(IReadOnlyList<ProviderRating> ratings, IReadOnlyList<RedFlag> flags)
    {
        double coverage = ratings.Count / (double)Enum.GetValues<Provider>().Length;

        double penalty = 0.0;
        foreach (RedFlag flag in flags)
        {
            // MISSING_COVERAGE is already reflected in the coverage fraction above — excluding it here
            // avoids double-counting the same missing provider in both terms.
            if (string.Equals(flag.Code, "MISSING_COVERAGE", StringComparison.Ordinal))
            {
                continue;
            }

            penalty += flag.Severity switch
            {
                "high" => 0.30,
                "medium" => 0.15,
                "low" => 0.05,
                _ => 0.0,
            };
        }

        return Math.Clamp(coverage - penalty, 0.0, 1.0);
    }
}
