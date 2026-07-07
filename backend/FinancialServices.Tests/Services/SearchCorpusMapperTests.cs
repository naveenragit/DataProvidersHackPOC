using FinancialServices.Api.Analysis;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Models;
using FinancialServices.Api.Services;
using FluentAssertions;
using Xunit;

namespace FinancialServices.Tests.Services;

/// <summary>
/// The corpus → domain mapping, especially the money-moment field choice: a ratingCard's
/// <c>asOfDate</c> (the rating-action date) becomes <see cref="ProviderRating.InputAsOfDate"/> — NOT
/// the card's <c>inputAsOfDate</c> (financials period-end, which precedes every filing and would
/// false-flag all providers).
/// </summary>
public sealed class SearchCorpusMapperTests
{
    private static DateTimeOffset D(int y, int m, int d) => new(y, m, d, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToProviderRating_maps_card_asOfDate_to_InputAsOfDate_not_the_financials_vintage()
    {
        // NordStar MSCI card: rating action 2025-09-15, financials period-end 2025-08-20.
        var card = new SearchCorpusRow
        {
            Id = "nordstar-msci",
            DocType = "ratingCard",
            IssuerId = "nordstar",
            Provider = "Msci",
            Letter = "BBB-",
            Notch = 10,
            AsOfDate = D(2025, 9, 15),
            InputAsOfDate = D(2025, 8, 20),
        };

        ProviderRating rating = SearchCorpusMapper.ToProviderRating(card);

        rating.Provider.Should().Be(Provider.Msci);
        rating.InputAsOfDate.Should().Be(D(2025, 9, 15));   // the rating-action date drives STALE
        rating.InputAsOfDate.Should().NotBe(D(2025, 8, 20)); // never the financials period-end
        rating.AsOfDate.Should().Be(D(2025, 9, 15));
        rating.MethodologyDocId.Should().Be("nordstar-msci");
        rating.Factors.Should().BeEmpty();
    }

    [Fact]
    public void ToProviderRating_recomputes_the_notch_from_the_letter_via_the_ladder()
    {
        var card = new SearchCorpusRow { Provider = "Moodys", Letter = "Baa2", Notch = 999 /* ignored */ };

        ProviderRating rating = SearchCorpusMapper.ToProviderRating(card);

        rating.Notch.Should().Be(NotchLadder.ToNotch("Baa2"));
        rating.Notch.Should().NotBe(999);
    }

    [Fact]
    public void MapCards_applies_the_as_of_gate_and_orders_by_provider()
    {
        var cards = new[]
        {
            new SearchCorpusRow { Provider = "Msci", Letter = "BBB-", AsOfDate = D(2025, 9, 15) },
            new SearchCorpusRow { Provider = "Moodys", Letter = "Baa2", AsOfDate = D(2025, 11, 12) },   // after asOf
            new SearchCorpusRow { Provider = "MorningstarDbrs", Letter = "BBB (mid)", AsOfDate = D(2025, 10, 1) },
        };

        IReadOnlyList<ProviderRating> mapped = SearchCorpusMapper.MapCards(cards, asOf: D(2025, 10, 15));

        // Moody's action (2025-11-12) is after the as-of and must be excluded (no hindsight, P3).
        mapped.Select(r => r.Provider).Should().Equal(Provider.MorningstarDbrs, Provider.Msci);
    }

    [Fact]
    public void ToIssuerEntry_maps_metadata_boundary_and_coverage()
    {
        var row = new SearchCorpusRow
        {
            DocType = "issuer",
            IssuerId = "nordstar",
            LegalName = "NordStar Industrials Inc.",
            Ticker = "NRDS",
            Cik = "0000000001",
            Sector = "Industrials",
            SampleBondIsin = "US0000NRDS11",
            LatestFilingDate = D(2025, 11, 5),
            FilingType = "10-Q",
            AsOfDate = D(2025, 11, 5),
        };

        IssuerCorpusEntry entry = SearchCorpusMapper.ToIssuerEntry(row, [Provider.Moodys, Provider.Msci]);

        entry.Issuer.LegalName.Should().Be("NordStar Industrials Inc.");
        entry.Issuer.Cik.Should().Be("0000000001");
        entry.LatestFilingDate.Should().Be(D(2025, 11, 5));
        entry.FilingType.Should().Be("10-Q");
        entry.Coverage.Should().Equal(Provider.Moodys, Provider.Msci);
    }

    [Fact]
    public void ToIssuerEntry_falls_back_to_asOfDate_when_latestFilingDate_is_absent()
    {
        var row = new SearchCorpusRow { IssuerId = "x", LatestFilingDate = null, AsOfDate = D(2025, 10, 28) };

        IssuerCorpusEntry entry = SearchCorpusMapper.ToIssuerEntry(row, []);

        entry.LatestFilingDate.Should().Be(D(2025, 10, 28));
        entry.FilingType.Should().Be("filing");
    }

    [Theory]
    [InlineData("Moodys", Provider.Moodys)]
    [InlineData("morningstardbrs", Provider.MorningstarDbrs)]
    [InlineData("MSCI", Provider.Msci)]
    public void ParseProvider_is_case_insensitive(string value, Provider expected) =>
        SearchCorpusMapper.ParseProvider(value).Should().Be(expected);

    [Fact]
    public void ParseProvider_fails_loud_on_an_unknown_provider()
    {
        Action act = () => SearchCorpusMapper.ParseProvider("Fitch");
        act.Should().Throw<UpstreamServiceException>().Which.Service.Should().Be("Search");
    }
}
