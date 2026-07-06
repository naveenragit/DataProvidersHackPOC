using FinancialServices.Api.Analysis;
using FinancialServices.Api.Connectors;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Models;
using FinancialServices.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinancialServices.Tests.Connectors;

/// <summary>
/// Provider-ratings abstraction coverage: the labeled-synthetic MSCI source resolves a known dev
/// issuer, hides an as-of-future action, and returns null for unknowns; the pending Moody's and
/// Morningstar/DBRS clients degrade to null + a warning (never throw — ARC-03); and the record→domain
/// mapper carries every field (fix 10).
/// </summary>
public sealed class ProviderRatingsSourceTests
{
    private static readonly DateTimeOffset AsOfDate = new(2026, 6, 25, 0, 0, 0, TimeSpan.Zero);

    [Fact] // A8 — known dev issuer → a non-null record on the MSCI slot with the mapped notch.
    public async Task Synthetic_KnownIssuer_ReturnsRecord()
    {
        var source = new SyntheticRatingsSource();

        var record = await source.GetRatingAsOfAsync("nordstar", AsOfDate, CancellationToken.None);

        record.Should().NotBeNull();
        record!.Provider.Should().Be(Provider.Msci);
        record.Notch.Should().Be(NotchLadder.ToNotch("BBB-"));
        record.SourceRef.Should().StartWith("synthetic:");
    }

    [Fact] // A8 — unknown issuer → null (→ MISSING_COVERAGE; never a fabricated rating).
    public async Task Synthetic_UnknownIssuer_ReturnsNull()
    {
        var source = new SyntheticRatingsSource();

        var record = await source.GetRatingAsOfAsync("unknown-issuer", AsOfDate, CancellationToken.None);

        record.Should().BeNull();
    }

    [Fact] // P3 — a rating action AFTER asOf is hidden (the money-moment as-of guard).
    public async Task Synthetic_ActionAfterAsOf_ReturnsNull()
    {
        var source = new SyntheticRatingsSource();

        // The nordstar seed's rating action is 2026-01-15; an earlier decision date must not see it.
        var record = await source.GetRatingAsOfAsync(
            "nordstar", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

        record.Should().BeNull();
    }

    [Fact] // fix 6 — Moody's is pending → null (MISSING_COVERAGE) + a warning, never a throw.
    public async Task Moodys_PendingSpec_ReturnsNull_AndWarns()
    {
        var logger = new CapturingLogger<MoodysRatingsClient>();
        var client = new MoodysRatingsClient(Options(), logger);

        var record = await client.GetRatingAsOfAsync("nordstar", AsOfDate, CancellationToken.None);

        record.Should().BeNull();
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("pending"));
    }

    [Fact] // fix 6 — Morningstar/DBRS is pending → null + a warning, never a throw.
    public async Task MorningstarDbrs_PendingSpec_ReturnsNull_AndWarns()
    {
        var logger = new CapturingLogger<MorningstarDbrsRatingsClient>();
        var client = new MorningstarDbrsRatingsClient(Options(), logger);

        var record = await client.GetRatingAsOfAsync("nordstar", AsOfDate, CancellationToken.None);

        record.Should().BeNull();
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains("pending"));
    }

    [Fact] // fix 10 — the record→domain mapper carries every field (no silent drop).
    public void ToProviderRating_MapsAllFields()
    {
        var factors = new RatingFactor[] { new("Leverage", 0.5m, 60m, "ref#1") };
        var record = new ProviderRatingRecord(
            Provider: Provider.Moodys,
            IssuerId: "nordstar",
            Letter: "Baa3",
            Notch: 10,
            AsOfDate: new DateTimeOffset(2026, 6, 25, 0, 0, 0, TimeSpan.Zero),
            RatingActionDate: new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            Factors: factors,
            SourceRef: "moodys:doc#42");

        var rating = record.ToProviderRating();

        rating.Provider.Should().Be(Provider.Moodys);
        rating.Letter.Should().Be("Baa3");
        rating.Notch.Should().Be(10);
        rating.AsOfDate.Should().Be(record.AsOfDate);
        rating.InputAsOfDate.Should().Be(record.RatingActionDate); // stale-flag driver
        rating.Factors.Should().BeSameAs(factors);
        rating.MethodologyDocId.Should().Be("moodys:doc#42");       // SourceRef carried through
    }

    private static IOptions<PrismOptions> Options() =>
        Microsoft.Extensions.Options.Options.Create(
            new PrismOptions { FredApiKey = "x", SecUserAgent = "y" });
}
