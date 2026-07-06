using FinancialServices.Api.Analysis;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using FinancialServices.Tests.TestSupport;
using FluentAssertions;
using Xunit;

namespace FinancialServices.Tests.Analysis;

/// <summary>
/// Deterministic divergence-decomposer tests (P2 — no LLM, no network). The centrepiece recomputes each
/// mechanical bucket independently over ~200 seeded random pairs (catching sign/scale drift, not just a
/// tautological <c>sum == gap</c>), pinned by directed weighting-only / input-sign / overlay-only /
/// null-input / rich-pair / letter-only cases plus the standalone invariant guard.
/// </summary>
public sealed class DivergenceDecomposerTests
{
    private static readonly DateTimeOffset Anchor = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly DivergenceDecomposer _decomposer = new();

    // ── Invariant property test (architecturalPlan/11) ─────────────────────────────────────────────

    [Fact]
    public void Decompose_RandomPairs_BucketsMatchIndependentRecomputation()
    {
        var rng = new Random(20260706);
        string[] sharedNames = { "Leverage", "InterestCoverage", "Profitability", "Liquidity", "BusinessRisk" };

        for (int i = 0; i < 200; i++)
        {
            int notchA = rng.Next(1, 22);
            int notchB = rng.Next(1, 22);

            // Bounded-precision weights (2 dp) and integer scores keep every product exact in decimal,
            // so the residual reconstructs the gap without rounding drift.
            int sharedCount = rng.Next(1, sharedNames.Length + 1);
            var aFactors = new List<RatingFactor>();
            var bFactors = new List<RatingFactor>();
            for (int f = 0; f < sharedCount; f++)
            {
                string name = sharedNames[f];
                aFactors.Add(new RatingFactor(name, rng.Next(0, 101) / 100m, rng.Next(0, 101), $"a:{name}"));
                bFactors.Add(new RatingFactor(name, rng.Next(0, 101) / 100m, rng.Next(0, 101), $"b:{name}"));
            }

            // Sometimes give exactly one side an overlay factor (drives the residual bucket).
            switch (rng.Next(0, 3))
            {
                case 1:
                    aFactors.Add(new RatingFactor("SectorEsgOverlay", rng.Next(0, 101) / 100m, rng.Next(0, 101), "a:overlay"));
                    break;
                case 2:
                    bFactors.Add(new RatingFactor("SectorEsgOverlay", rng.Next(0, 101) / 100m, rng.Next(0, 101), "b:overlay"));
                    break;
            }

            var a = new ProviderRating(Provider.Moodys, "n/a", notchA, Anchor, Anchor, aFactors, "method:a");
            var b = new ProviderRating(Provider.Msci, "n/a", notchB, Anchor, Anchor, bFactors, "method:b");

            FundamentalSnapshot latest = PrismFixtures.Snapshot(
                "iss", PrismFixtures.D(2025, 6, 30), debtToEbitda: rng.Next(50, 800) / 100m);

            // A quarter of the time a side's own-vintage snapshot is genuinely absent ⇒ 0m Input (P1).
            FundamentalSnapshot? aInputs = rng.Next(0, 4) == 0
                ? null
                : PrismFixtures.Snapshot("iss", PrismFixtures.D(2024, 6, 30), debtToEbitda: rng.Next(50, 800) / 100m);
            FundamentalSnapshot? bInputs = rng.Next(0, 4) == 0
                ? null
                : PrismFixtures.Snapshot("iss", PrismFixtures.D(2024, 6, 30), debtToEbitda: rng.Next(50, 800) / 100m);

            PairDivergence divergence = _decomposer.Decompose(a, b, latest, aInputs, bInputs);

            // Independently recompute each mechanical bucket from the SAME inputs (not from the
            // decomposer) — this catches a sign flip or scale drift that a bare sum==gap check cannot.
            decimal expectedWeighting = RecomputeWeighting(aFactors, bFactors);
            decimal expectedInput = RecomputeInputLeg(bInputs, latest) - RecomputeInputLeg(aInputs, latest);
            decimal expectedAdj = (notchB - notchA) - expectedWeighting - expectedInput;

            divergence.NotchGap.Should().Be(notchB - notchA);
            Bucket(divergence, "Weighting").Notches.Should().Be(expectedWeighting, "iteration {0} weighting", i);
            Bucket(divergence, "Input").Notches.Should().Be(expectedInput, "iteration {0} input", i);
            Bucket(divergence, "MethodologyAdjustment").Notches.Should()
                .Be(expectedAdj, "iteration {0}: residual == gap - weighting - input", i);
        }
    }

