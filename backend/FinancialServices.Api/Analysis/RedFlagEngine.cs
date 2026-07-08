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
    // R5 — materiality window for STALE_INPUT. A rating action that predates the latest filing by only
    // a few weeks likely saw substantially the same fundamentals; agencies do not re-rate on every
    // filing. Only a gap WIDER than this (whole UTC days) is materially stale and fires the flag. Kept
    // a plain constant so the P2 core stays pure (no IOptions); tune here if the demo corpus changes.
    private const int StaleMaterialityDays = 45;

    // R4 — a provider must sit at least this many notches from the peer median to be an outlier.
    private const decimal OutlierNotchDistance = 3m;

    /// <summary>
    /// Evaluate the deterministic rules for one issuer. <paramref name="divergences"/> come from
    /// <see cref="DivergenceDecomposer"/> (run first) and drive METHODOLOGY_CONFLICT. Emission order is
    /// stable: STALE_INPUT → IG_HY_BOUNDARY → MISSING_COVERAGE → OUTLIER_PROVIDER →
    /// PROVIDER_UNDER_REVIEW → METHODOLOGY_CONFLICT.
    /// </summary>
    public IReadOnlyList<RedFlag> Evaluate(
        Issuer issuer,
        IReadOnlyList<ProviderRating> ratings,
        FundamentalSnapshot latest,
        IReadOnlyList<PairDivergence> divergences)
    {
        var flags = new List<RedFlag>();

        // ── THE MONEY MOMENT — STALE_INPUT (high) ─────────────────────────────────────────────────
        // A provider's rating action materially predates the issuer's latest real filing. Compared on
        // the DATE (UTC calendar day), not the instant: cross-source timestamps (provider rating-action
        // date vs EDGAR filed date) on the SAME UTC day must NOT be flagged stale. R5: only a gap wider
        // than the materiality window (StaleMaterialityDays) fires — a rating a week older than a routine
        // filing is not meaningfully stale. Stale providers are tracked so METHODOLOGY_CONFLICT (below)
        // does not also claim a gap that staleness already explains (R3).
        DateTime filingDay = latest.FilingDate.UtcDateTime.Date;
        var staleProviders = new HashSet<Provider>();
        foreach (ProviderRating r in ratings)
        {
            DateTime actionDay = r.InputAsOfDate.UtcDateTime.Date;
            int daysStale = (int)(filingDay - actionDay).TotalDays;
            if (daysStale > StaleMaterialityDays)
            {
                staleProviders.Add(r.Provider);
                flags.Add(new RedFlag(
                    "STALE_INPUT", "high",
                    string.Create(CultureInfo.InvariantCulture, $"Rating action dated {actionDay:yyyy-MM-dd} predates the issuer's latest filing ({latest.FilingType}) on {filingDay:yyyy-MM-dd} by {daysStale} days, so this opinion has not yet reflected the newer fundamentals. Rating lag is a documented driver of temporary split ratings — bond yield spreads typically move before an agency re-rates."),
                    "",
                    new[] { $"{issuer.IssuerId}-{r.Provider}", $"edgar:{issuer.Cik}:{latest.FilingType}" }));
            }
        }

        // ── IG_HY_BOUNDARY (high) ─────────────────────────────────────────────────────────────────
        // The single most consequential divergence: the provider set straddles the investment-grade /
        // high-yield line (BBB-/Baa3 = notch 10). Index membership, regulatory capital, and collateral
        // eligibility all key off which side an issuer lands on. Issuer-level: one flag naming both sides.
        ProviderRating[] igProviders = ratings.Where(r => NotchLadder.IsInvestmentGrade(r.Notch)).ToArray();
        ProviderRating[] hyProviders = ratings.Where(r => !NotchLadder.IsInvestmentGrade(r.Notch)).ToArray();
        if (igProviders.Length > 0 && hyProviders.Length > 0)
        {
            string igList = string.Join(", ", igProviders.Select(r => $"{r.Provider} ({r.Letter})"));
            string hyList = string.Join(", ", hyProviders.Select(r => $"{r.Provider} ({r.Letter})"));
            var refs = new List<string> { issuer.IssuerId };
            refs.AddRange(ratings.Select(r => $"{issuer.IssuerId}-{r.Provider}"));
            flags.Add(new RedFlag(
                "IG_HY_BOUNDARY", "high",
                string.Create(CultureInfo.InvariantCulture, $"Providers straddle the investment-grade / high-yield boundary: {igList} rate {issuer.LegalName} investment grade (BBB-/Baa3 or higher) while {hyList} rate it high yield (BB+/Ba1 or lower). A split across this line is the most consequential rating divergence — index membership (a bond leaves investment-grade indices such as the Bloomberg US Aggregate and enters high-yield indices), regulatory capital treatment (NAIC, Solvency II), and collateral eligibility all key off it. Index rules typically resolve such splits by convention — the middle of three agency ratings, or the lower of two."),
                "",
                refs.ToArray()));
        }

        // ── MISSING_COVERAGE (medium) ─────────────────────────────────────────────────────────────
        foreach (Provider p in Enum.GetValues<Provider>())
        {
            if (ratings.All(r => r.Provider != p))
            {
                flags.Add(new RedFlag(
                    "MISSING_COVERAGE", "medium",
                    $"{p} publishes no rating for {issuer.LegalName}. Agency coverage is not universal — the Big Three plus DBRS Morningstar and MSCI each cover different issuer sets, so thinner coverage (common for newer, smaller, or unsolicited names) leaves fewer independent opinions and lowers confidence; unsolicited assessments also tend to run more conservative.",
                    "",
                    new[] { issuer.IssuerId }));
            }
        }

        // ── OUTLIER_PROVIDER (medium) ─────────────────────────────────────────────────────────────
        // R4: fire for the UNIQUE most-distant provider (≥ OutlierNotchDistance from the peer median),
        // never for both ends of a symmetric spread. {6,9,12} (tie at 3) → none; {6,6,10} (unique 4) →
        // one. Requires ≥ 3 ratings; 2-provider wide splits are covered by METHODOLOGY_CONFLICT / IG_HY.
        var outlierProviders = new HashSet<Provider>();
        if (ratings.Count >= 3)
        {
            decimal median = Median(ratings.Select(r => r.Notch));
            (ProviderRating Rating, decimal Distance)[] distances =
                ratings.Select(r => (r, Math.Abs(r.Notch - median))).ToArray();
            decimal maxDistance = distances.Max(d => d.Distance);
            (ProviderRating Rating, decimal Distance)[] farthest =
                distances.Where(d => d.Distance == maxDistance).ToArray();

            if (maxDistance >= OutlierNotchDistance && farthest.Length == 1)
            {
                ProviderRating outlier = farthest[0].Rating;
                outlierProviders.Add(outlier.Provider);
                flags.Add(new RedFlag(
                    "OUTLIER_PROVIDER", "medium",
                    string.Create(CultureInfo.InvariantCulture, $"{outlier.Provider} sits {maxDistance} notches from the peer median — the lone outlier. A gap this wide on the same fundamentals usually traces to a structurally different methodology: a probability-of-default scale (as S&P uses) versus an expected-loss scale that credits recovery (as Moody's and Fitch use), or a heavier ESG/sector overlay."),
                    "",
                    new[] { $"{issuer.IssuerId}-{outlier.Provider}" }));
            }
        }

        // ── PROVIDER_UNDER_REVIEW (low) ───────────────────────────────────────────────────────────
        // R6: a provider on CreditWatch / under review may re-rate in the near term, so the present
        // divergence may not be durable. Fires only when the source actually supplies the flag (honest —
        // never fabricated); reviews frequently resolve toward the peer consensus.
        foreach (ProviderRating r in ratings)
        {
            if (r.UnderReview)
            {
                flags.Add(new RedFlag(
                    "PROVIDER_UNDER_REVIEW", "low",
                    string.Create(CultureInfo.InvariantCulture, $"{r.Provider} has {issuer.LegalName} on CreditWatch / under review, so its current {r.Letter} opinion may change in the near term. A rating under review is inherently less stable and lowers confidence that this divergence is durable — reviews frequently resolve toward the peer consensus."),
                    "",
                    new[] { $"{issuer.IssuerId}-{r.Provider}" }));
            }
        }

        // ── METHODOLOGY_CONFLICT (medium) ─────────────────────────────────────────────────────────
        // A wide gap (>= 2 notches) whose residual dominates it (> 50%): the part NOT mechanically
        // attributable to factor weighting or input timing. The flag is suppressed when the SAME gap is
        // already explained by another flag, so one divergence is not triple-reported: R3 — either
        // provider is STALE (input timing explains it); F1 — either provider is the lone OUTLIER (the
        // outlier flag already carries this MSCI-vs-peers gap). The Rule states the residual share
        // honestly (not a precise methodology measurement) and cites BOTH provider cards (P3).
        foreach (PairDivergence d in divergences)
        {
            int absGap = Math.Abs(d.NotchGap);
            if (absGap < 2)
            {
                continue;
            }

            if (staleProviders.Contains(d.A) || staleProviders.Contains(d.B))
            {
                continue; // R3: staleness already explains this gap — not a methodology conflict.
            }

            if (outlierProviders.Contains(d.A) || outlierProviders.Contains(d.B))
            {
                continue; // F1: the outlier flag already reports this gap — avoid triple-reporting it.
            }

            decimal residualShare = DivergenceDecomposer.ResidualShare(d);
            if (residualShare > 0.5m)
            {
                int pct = (int)Math.Round(residualShare * 100m, MidpointRounding.AwayFromZero);
                flags.Add(new RedFlag(
                    "METHODOLOGY_CONFLICT", "medium",
                    string.Create(CultureInfo.InvariantCulture, $"{d.A} vs {d.B} differ by {absGap} notches; {pct}% of the gap is a methodology residual, not mechanically attributable to factor weighting or input timing. Methodology splits arise from parent/group-support uplift, structural subordination (notching a parent below its operating subsidiaries), government-support assumptions, or hybrid equity-credit treatment. Precedent: Kraft Heinz (Feb 2020), when S&P and Fitch moved it to BB+ while Moody's kept it at Baa3 — a split straddling the investment-grade line."),
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
