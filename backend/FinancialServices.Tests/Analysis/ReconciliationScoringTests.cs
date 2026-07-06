using FinancialServices.Api.Analysis;
using FinancialServices.Api.Models;
using FinancialServices.Tests.TestSupport;
using FluentAssertions;
using Xunit;

namespace FinancialServices.Tests.Analysis;

/// <summary>
/// Deterministic reconciliation-scoring tests (P2 — no LLM, no network). Pins the confidence formula
/// (coverage minus severity penalties, clamped) against the named casts and the consensus wording
/// across the notch-spread boundary.
/// </summary>
public sealed class ReconciliationScoringTests
{
    private static readonly DateTimeOffset Anchor = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly RedFlagEngine Engine = new();
    private static readonly IReadOnlyList<PairDivergence> NoDivergences = Array.Empty<PairDivergence>();

    [Fact]
    public void ConfidenceScore_CedarGrove_FullFreshConsensusIsMaximum()
    {
        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.CedarGroveIssuer(), PrismFixtures.CedarGroveRatings(),
            PrismFixtures.CedarGroveLatest(), NoDivergences);

        ReconciliationScoring.ConfidenceScore(PrismFixtures.CedarGroveRatings(), flags)
            .Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void ConfidenceScore_NordStar_SingleHighFlagDropsToPointSeven()
    {
        IReadOnlyList<ProviderRating> ratings = PrismFixtures.NordStarRatings();
        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.NordStarIssuer(), ratings, PrismFixtures.NordStarLatest(), NoDivergences);

        // Full coverage (3/3) minus one high flag (0.30) = 0.70.
        ReconciliationScoring.ConfidenceScore(ratings, flags).Should().BeApproximately(0.70, 1e-9);
    }

    [Fact]
    public void ConfidenceScore_AsterBio_MissingCoverageNotDoublePenalized()
    {
        IReadOnlyList<ProviderRating> ratings = PrismFixtures.AsterBioRatings();
        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.AsterBioIssuer(), ratings, PrismFixtures.AsterBioLatest(), NoDivergences);

        // Coverage is 2/3. The lone flag is MISSING_COVERAGE, already reflected in that fraction, so it
        // adds NO extra penalty (no double-counting) — confidence is exactly 2/3.
        ReconciliationScoring.ConfidenceScore(ratings, flags)
            .Should().BeApproximately(2.0 / 3.0, 1e-9);
    }

    [Theory]
    [InlineData(9, 9, 9, "full consensus")]
    [InlineData(9, 9, 10, "consensus within 1 notch")]
    [InlineData(6, 6, 9, "3-notch split")]
    public void ConsensusSummary_ReflectsNotchSpread(int n1, int n2, int n3, string expected)
    {
        var ratings = new[]
        {
            Rating(Provider.Moodys, n1),
            Rating(Provider.MorningstarDbrs, n2),
            Rating(Provider.Msci, n3),
        };

        ReconciliationScoring.ConsensusSummary(ratings).Should().Be(expected);
    }

    [Fact]
    public void ConsensusSummary_NoRatings_ReportsNoCoverage()
    {
        ReconciliationScoring.ConsensusSummary(Array.Empty<ProviderRating>()).Should().Be("no coverage");
    }

    private static ProviderRating Rating(Provider provider, int notch) =>
        new(provider, "n/a", notch, Anchor, Anchor, Array.Empty<RatingFactor>(), $"method:{provider}");
}