    // ── Directed bucket cases ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Decompose_EqualLeverageDifferentWeights_InputZeroWeightingCarries()
    {
        var a = Rating(Provider.Moodys, notch: 6,
            new RatingFactor("Leverage", 0.40m, 70m, "a:lev"),
            new RatingFactor("Profitability", 0.60m, 80m, "a:prof"));
        var b = Rating(Provider.Msci, notch: 9,
            new RatingFactor("Leverage", 0.20m, 70m, "b:lev"),
            new RatingFactor("Profitability", 0.80m, 80m, "b:prof"));
        FundamentalSnapshot latest = PrismFixtures.Snapshot("iss", PrismFixtures.D(2025, 6, 30), debtToEbitda: 3.0m);
        FundamentalSnapshot equalInputs = PrismFixtures.Snapshot("iss", PrismFixtures.D(2025, 6, 30), debtToEbitda: 3.0m);

        PairDivergence divergence = _decomposer.Decompose(a, b, latest, equalInputs, equalInputs);

        // 5 × [(0.40−0.20)×0.70 + (0.60−0.80)×0.80] = 5 × (0.14 − 0.16) = −0.10.
        Bucket(divergence, "Weighting").Notches.Should().Be(-0.10m);
        Bucket(divergence, "Input").Notches.Should().Be(0m);
        Bucket(divergence, "MethodologyAdjustment").Notches.Should().Be(3.10m);
        Sum(divergence).Should().Be(3m);
    }

    [Fact]
    public void Decompose_StaleProviderHigherLeverage_InputBucketPositiveAndCarriesGap()
    {
        // Identical weights isolate the Input bucket; b is built on older, higher-leverage financials.
        var a = Rating(Provider.Moodys, notch: 6,
            new RatingFactor("Leverage", 0.50m, 70m, "a:lev"),
            new RatingFactor("Profitability", 0.50m, 80m, "a:prof"));
        var b = Rating(Provider.Msci, notch: 8,
            new RatingFactor("Leverage", 0.50m, 70m, "b:lev"),
            new RatingFactor("Profitability", 0.50m, 80m, "b:prof"));
        FundamentalSnapshot latest = PrismFixtures.Snapshot("iss", PrismFixtures.D(2025, 6, 30), debtToEbitda: 3.0m);
        FundamentalSnapshot freshInputs = PrismFixtures.Snapshot("iss", PrismFixtures.D(2025, 6, 30), debtToEbitda: 3.0m);
        FundamentalSnapshot staleInputs = PrismFixtures.Snapshot("iss", PrismFixtures.D(2024, 6, 30), debtToEbitda: 5.0m);

        PairDivergence divergence = _decomposer.Decompose(a, b, latest, freshInputs, staleInputs);

        Bucket(divergence, "Weighting").Notches.Should().Be(0m);
        // 1 × (5.0 − 3.0) − 1 × (3.0 − 3.0) = +2 ⇒ the stale (b) side carries the divergence in the
        // b−a direction (higher leverage = weaker credit = larger notch). Locks the Input sign.
        Bucket(divergence, "Input").Notches.Should().Be(2m);
        Sum(divergence).Should().Be(2m);
    }

    [Fact]
    public void Decompose_OverlayFactorOnOneSide_LandsEntirelyInMethodologyResidual()
    {
        var a = Rating(Provider.Moodys, notch: 6,
            new RatingFactor("Leverage", 0.50m, 70m, "a:lev"),
            new RatingFactor("Profitability", 0.50m, 80m, "a:prof"));
        var b = Rating(Provider.Msci, notch: 9,
            new RatingFactor("Leverage", 0.50m, 70m, "b:lev"),
            new RatingFactor("Profitability", 0.50m, 80m, "b:prof"),
            new RatingFactor("SectorEsgOverlay", 0.10m, 90m, "b:overlay"));
        FundamentalSnapshot latest = PrismFixtures.Snapshot("iss", PrismFixtures.D(2025, 6, 30), debtToEbitda: 3.0m);
        FundamentalSnapshot equalInputs = PrismFixtures.Snapshot("iss", PrismFixtures.D(2025, 6, 30), debtToEbitda: 3.0m);

        PairDivergence divergence = _decomposer.Decompose(a, b, latest, equalInputs, equalInputs);

        Bucket(divergence, "Weighting").Notches.Should().Be(0m);
        Bucket(divergence, "Input").Notches.Should().Be(0m);
        Bucket(divergence, "MethodologyAdjustment").Notches.Should().Be(3m); // the whole gap
        Bucket(divergence, "MethodologyAdjustment").EvidenceRefs.Should().Contain("b:overlay");
    }

