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

    // ── R1 — IG/HY boundary straddle ────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_IgHyStraddle_FiresExactlyOneHighBoundaryFlag()
    {
        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.IgHyStraddleIssuer(), PrismFixtures.IgHyStraddleRatings(),
            PrismFixtures.IgHyStraddleLatest(), NoDivergences);

        RedFlag[] boundary = flags.Where(f => f.Code == "IG_HY_BOUNDARY").ToArray();
        boundary.Should().ContainSingle();
        boundary[0].Severity.Should().Be("high");
        boundary[0].Narrative.Should().BeEmpty();
        boundary[0].Rule.Should().Contain("investment-grade");
        boundary[0].Rule.Should().Contain("high yield");
        boundary[0].EvidenceRefs.Should().Contain("meridian");
        boundary[0].EvidenceRefs.Should().Contain("meridian-MorningstarDbrs"); // the HY side card
        // P4: no trading vocabulary in the deterministic rule text.
        boundary[0].Rule.ToLowerInvariant().Should()
            .NotContainAny("buy", "sell", " hold", "recommend", "allocate", "signal", "alpha");
    }

    [Fact]
    public void Evaluate_AllInvestmentGrade_DoesNotFireIgHyBoundary()
    {
        // NordStar notches {9, 9, 10} are all investment grade ⇒ no straddle.
        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.NordStarIssuer(), PrismFixtures.NordStarRatings(),
            PrismFixtures.NordStarLatest(), NoDivergences);

        flags.Should().NotContain(f => f.Code == "IG_HY_BOUNDARY");
    }

    // ── R5 — STALE_INPUT materiality window ─────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_StaleBelowMaterialityWindow_DoesNotFireStale()
    {
        // A rating action 11 days before the latest filing is within the 45-day materiality window —
        // the agency likely saw substantially the same fundamentals, so no STALE_INPUT.
        var issuer = PrismFixtures.Issuer("verdant", "Verdant Water Co.", "VRDW", "0000070011");
        FundamentalSnapshot latest = PrismFixtures.Snapshot("verdant", PrismFixtures.D(2025, 11, 5), "10-Q", debtToEbitda: 3.0m);
        var ratings = new[]
        {
            PrismFixtures.Rating(Provider.Moodys, "A2", inputAsOf: PrismFixtures.D(2025, 10, 25)),          // 11 days
            PrismFixtures.Rating(Provider.MorningstarDbrs, "A (mid)", inputAsOf: PrismFixtures.D(2025, 11, 1)),
            PrismFixtures.Rating(Provider.Msci, "A", inputAsOf: PrismFixtures.D(2025, 11, 2)),
        };

        IReadOnlyList<RedFlag> flags = Engine.Evaluate(issuer, ratings, latest, NoDivergences);

        flags.Should().NotContain(f => f.Code == "STALE_INPUT");
    }

    // ── R4 — OUTLIER unique-max distance ────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_SymmetricSpread_DoesNotDoubleFireOutlier()
    {
        // Notches {11, 14, 17}: median 14, both ends 3 away (a tie). Neither is the LONE outlier, so
        // OUTLIER_PROVIDER must stay silent (the old "distance >= 3" rule wrongly flagged both).
        var issuer = PrismFixtures.Issuer("summit", "Summit Alloys Ltd.", "SMTA", "0000081020");
        FundamentalSnapshot latest = PrismFixtures.Snapshot("summit", PrismFixtures.D(2025, 11, 5), "10-Q", debtToEbitda: 5.0m);
        var ratings = new[]
        {
            PrismFixtures.Rating(Provider.Moodys, "BB+", inputAsOf: PrismFixtures.D(2025, 11, 10)),   // notch 11
            PrismFixtures.Rating(Provider.MorningstarDbrs, "B+", inputAsOf: PrismFixtures.D(2025, 11, 10)), // notch 14
            PrismFixtures.Rating(Provider.Msci, "CCC+", inputAsOf: PrismFixtures.D(2025, 11, 10)),    // notch 17
        };

        IReadOnlyList<RedFlag> flags = Engine.Evaluate(issuer, ratings, latest, NoDivergences);

        flags.Should().NotContain(f => f.Code == "OUTLIER_PROVIDER");
    }

    // ── R6 — PROVIDER_UNDER_REVIEW ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_ProviderUnderReview_FiresOneLowFlag()
    {
        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.UnderReviewIssuer(), PrismFixtures.UnderReviewRatings(),
            PrismFixtures.UnderReviewLatest(), NoDivergences);

        RedFlag[] review = flags.Where(f => f.Code == "PROVIDER_UNDER_REVIEW").ToArray();
        review.Should().ContainSingle();
        review[0].Severity.Should().Be("low");
        review[0].Narrative.Should().BeEmpty();
        review[0].EvidenceRefs.Should().Contain("beacon-Msci");
    }

    // ── R3 — METHODOLOGY_CONFLICT suppressed when a provider is stale ────────────────────────────────

    [Fact]
    public void Evaluate_MethodologyConflict_SuppressedWhenProviderStale()
    {
        // MSCI's input is 96 days stale, forming a 4-notch, residual-dominated gap with fresh Moody's.
        // The STALE_INPUT flag already explains the gap, so METHODOLOGY_CONFLICT must NOT also fire.
        var issuer = PrismFixtures.Issuer("harbor", "Harbor Logistics Inc.", "HRBL", "0000091044");
        FundamentalSnapshot latest = PrismFixtures.Snapshot("harbor", PrismFixtures.D(2025, 11, 5), "10-Q", debtToEbitda: 3.0m);
        var ratings = new[]
        {
            PrismFixtures.Rating(Provider.Moodys, "A2", inputAsOf: PrismFixtures.D(2025, 11, 10)),  // notch 6, fresh
            PrismFixtures.Rating(Provider.Msci, "BBB-", inputAsOf: PrismFixtures.D(2025, 8, 1)),    // notch 10, 96d stale
        };
        var wideResidualGap = new PairDivergence(Provider.Moodys, Provider.Msci, 4, new[]
        {
            new BucketAttribution("Weighting", 0m, "", Array.Empty<string>()),
            new BucketAttribution("Input", 0m, "", Array.Empty<string>()),
            new BucketAttribution("MethodologyAdjustment", 4m, "", Array.Empty<string>()), // 100% residual
        });

        IReadOnlyList<RedFlag> flags = Engine.Evaluate(issuer, ratings, latest, new[] { wideResidualGap });

        flags.Should().Contain(f => f.Code == "STALE_INPUT");
        flags.Should().NotContain(f => f.Code == "METHODOLOGY_CONFLICT");
    }

    // ── F1 — METHODOLOGY_CONFLICT suppressed when the pair's provider is the lone OUTLIER ────────────

    [Fact]
    public void Evaluate_MethodologyConflict_SuppressedForTheOutlierPairs()
    {
        // Onyx {6,6,10}: MSCI is the lone outlier at distance 4. Both wide pairs (Moody's-MSCI and
        // DBRS-MSCI) are the SAME outlier gap — the OUTLIER flag already carries it, so no pair should
        // ALSO fire METHODOLOGY_CONFLICT (F1: one divergence is never triple-reported).
        ProviderRating[] ratings = PrismFixtures.OnyxRatings().ToArray();
        var decomposer = new DivergenceDecomposer();
        var divergences = new List<PairDivergence>();
        for (int i = 0; i < ratings.Length; i++)
        {
            for (int j = i + 1; j < ratings.Length; j++)
            {
                divergences.Add(decomposer.Decompose(
                    ratings[i], ratings[j], PrismFixtures.OnyxLatest(), aInputs: null, bInputs: null));
            }
        }

        IReadOnlyList<RedFlag> flags = Engine.Evaluate(
            PrismFixtures.OnyxIssuer(), ratings, PrismFixtures.OnyxLatest(), divergences);

        flags.Should().ContainSingle(f => f.Code == "OUTLIER_PROVIDER");
        flags.Should().NotContain(f => f.Code == "METHODOLOGY_CONFLICT");
    }

    // ── F5a — STALE_INPUT materiality window is exact at 45 days ─────────────────────────────────────

    [Theory]
    [InlineData(45, false)] // exactly at the window ⇒ not yet material
    [InlineData(46, true)]  // one day past ⇒ material
    public void Evaluate_StaleInput_FiresOnlyStrictlyBeyondTheMaterialityWindow(int daysStale, bool expectFlag)
    {
        DateTimeOffset filing = PrismFixtures.D(2025, 11, 5);
        var issuer = PrismFixtures.Issuer("cobalt", "Cobalt Ridge Mining Inc.", "CBLT", "0000073099");
        FundamentalSnapshot latest = PrismFixtures.Snapshot("cobalt", filing, "10-Q", debtToEbitda: 4.0m);
        var ratings = new[]
        {
            PrismFixtures.Rating(Provider.Moodys, "Ba1", inputAsOf: filing.AddDays(-daysStale)),
            PrismFixtures.Rating(Provider.MorningstarDbrs, "BB (high)", inputAsOf: filing),
            PrismFixtures.Rating(Provider.Msci, "BB+", inputAsOf: filing),
        };

        IReadOnlyList<RedFlag> flags = Engine.Evaluate(issuer, ratings, latest, NoDivergences);

        flags.Any(f => f.Code == "STALE_INPUT").Should().Be(expectFlag);
    }

    // ── F5b — stable emission order ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_EmitsFlagsInStableSeverityGroupedOrder()
    {
        // An issuer that trips STALE, IG_HY, OUTLIER and UNDER_REVIEW at once, to pin the documented
        // emission order: STALE_INPUT → IG_HY_BOUNDARY → MISSING_COVERAGE → OUTLIER_PROVIDER →
        // PROVIDER_UNDER_REVIEW → METHODOLOGY_CONFLICT.
        var issuer = PrismFixtures.Issuer("zenith", "Zenith Grid Systems Inc.", "ZNTH", "0000064500");
        FundamentalSnapshot latest = PrismFixtures.Snapshot("zenith", PrismFixtures.D(2025, 11, 5), "10-Q", debtToEbitda: 6.0m);
        var ratings = new[]
        {
            // {6, 12, 17}: median 12; MSCI at 17 is the lone outlier (distance 5). IG (6) + HY (12,17)
            // straddle. Moody's action is 120 days stale. MSCI is under review.
            PrismFixtures.Rating(Provider.Moodys, "A2", inputAsOf: PrismFixtures.D(2025, 7, 8)),   // notch 6, stale, IG
            PrismFixtures.Rating(Provider.MorningstarDbrs, "BB", inputAsOf: PrismFixtures.D(2025, 11, 4)), // notch 12, HY
            PrismFixtures.Rating(Provider.Msci, "CCC+", inputAsOf: PrismFixtures.D(2025, 11, 4), underReview: true), // notch 17
        };

        string[] codes = Engine.Evaluate(issuer, ratings, latest, NoDivergences).Select(f => f.Code).ToArray();

        int[] positions =
        {
            Array.IndexOf(codes, "STALE_INPUT"),
            Array.IndexOf(codes, "IG_HY_BOUNDARY"),
            Array.IndexOf(codes, "OUTLIER_PROVIDER"),
            Array.IndexOf(codes, "PROVIDER_UNDER_REVIEW"),
        };
        positions.Should().OnlyContain(p => p >= 0);
        positions.Should().BeInAscendingOrder();
    }
}
