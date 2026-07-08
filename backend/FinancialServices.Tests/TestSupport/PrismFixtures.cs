using FinancialServices.Api.Analysis;
using FinancialServices.Api.Models;

namespace FinancialServices.Tests.TestSupport;

/// <summary>
/// Constructed Prism fixtures for the deterministic-core tests (P2 — no corpus, no network, no LLM).
/// The named casts mirror the package-03 story: <b>NordStar</b> (MSCI built on stale financials — the
/// money moment), <b>Cedar Grove</b> (fresh full consensus), <b>Aster Bio</b> (a provider missing), and
/// <b>Onyx</b> (one provider a notch outlier). Every rating's notch comes from the real
/// <see cref="NotchLadder"/>, so the fixtures exercise the production ladder, not hand-picked integers.
/// </summary>
internal static class PrismFixtures
{
    public static DateTimeOffset D(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, TimeSpan.Zero);

    public static RatingFactor Factor(string name, decimal weight, decimal score, string? sourceRef = null) =>
        new(name, weight, score, sourceRef ?? $"ref:{name}");

    public static Issuer Issuer(
        string id = "nordstar",
        string legalName = "NordStar Components Inc.",
        string ticker = "NRDS",
        string cik = "0000320193",
        string sector = "Industrials",
        string sampleBondIsin = "US65473PAA12") =>
        new(id, legalName, ticker, cik, sector, sampleBondIsin);

    public static FundamentalSnapshot Snapshot(
        string issuerId,
        DateTimeOffset filingDate,
        string filingType = "10-Q",
        decimal? debtToEbitda = null,
        decimal? interestCoverage = null,
        decimal? cashAndEquivalents = null) =>
        new(issuerId, filingDate, filingType, debtToEbitda, interestCoverage, cashAndEquivalents);

    public static ProviderRating Rating(
        Provider provider,
        string letter,
        DateTimeOffset inputAsOf,
        DateTimeOffset? asOf = null,
        IReadOnlyList<RatingFactor>? factors = null,
        string? methodologyDocId = null,
        RatingOutlook outlook = RatingOutlook.Unknown,
        bool underReview = false) =>
        new(
            provider,
            letter,
            NotchLadder.ToNotch(letter),
            asOf ?? inputAsOf,
            inputAsOf,
            factors ?? Array.Empty<RatingFactor>(),
            methodologyDocId ?? $"method:{provider}",
            outlook,
            underReview);

    // ── NordStar: Q3 filing on 2025-11-05. MSCI's input predates it (STALE_INPUT money moment);   ──
    // ── Moody's + DBRS are fresh. Notches {9, 9, 10}: a consensus with one stale card, no outlier. ──
    public static Issuer NordStarIssuer() =>
        Issuer("nordstar", "NordStar Components Inc.", "NRDS", "0000320193");

    public static FundamentalSnapshot NordStarLatest() =>
        Snapshot("nordstar", D(2025, 11, 5), "10-Q", debtToEbitda: 3.0m, interestCoverage: 6.0m, cashAndEquivalents: 400m);

    public static IReadOnlyList<ProviderRating> NordStarRatings() => new[]
    {
        Rating(Provider.Moodys, "Baa2", inputAsOf: D(2025, 11, 10)),              // notch 9, fresh
        Rating(Provider.MorningstarDbrs, "BBB (mid)", inputAsOf: D(2025, 11, 8)), // notch 9, fresh
        Rating(Provider.Msci, "BBB-", inputAsOf: D(2025, 8, 20)),                 // notch 10, STALE
    };

    // ── Cedar Grove: three fresh cards, identical notch (6) ⇒ full consensus, zero flags. ──
    public static Issuer CedarGroveIssuer() =>
        Issuer("cedar-grove", "Cedar Grove Utilities LLC", "CDGU", "0000021344");

    public static FundamentalSnapshot CedarGroveLatest() =>
        Snapshot("cedar-grove", D(2025, 11, 5), "10-Q", debtToEbitda: 2.5m, interestCoverage: 8.0m, cashAndEquivalents: 600m);

    public static IReadOnlyList<ProviderRating> CedarGroveRatings() => new[]
    {
        Rating(Provider.Moodys, "A2", inputAsOf: D(2025, 11, 10)),               // notch 6, fresh
        Rating(Provider.MorningstarDbrs, "A (mid)", inputAsOf: D(2025, 11, 12)), // notch 6, fresh
        Rating(Provider.Msci, "A", inputAsOf: D(2025, 11, 11)),                  // notch 6, fresh
    };