    [Fact]
    public void Decompose_NullInputsAndNullLatestLeverage_InputZeroAndInvariantHolds()
    {
        var a = Rating(Provider.Moodys, notch: 5, new RatingFactor("Leverage", 0.50m, 70m, "a:lev"));
        var b = Rating(Provider.Msci, notch: 9, new RatingFactor("Leverage", 0.30m, 70m, "b:lev"));
        FundamentalSnapshot latest = PrismFixtures.Snapshot("iss", PrismFixtures.D(2025, 6, 30), debtToEbitda: 3.0m);

        // Both own-vintage snapshots absent ⇒ Input 0m; the weight delta still reconstructs the gap.
        PairDivergence bothNull = _decomposer.Decompose(a, b, latest, null, null);
        Bucket(bothNull, "Input").Notches.Should().Be(0m);
        Sum(bothNull).Should().Be(4m);

        // Only b has an own-vintage (higher-leverage) snapshot ⇒ Input = +2, a's side is 0m (never faked).
        PairDivergence aNull = _decomposer.Decompose(
            a, b, latest, null, PrismFixtures.Snapshot("iss", PrismFixtures.D(2024, 6, 30), debtToEbitda: 5.0m));
        Bucket(aNull, "Input").Notches.Should().Be(2m);
        Sum(aNull).Should().Be(4m);

        // Latest leverage genuinely absent ⇒ no side can recompute ⇒ Input 0m even with own snapshots.
        FundamentalSnapshot latestNoLeverage = PrismFixtures.Snapshot("iss", PrismFixtures.D(2025, 6, 30), debtToEbitda: null);
        PairDivergence noLatest = _decomposer.Decompose(
            a, b, latestNoLeverage,
            PrismFixtures.Snapshot("iss", PrismFixtures.D(2024, 6, 30), debtToEbitda: 5.0m),
            PrismFixtures.Snapshot("iss", PrismFixtures.D(2024, 6, 30), debtToEbitda: 4.0m));
        Bucket(noLatest, "Input").Notches.Should().Be(0m);
        Sum(noLatest).Should().Be(4m);
    }

    [Fact]
    public void Decompose_PopulatesDeterministicEvidenceRefs()
    {
        var a = Rating(Provider.Moodys, notch: 6,
            new RatingFactor("Leverage", 0.40m, 70m, "a:lev"),
            new RatingFactor("Profitability", 0.60m, 80m, "a:prof"));
        var b = Rating(Provider.Msci, notch: 9,
            new RatingFactor("Leverage", 0.20m, 70m, "b:lev"),
            new RatingFactor("Profitability", 0.80m, 80m, "b:prof"));
        FundamentalSnapshot latest = PrismFixtures.Snapshot("iss", PrismFixtures.D(2025, 6, 30), debtToEbitda: 3.0m);
        FundamentalSnapshot inputs = PrismFixtures.Snapshot("iss", PrismFixtures.D(2025, 6, 30), debtToEbitda: 3.0m);

        PairDivergence divergence = _decomposer.Decompose(a, b, latest, inputs, inputs);

        // Weighting refs = differing shared factors + both methodology docs (P3 provenance).
        Bucket(divergence, "Weighting").EvidenceRefs.Should()
            .Contain(new[] { "a:lev", "b:lev", "a:prof", "b:prof", "method:Moodys", "method:Msci" });
        // Input refs = each provider's leverage factor citation.
        Bucket(divergence, "Input").EvidenceRefs.Should().Contain(new[] { "a:lev", "b:lev" });
    }

