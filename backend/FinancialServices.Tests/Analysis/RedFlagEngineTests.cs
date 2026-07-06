using FinancialServices.Api.Analysis;
using FinancialServices.Api.Models;
using FinancialServices.Tests.TestSupport;
using FluentAssertions;
using Xunit;

namespace FinancialServices.Tests.Analysis;

/// <summary>
/// Deterministic red-flag-engine tests (P2 — no LLM, no network). Locks the money moment (NordStar ⇒
/// exactly one STALE_INPUT high, zero on Cedar Grove), the structural rules (MISSING_COVERAGE,
/// OUTLIER_PROVIDER), and the pair-level METHODOLOGY_CONFLICT threshold. Severity is fixed by rule and
/// <see cref="RedFlag.Narrative"/> stays empty (the LLM fills it later).
/// </summary>
public sealed class RedFlagEngineTests
{
    private static readonly RedFlagEngine Engine = new();
    private static readonly IReadOnlyList<PairDivergence> NoDivergences = Array.Empty<PairDivergence>();

    [Fact]
    public void Evaluate_NordStar_FiresExactlyOneStaleInputHighOnMsci()
    {
        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.NordStarIssuer(), PrismFixtures.NordStarRatings(),
            PrismFixtures.NordStarLatest(), NoDivergences);

        RedFlag[] stale = flags.Where(f => f.Code == "STALE_INPUT").ToArray();
        stale.Should().ContainSingle();

        RedFlag flag = stale[0];
        flag.Severity.Should().Be("high");
        flag.Narrative.Should().BeEmpty();
        flag.EvidenceRefs.Should().Contain("nordstar-Msci");           // the MSCI card
        flag.EvidenceRefs.Should().Contain("edgar:0000320193:10-Q");   // the real EDGAR filing
        flag.Rule.Should().Contain("2025-08-20");                      // MSCI stale input date
        flag.Rule.Should().Contain("2025-11-05");                      // real EDGAR filing date
        flag.Rule.Should().Contain("10-Q");
    }

    [Fact]
    public void Evaluate_CedarGrove_ProducesZeroFlags()
    {
        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.CedarGroveIssuer(), PrismFixtures.CedarGroveRatings(),
            PrismFixtures.CedarGroveLatest(), NoDivergences);

        flags.Should().BeEmpty();
    }

    [Fact]
    public void Evaluate_SameUtcDayInput_DoesNotFireStale()
    {
        // Moody's rating action is on the SAME UTC calendar day as the filing (earlier instant, via a
        // -05:00 offset). An instant "<" comparison would falsely fire STALE; date-only UTC must not.
        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.SameDayIssuer(), PrismFixtures.SameDayRatings(),
            PrismFixtures.SameDayLatest(), NoDivergences);

        flags.Should().NotContain(f => f.Code == "STALE_INPUT");
    }

    [Fact]
    public void Evaluate_AsterBio_FiresOneMissingCoverageMedium()
    {
        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.AsterBioIssuer(), PrismFixtures.AsterBioRatings(),
            PrismFixtures.AsterBioLatest(), NoDivergences);

        RedFlag[] missing = flags.Where(f => f.Code == "MISSING_COVERAGE").ToArray();
        missing.Should().ContainSingle();
        missing[0].Severity.Should().Be("medium");
        missing[0].Rule.Should().Contain("Msci");             // the absent provider
        missing[0].EvidenceRefs.Should().Contain("aster-bio");
    }

    [Fact]
    public void Evaluate_Onyx_FiresOneOutlierProviderMedium()
    {
        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.OnyxIssuer(), PrismFixtures.OnyxRatings(),
            PrismFixtures.OnyxLatest(), NoDivergences);

        RedFlag[] outliers = flags.Where(f => f.Code == "OUTLIER_PROVIDER").ToArray();
        outliers.Should().ContainSingle();
        outliers[0].Severity.Should().Be("medium");
        outliers[0].Rule.Should().Contain("Msci");            // 4 notches from the {6,6,10} median
        outliers[0].EvidenceRefs.Should().Contain("onyx-Msci");
    }

    [Fact]
    public void Evaluate_MethodologyConflict_FiresWhenGapWideAndOverlayDominant()
    {
        // Cedar Grove base state is flag-free, so any flag here is the injected divergence's.
        var conflict = new PairDivergence(Provider.Moodys, Provider.Msci, 3, new[]
        {
            new BucketAttribution("Weighting", 0.5m, "", Array.Empty<string>()),
            new BucketAttribution("Input", 0.5m, "", Array.Empty<string>()),
            new BucketAttribution("MethodologyAdjustment", 2.0m, "", Array.Empty<string>()), // 2/3 ≈ 67% > 50%
        });

        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.CedarGroveIssuer(), PrismFixtures.CedarGroveRatings(),
            PrismFixtures.CedarGroveLatest(), new[] { conflict });

        RedFlag[] conflicts = flags.Where(f => f.Code == "METHODOLOGY_CONFLICT").ToArray();
        conflicts.Should().ContainSingle();
        conflicts[0].Severity.Should().Be("medium");
        conflicts[0].Narrative.Should().BeEmpty();
        conflicts[0].Rule.Should().Contain("methodology");
        conflicts[0].EvidenceRefs.Should().Contain("cedar-grove-Moodys");
        conflicts[0].EvidenceRefs.Should().Contain("cedar-grove-Msci");
    }

    [Fact]
    public void Evaluate_MethodologyConflict_DoesNotFireBelowThresholds()
    {
        // Overlay minority (1/3 of a 3-notch gap) and a narrow (1-notch) gap both stay silent.
        var overlayMinority = new PairDivergence(Provider.Moodys, Provider.Msci, 3, new[]
        {
            new BucketAttribution("Weighting", 1.0m, "", Array.Empty<string>()),
            new BucketAttribution("Input", 1.0m, "", Array.Empty<string>()),
            new BucketAttribution("MethodologyAdjustment", 1.0m, "", Array.Empty<string>()), // 1/3 ≈ 33% ≤ 50%
        });
        var narrowGap = new PairDivergence(Provider.Moodys, Provider.MorningstarDbrs, 1, new[]
        {
            new BucketAttribution("Weighting", 0m, "", Array.Empty<string>()),
            new BucketAttribution("Input", 0m, "", Array.Empty<string>()),
            new BucketAttribution("MethodologyAdjustment", 1.0m, "", Array.Empty<string>()), // |gap| 1 < 2
        });

        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.CedarGroveIssuer(), PrismFixtures.CedarGroveRatings(),
            PrismFixtures.CedarGroveLatest(), new[] { overlayMinority, narrowGap });

        flags.Should().NotContain(f => f.Code == "METHODOLOGY_CONFLICT");
    }
}