    // ── Aster Bio: only Moody's + DBRS publish; MSCI is absent ⇒ one MISSING_COVERAGE. Fresh inputs. ──
    public static Issuer AsterBioIssuer() =>
        Issuer("aster-bio", "Aster Bio Therapeutics Inc.", "ASTB", "0000019617");

    public static FundamentalSnapshot AsterBioLatest() =>
        Snapshot("aster-bio", D(2025, 11, 5), "10-Q", debtToEbitda: 4.0m, interestCoverage: 3.5m, cashAndEquivalents: 250m);

    public static IReadOnlyList<ProviderRating> AsterBioRatings() => new[]
    {
        Rating(Provider.Moodys, "Ba1", inputAsOf: D(2025, 11, 9)),                // notch 11, fresh
        Rating(Provider.MorningstarDbrs, "BB (high)", inputAsOf: D(2025, 11, 9)), // notch 11, fresh
    };

    // ── Onyx: all three publish and are fresh, but MSCI sits 4 notches from the {6, 6, 10} median ⇒ ──
    // ── one OUTLIER_PROVIDER.                                                                       ──
    public static Issuer OnyxIssuer() =>
        Issuer("onyx", "Onyx Midstream Partners LP", "ONXM", "0000034088");

    public static FundamentalSnapshot OnyxLatest() =>
        Snapshot("onyx", D(2025, 11, 5), "10-Q", debtToEbitda: 3.2m, interestCoverage: 5.0m, cashAndEquivalents: 320m);

    public static IReadOnlyList<ProviderRating> OnyxRatings() => new[]
    {
        Rating(Provider.Moodys, "A2", inputAsOf: D(2025, 11, 10)),               // notch 6, fresh
        Rating(Provider.MorningstarDbrs, "A (mid)", inputAsOf: D(2025, 11, 10)), // notch 6, fresh
        Rating(Provider.Msci, "BBB-", inputAsOf: D(2025, 11, 10)),               // notch 10, outlier
    };

    // ── Same-UTC-day boundary (Fix 1): Moody's rating action is EARLIER in the day but on the SAME    ──
    // ── UTC calendar day as the filing, via a different offset (2025-11-04T21:00-05:00 == 2025-11-05  ──
    // ── T02:00Z). An instant "<" comparison would FALSELY fire STALE; the date-only UTC compare must  ──
    // ── not. DBRS + MSCI are genuinely fresh. Notches {9, 9, 10} ⇒ no outlier, full coverage.         ──
    public static Issuer SameDayIssuer() =>
        Issuer("same-day", "Same Day Metals Corp.", "SDMC", "0000102344");

    public static FundamentalSnapshot SameDayLatest() =>
        new("same-day", new DateTimeOffset(2025, 11, 5, 12, 0, 0, TimeSpan.Zero), "10-Q",
            DebtToEbitda: 3.0m, InterestCoverage: 6.0m, CashAndEquivalents: 400m);

    public static IReadOnlyList<ProviderRating> SameDayRatings() => new[]
    {
        // 2025-11-04T21:00-05:00 == 2025-11-05T02:00Z: same UTC day as the filing, earlier instant,
        // different offset. Instant "<" fires; date-only UTC must not.
        Rating(Provider.Moodys, "Baa2",
            inputAsOf: new DateTimeOffset(2025, 11, 4, 21, 0, 0, TimeSpan.FromHours(-5))), // notch 9
        Rating(Provider.MorningstarDbrs, "BBB (mid)", inputAsOf: D(2025, 11, 8)),          // notch 9, fresh
        Rating(Provider.Msci, "BBB-", inputAsOf: D(2025, 11, 7)),                          // notch 10, fresh
    };

    // ── Halcyon (Fix 5): a RICH pair proving attribution works when the data exists — shared factors  ──
    // ── with DIFFERENT weights AND scores (non-zero Weighting) plus per-provider vintage snapshots     ──
    // ── with DIFFERENT leverage (non-zero Input). Used directly by the decomposer's "attributes when   ──
    // ── data exists" directed test (not an issuer-level cast).                                          ──
    public static ProviderRating HalcyonRatingA() =>
        Rating(Provider.Moodys, "Baa1", inputAsOf: D(2025, 11, 10), factors: new[]
        {
            Factor("Leverage", 0.50m, 60m, "halcyon:moodys:lev"),
            Factor("Profitability", 0.50m, 70m, "halcyon:moodys:prof"),
        });