    [Fact]
    public void Decompose_RichPair_AttributesBothWeightingAndInput()
    {
        // Shared factors (different weights AND scores) + per-provider vintage snapshots (different
        // leverage) so BOTH mechanical buckets are non-zero: attribution works when the data exists.
        PairDivergence divergence = _decomposer.Decompose(
            PrismFixtures.HalcyonRatingA(), PrismFixtures.HalcyonRatingB(),
            PrismFixtures.HalcyonLatest(), PrismFixtures.HalcyonInputsA(), PrismFixtures.HalcyonInputsB());

        Bucket(divergence, "Weighting").Notches.Should().NotBe(0m);
        Bucket(divergence, "Input").Notches.Should().NotBe(0m);
        Sum(divergence).Should().Be(divergence.NotchGap);
        DivergenceDecomposer.IsResidualDominated(divergence).Should().BeFalse();
    }

    [Fact]
    public void Decompose_LetterOnlyPair_ResidualDominatedAndHonest()
    {
        // No factors, no vintage snapshots so Weighting and Input are both 0m and the residual absorbs
        // the whole gap. The decomposer surfaces this honestly instead of faking a precise split.
        PairDivergence divergence = _decomposer.Decompose(
            PrismFixtures.LetterOnlyRatingA(), PrismFixtures.LetterOnlyRatingB(),
            PrismFixtures.LetterOnlyLatest(), null, null);

        Bucket(divergence, "Weighting").Notches.Should().Be(0m);
        Bucket(divergence, "Input").Notches.Should().Be(0m);
        Bucket(divergence, "MethodologyAdjustment").Notches.Should().Be(divergence.NotchGap);
        DivergenceDecomposer.ResidualShare(divergence).Should().Be(1.0m);
        DivergenceDecomposer.IsResidualDominated(divergence).Should().BeTrue();
    }

    // ── Invariant guard (Task 5.3 + forced-throw directed case) ────────────────────────────────────

    [Fact]
    public void EnsureBucketsReconcile_ValidResidual_DoesNotThrow()
    {
        Action act = () => DivergenceDecomposer.EnsureBucketsReconcile(1.6m, 0.4m, 1.0m, 3);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureBucketsReconcile_BrokenSum_ThrowsDomainInvariant()
    {
        // Force the guard: 1 + 1 + 1 ≠ 5. Proves the defensive invariant fails loud when broken.
        Action act = () => DivergenceDecomposer.EnsureBucketsReconcile(1m, 1m, 1m, 5);

        act.Should().Throw<DomainInvariantException>()
            .Which.Invariant.Should().Be("attribution.sum==gap");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────────────────────────

    private static ProviderRating Rating(Provider provider, int notch, params RatingFactor[] factors) =>
        new(provider, "n/a", notch, Anchor, Anchor, factors, $"method:{provider}");

    private static BucketAttribution Bucket(PairDivergence divergence, string name) =>
        divergence.Attribution.Single(bucket => bucket.Bucket == name);

    private static decimal Sum(PairDivergence divergence) =>
        divergence.Attribution.Sum(bucket => bucket.Notches);

    // Independent (test-side) recomputations mirroring the decomposer's public contract: Weighting =
    // 5 * sum over shared factors (wA - wB) * avg(score)/100; each Input leg = 1 * (ownLeverage -
    // latest), 0m when either leverage is absent. Locks the scale constants (5m / 1m) and b-a direction.
    private static decimal RecomputeWeighting(IReadOnlyList<RatingFactor> aFactors, IReadOnlyList<RatingFactor> bFactors)
    {
        decimal sum = 0m;
        foreach (RatingFactor fa in aFactors)
        {
            RatingFactor? fb = bFactors.FirstOrDefault(f => string.Equals(f.Name, fa.Name, StringComparison.Ordinal));
            if (fb is null)
            {
                continue;
            }

            sum += (fa.Weight - fb.Weight) * ((fa.Score + fb.Score) / 2m / 100m);
        }

        return 5m * sum;
    }

    private static decimal RecomputeInputLeg(FundamentalSnapshot? side, FundamentalSnapshot latest)
    {
        if (side?.DebtToEbitda is not decimal ownLeverage || latest.DebtToEbitda is not decimal latestLeverage)
        {
            return 0m;
        }

        return 1m * (ownLeverage - latestLeverage);
    }
}
