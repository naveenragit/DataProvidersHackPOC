using System.Globalization;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;

namespace FinancialServices.Api.Analysis;

/// <summary>
/// Deterministic divergence decomposer — the P2 heart of Prism (plain C#, no LLM, no network). For a
/// provider pair it splits the <c>b − a</c> notch gap into three signed buckets (Weighting, Input,
/// MethodologyAdjustment) that reconstruct the gap <b>exactly</b>. The LLM later narrates each bucket's
/// <see cref="BucketAttribution.Explanation"/> and cites its refs — it never changes the numbers.
/// </summary>
public sealed class DivergenceDecomposer
{
    // Maps a weighted sub-score delta (0..1 units) into notches. Shifts mass between the Weighting
    // bucket and the MethodologyAdjustment residual only — it can never break the sum==gap invariant.
    private const decimal WeightingNotchScale = 5m;

    // Maps one turn of Debt/EBITDA into notches (higher leverage ⇒ weaker credit ⇒ larger notch).
    private const decimal LeverageNotchScale = 1m;

    // The canonical leverage factor name; its own-vintage recompute drives the Input bucket.
    private const string LeverageFactorName = "Leverage";

    /// <summary>
    /// Decompose the <c>b − a</c> notch gap into Weighting + Input + MethodologyAdjustment (all
    /// expressed in the same <c>b − a</c> direction so they sum to the gap). <paramref name="aInputs"/>
    /// / <paramref name="bInputs"/> are each provider's own-vintage fundamentals; a null side (or a null
    /// leverage) contributes <c>0m</c> to the Input bucket — never fabricated (P1) — and folds into the
    /// residual.
    /// </summary>
    public PairDivergence Decompose(
        ProviderRating a, ProviderRating b,
        FundamentalSnapshot latest, FundamentalSnapshot? aInputs, FundamentalSnapshot? bInputs)
    {
        int gap = b.Notch - a.Notch;

        decimal weighting = WeightingContribution(a, b);
        decimal input = LeverageContribution(bInputs, latest) - LeverageContribution(aInputs, latest);

        // MethodologyAdjustment = the residual gap NOT mechanically attributable to factor weighting or
        // input timing. For letter-only inputs (no shared factors, no per-provider vintage snapshots) it
        // absorbs ~100% of the gap — surfaced honestly via ResidualShare / IsResidualDominated (P1),
        // never presented as a precise methodology measurement.
        decimal adj = gap - weighting - input;

        // P2 invariant (architecturalPlan/03): the three buckets must reconstruct the gap EXACTLY.
        EnsureBucketsReconcile(weighting, input, adj, gap);

        return new PairDivergence(a.Provider, b.Provider, gap, new[]
        {
            new BucketAttribution("Weighting", weighting, "", RefsFor(a, b, "weight")),
            new BucketAttribution("Input", input, "", RefsFor(a, b, "input")),
            new BucketAttribution("MethodologyAdjustment", adj, "", RefsFor(a, b, "overlay")),
        });
    }

