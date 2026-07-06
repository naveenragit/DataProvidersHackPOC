using System.Globalization;
using FinancialServices.Api.Models;

namespace FinancialServices.Api.Analysis;

/// <summary>
/// Deterministic red-flag engine — pure C# (P2, no LLM, no network). Severity is fixed by rule (never
/// by a model) and <see cref="RedFlag.Rule"/> is the verbatim string the UI shows on click-through; the
/// LLM only fills <see cref="RedFlag.Narrative"/> later (pkg 06).
/// </summary>
public sealed class RedFlagEngine
{
    /// <summary>
    /// Evaluate the deterministic rules for one issuer. <paramref name="divergences"/> come from
    /// <see cref="DivergenceDecomposer"/> (run first) and drive METHODOLOGY_CONFLICT. Emission order is
    /// stable: STALE_INPUT → MISSING_COVERAGE → OUTLIER_PROVIDER → METHODOLOGY_CONFLICT.
    /// </summary>
    public IReadOnlyList<RedFlag> Evaluate(
        Issuer issuer,
        IReadOnlyList<ProviderRating> ratings,
        FundamentalSnapshot latest,
        IReadOnlyList<PairDivergence> divergences)
    {
        var flags = new List<RedFlag>();

        // ── THE MONEY MOMENT — STALE_INPUT (high) ─────────────────────────────────────────────────
        // A provider's rating action predates the issuer's latest real filing. Compared on the DATE
        // (UTC calendar day), not the instant: cross-source timestamps (provider rating-action date vs
        // EDGAR filed date) on the SAME UTC day must NOT be flagged stale — only a genuinely earlier day.
        DateTime filingDay = latest.FilingDate.UtcDateTime.Date;
        foreach (ProviderRating r in ratings)
        {
            DateTime actionDay = r.InputAsOfDate.UtcDateTime.Date;
            if (actionDay < filingDay)
            {
                flags.Add(new RedFlag(
                    "STALE_INPUT", "high",
                    string.Create(CultureInfo.InvariantCulture, $"Rating action dated {actionDay:yyyy-MM-dd} predates the issuer's latest filing ({latest.FilingType}) on {filingDay:yyyy-MM-dd}."),
                    "",
                    new[] { $"{issuer.IssuerId}-{r.Provider}", $"edgar:{issuer.Cik}:{latest.FilingType}" }));
            }
        }

        // ── MISSING_COVERAGE (medium) ─────────────────────────────────────────────────────────────
        foreach (Provider p in Enum.GetValues<Provider>())
        {
            if (ratings.All(r => r.Provider != p))
            {
                flags.Add(new RedFlag(
                    "MISSING_COVERAGE", "medium",
                    $"{p} publishes no rating for {issuer.LegalName}.",
                    "",
                    new[] { issuer.IssuerId }));
            }
        }

        // ── OUTLIER_PROVIDER (medium) ─────────────────────────────────────────────────────────────
        if (ratings.Count >= 3)
        {
            decimal median = Median(ratings.Select(r => r.Notch));
            foreach (ProviderRating r in ratings)
            {
                decimal distance = Math.Abs(r.Notch - median);
                if (distance >= 3m)
                {
                    flags.Add(new RedFlag(
                        "OUTLIER_PROVIDER", "medium",
                        string.Create(CultureInfo.InvariantCulture, $"{r.Provider} sits {distance} notches from the peer median."),
                        "",
                        new[] { $"{issuer.IssuerId}-{r.Provider}" }));
                }
            }
        }

        // ── METHODOLOGY_CONFLICT (medium) ─────────────────────────────────────────────────────────
        // A wide gap (>= 2 notches) whose residual dominates it (> 50%): the part NOT mechanically
        // attributable to factor weighting or input timing. The Rule states that residual share
        // honestly (not a precise methodology measurement) and cites BOTH provider cards (P3).
        foreach (PairDivergence d in divergences)
        {
            int absGap = Math.Abs(d.NotchGap);
            if (absGap < 2)
            {
                continue;
            }

            decimal residualShare = DivergenceDecomposer.ResidualShare(d);
            if (residualShare > 0.5m)
            {
                int pct = (int)Math.Round(residualShare * 100m, MidpointRounding.AwayFromZero);
                flags.Add(new RedFlag(
                    "METHODOLOGY_CONFLICT", "medium",
                    string.Create(CultureInfo.InvariantCulture, $"{d.A} vs {d.B} differ by {absGap} notches; {pct}% of the gap is a methodology residual not mechanically attributable to factor weighting or input timing."),
                    "",
                    new[] { $"{issuer.IssuerId}-{d.A}", $"{issuer.IssuerId}-{d.B}" }));
            }
        }

        return flags;
    }

    // Median of the notch set: odd count ⇒ the middle value, even ⇒ the average of the two middles
    // (may be .5). Only called with ≥ 3 ratings, so the set is non-empty.
    private static decimal Median(IEnumerable<int> notches)
    {
        int[] sorted = notches.OrderBy(n => n).ToArray();
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2m;
    }
}