    public static ProviderRating HalcyonRatingB() =>
        Rating(Provider.Msci, "BBB-", inputAsOf: D(2025, 11, 9), factors: new[]
        {
            Factor("Leverage", 0.30m, 80m, "halcyon:msci:lev"),
            Factor("Profitability", 0.70m, 90m, "halcyon:msci:prof"),
        });

    public static FundamentalSnapshot HalcyonLatest() =>
        Snapshot("halcyon", D(2025, 11, 5), debtToEbitda: 3.0m);

    public static FundamentalSnapshot HalcyonInputsA() =>
        Snapshot("halcyon", D(2025, 6, 30), debtToEbitda: 2.5m);   // a's own vintage: lower leverage

    public static FundamentalSnapshot HalcyonInputsB() =>
        Snapshot("halcyon", D(2025, 3, 31), debtToEbitda: 4.5m);   // b's older vintage: higher leverage

    // ── Letter-only pair (Fix 5): no factors, no per-provider vintage snapshots ⇒ Weighting AND Input  ──
    // ── are structurally 0m, so the residual absorbs 100% of the gap. Proves honest degradation         ──
    // ── (IsResidualDominated == true) — the decomposition never fakes a precise split it can't support. ──
    public static ProviderRating LetterOnlyRatingA() =>
        Rating(Provider.Moodys, "A2", inputAsOf: D(2025, 11, 10));   // notch 6, empty factors

    public static ProviderRating LetterOnlyRatingB() =>
        Rating(Provider.Msci, "BBB-", inputAsOf: D(2025, 11, 9));    // notch 10, empty factors

    public static FundamentalSnapshot LetterOnlyLatest() =>
        Snapshot("letter-only", D(2025, 11, 5), debtToEbitda: 3.0m);

    // ── IG/HY straddle (R1): Moody's BBB-/notch 10 (IG) + DBRS BB (high)/notch 11 (HY) + MSCI BBB/notch ──
    // ── 9 (IG). The set has both an IG and an HY provider ⇒ exactly one IG_HY_BOUNDARY (high). All fresh ──
    // ── (no STALE) and median-distance ≤ 1 (no OUTLIER), so IG_HY stands alone.                          ──
    public static Issuer IgHyStraddleIssuer() =>
        Issuer("meridian", "Meridian Freight Corp.", "MRDF", "0000045012");

    public static FundamentalSnapshot IgHyStraddleLatest() =>
        Snapshot("meridian", D(2025, 11, 5), "10-Q", debtToEbitda: 3.5m);

    public static IReadOnlyList<ProviderRating> IgHyStraddleRatings() => new[]
    {
        Rating(Provider.Moodys, "Baa3", inputAsOf: D(2025, 11, 10)),              // notch 10, IG floor
        Rating(Provider.MorningstarDbrs, "BB (high)", inputAsOf: D(2025, 11, 9)), // notch 11, HY
        Rating(Provider.Msci, "BBB", inputAsOf: D(2025, 11, 8)),                  // notch 9, IG
    };

    // ── Under review (R6): three fresh, consensus IG ratings, but MSCI is on CreditWatch ⇒ exactly one ──
    // ── PROVIDER_UNDER_REVIEW (low). No STALE / OUTLIER / IG_HY.                                         ──
    public static Issuer UnderReviewIssuer() =>
        Issuer("beacon", "Beacon Retail Group Inc.", "BCNR", "0000056023");

    public static FundamentalSnapshot UnderReviewLatest() =>
        Snapshot("beacon", D(2025, 11, 5), "10-Q", debtToEbitda: 2.8m);

    public static IReadOnlyList<ProviderRating> UnderReviewRatings() => new[]
    {
        Rating(Provider.Moodys, "A2", inputAsOf: D(2025, 11, 10)),                            // notch 6, fresh
        Rating(Provider.MorningstarDbrs, "A (mid)", inputAsOf: D(2025, 11, 11)),              // notch 6, fresh
        Rating(Provider.Msci, "A", inputAsOf: D(2025, 11, 12), outlook: RatingOutlook.Negative, underReview: true),
    };
}