    /// <summary>
    /// Guards the reconciliation invariant <c>weighting + input + adj == gap</c> (exact decimal). Public
    /// and static so the guard is unit-testable in isolation: the residual definition makes it
    /// unreachable on valid inputs, yet it still fails loud (P1) if the algebra is ever broken.
    /// </summary>
    /// <exception cref="DomainInvariantException">The buckets do not reconstruct the gap.</exception>
    public static void EnsureBucketsReconcile(decimal weighting, decimal input, decimal adj, int gap)
    {
        // Tiny tolerance (not exact ==) so a future non-terminating-decimal formula can't throw on
        // valid data; a real algebra break (whole-notch drift) is far larger and still fails loud (P1).
        if (Math.Abs(weighting + input + adj - gap) > 0.0001m)
        {
            throw new DomainInvariantException(
                "attribution.sum==gap",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Attribution buckets ({weighting} + {input} + {adj}) do not reconstruct notch gap {gap}."));
        }
    }

    /// <summary>
    /// The share of the notch gap absorbed by the <c>MethodologyAdjustment</c> residual:
    /// <c>|methodology notches| / max(|NotchGap|, 1)</c>. A high share means the gap is <b>not</b>
    /// mechanically attributable to factor weighting or input timing (e.g. letter-only inputs), so
    /// pkg 10 leads with the stale-input / "methodology-driven" story instead of a one-bar waterfall.
    /// </summary>
    public static decimal ResidualShare(PairDivergence d)
    {
        decimal residual = Math.Abs(MethodologyAdjustmentOf(d));
        decimal denominator = Math.Max(Math.Abs(d.NotchGap), 1);
        return residual / denominator;
    }

    /// <summary>
    /// True when the <c>MethodologyAdjustment</c> residual dominates the gap (default ≥ 80%): the
    /// decomposition is honestly "methodology-driven" rather than a precise weighting/input split.
    /// </summary>
    public static bool IsResidualDominated(PairDivergence d, decimal threshold = 0.8m) =>
        ResidualShare(d) >= threshold;

    // The signed MethodologyAdjustment bucket contribution (the residual), or 0m if absent.
    private static decimal MethodologyAdjustmentOf(PairDivergence divergence) =>
        divergence.Attribution
            .Where(bucket => string.Equals(bucket.Bucket, "MethodologyAdjustment", StringComparison.Ordinal))
            .Select(bucket => bucket.Notches)
            .FirstOrDefault();

    // Weighting bucket: same inputs, different factor weights. Over factors present in BOTH providers,
    // Σ (weightA − weightB) × symmetric-average normalized score, scaled to notches. Overlay factors
    // (present in one provider only) are excluded here and land in the residual.
    private static decimal WeightingContribution(ProviderRating a, ProviderRating b)
    {
        decimal sum = 0m;
        foreach (RatingFactor fa in a.Factors)
        {
            RatingFactor? fb = FindFactor(b, fa.Name);
            if (fb is null)
            {
                continue;
            }

            decimal normalizedAverageScore = (fa.Score + fb.Score) / 2m / 100m;
            sum += (fa.Weight - fb.Weight) * normalizedAverageScore;
        }

        return WeightingNotchScale * sum;
    }

    // A provider's leverage contribution: the notches implied by its own-vintage Debt/EBITDA relative
    // to the latest filing. A null own-vintage snapshot, a null own-vintage leverage, or a null latest
    // leverage ⇒ 0m — no honest recompute is possible, so the effect folds into the residual (P1).
    private static decimal LeverageContribution(FundamentalSnapshot? providerInputs, FundamentalSnapshot latest)
    {
        if (providerInputs?.DebtToEbitda is not decimal providerLeverage ||
            latest.DebtToEbitda is not decimal latestLeverage)
        {
            return 0m;
        }

        return LeverageNotchScale * (providerLeverage - latestLeverage);
    }

    // Deterministic provenance (P3): the evidence refs behind each bucket, de-duplicated and stable in
    // first-occurrence order.
    private static IReadOnlyList<string> RefsFor(ProviderRating a, ProviderRating b, string kind)
    {
        var refs = new List<string>();
        switch (kind)
        {
            case "weight": // shared factors whose weights differ, plus both methodology documents.
                foreach (RatingFactor fa in a.Factors)
                {
                    RatingFactor? fb = FindFactor(b, fa.Name);
                    if (fb is not null && fa.Weight != fb.Weight)
                    {
                        refs.Add(fa.SourceRef);
                        refs.Add(fb.SourceRef);
                    }
                }

                refs.Add(a.MethodologyDocId);
                refs.Add(b.MethodologyDocId);
                break;

            case "input": // each provider's leverage-factor citation.
                AddLeverageRefs(a, refs);
                AddLeverageRefs(b, refs);
                break;

            case "overlay": // factors present in exactly one provider (the overlay drivers).
                AddOverlayRefs(a, b, refs);
                AddOverlayRefs(b, a, refs);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown evidence kind.");
        }

        return refs.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static void AddLeverageRefs(ProviderRating rating, List<string> refs)
    {
        foreach (RatingFactor f in rating.Factors)
        {
            if (string.Equals(f.Name, LeverageFactorName, StringComparison.Ordinal))
            {
                refs.Add(f.SourceRef);
            }
        }
    }

    private static void AddOverlayRefs(ProviderRating source, ProviderRating other, List<string> refs)
    {
        foreach (RatingFactor f in source.Factors)
        {
            if (FindFactor(other, f.Name) is null)
            {
                refs.Add(f.SourceRef);
            }
        }
    }

    private static RatingFactor? FindFactor(ProviderRating rating, string name) =>
        rating.Factors.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal));
}
