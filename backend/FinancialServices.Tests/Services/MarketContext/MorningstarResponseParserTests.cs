using FinancialServices.Api.Services.MarketContext;
using FluentAssertions;
using Xunit;

namespace FinancialServices.Tests.Services.MarketContext;

/// <summary>
/// Proves <see cref="MorningstarResponseParser"/> against the <b>real captured</b> Morningstar tool
/// shapes (grounded from a live <c>tools/call</c>, P1 — no invented fields): id-lookup →
/// <c>{"investments":{"AAPL":[{…}]}}</c> and analyst-research → <c>{"results":[{…}]}</c>. Also covers the
/// fail-soft paths (blank/malformed/empty) that keep a provider-side change from 5xxing the panel.
/// </summary>
public sealed class MorningstarResponseParserTests
{
    private const string IdLookupAapl =
        """
        {"investments":{"AAPL":[{"morningstar_id":"0P000000GY","investment_name":"Apple Inc","ticker_symbol":"AAPL","investment_type":"ST","message":null,"exchange":"Nasdaq - All Markets"}]},"datapoints":{}}
        """;

    private const string AnalystResearchAapl =
        """
        {"results":[{"content":"Apple has a wide economic moat driven by switching costs and intangible assets.","published_at":"2026-06-18T20:11:00Z","title":"Apple Remains The Preeminent Device Vendor","security_names":["Apple Inc"],"url":"https://mcp.morningstar.com/redirect?url=report/1485466"}]}
        """;

    [Fact]
    public void ParseInvestment_maps_the_first_matched_security()
    {
        var investment = MorningstarResponseParser.ParseInvestment(IdLookupAapl);

        investment.Should().NotBeNull();
        investment!.MorningstarId.Should().Be("0P000000GY");
        investment.Name.Should().Be("Apple Inc");
        investment.Ticker.Should().Be("AAPL");
        investment.InvestmentType.Should().Be("ST");
        investment.Exchange.Should().Be("Nasdaq - All Markets");
    }

    [Fact]
    public void ParseInvestment_returns_null_when_investments_is_empty()
    {
        MorningstarResponseParser.ParseInvestment("""{"investments":{},"datapoints":{}}""").Should().BeNull();
    }

    [Fact]
    public void ParseInvestment_returns_null_when_the_identifier_matched_nothing()
    {
        MorningstarResponseParser.ParseInvestment("""{"investments":{"NORDSTAR":[]},"datapoints":{}}""").Should().BeNull();
    }

    [Fact]
    public void ParseInvestment_returns_null_when_no_morningstar_id_is_present()
    {
        MorningstarResponseParser.ParseInvestment("""{"investments":{"X":[{"investment_name":"X"}]}}""").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{}")]
    public void ParseInvestment_returns_null_for_blank_or_malformed_input(string? json)
    {
        MorningstarResponseParser.ParseInvestment(json).Should().BeNull();
    }

    [Fact]
    public void ParseSections_maps_title_date_excerpt_and_url()
    {
        var sections = MorningstarResponseParser.ParseSections(AnalystResearchAapl, maxSections: 4, maxExcerptChars: 900);

        sections.Should().HaveCount(1);
        var section = sections[0];
        section.Title.Should().Be("Apple Remains The Preeminent Device Vendor");
        section.PublishedAt.Should().Be(new DateTimeOffset(2026, 6, 18, 20, 11, 0, TimeSpan.Zero));
        section.Excerpt.Should().StartWith("Apple has a wide economic moat");
        section.Url.Should().Be("https://mcp.morningstar.com/redirect?url=report/1485466");
    }

    [Fact]
    public void ParseSections_truncates_long_content_on_a_word_boundary_with_an_ellipsis()
    {
        var longContent = string.Join(" ", Enumerable.Repeat("moat", 500)); // ~2499 chars
        var json = $$"""{"results":[{"content":"{{longContent}}","title":"T"}]}""";

        var sections = MorningstarResponseParser.ParseSections(json, maxSections: 4, maxExcerptChars: 900);

        sections.Should().HaveCount(1);
        sections[0].Excerpt.Length.Should().BeLessThanOrEqualTo(901);
        sections[0].Excerpt.Should().EndWith("…");
    }

    [Fact]
    public void ParseSections_caps_the_number_of_sections()
    {
        var results = string.Join(",", Enumerable.Range(0, 6).Select(i => $$"""{"content":"c{{i}}","title":"t{{i}}"}"""));
        var json = $$"""{"results":[{{results}}]}""";

        MorningstarResponseParser.ParseSections(json, maxSections: 4, maxExcerptChars: 900).Should().HaveCount(4);
    }

    [Fact]
    public void ParseSections_skips_results_with_no_content_and_defaults_a_missing_title()
    {
        var json = """{"results":[{"title":"no content here"},{"content":"kept","published_at":"bad-date"}]}""";

        var sections = MorningstarResponseParser.ParseSections(json, maxSections: 4, maxExcerptChars: 900);

        sections.Should().HaveCount(1);
        sections[0].Excerpt.Should().Be("kept");
        sections[0].Title.Should().Be("Morningstar analyst research");
        sections[0].PublishedAt.Should().BeNull();
    }

    [Fact]
    public void ParseSections_decodes_literal_unicode_escapes_in_content()
    {
        // Morningstar double-escapes punctuation: the content arrives as the literal 6 chars "\u2019".
        var json = """{"results":[{"content":"Apple\\u2019s wide\\u2013moat ecosystem","title":"T"}]}""";

        var sections = MorningstarResponseParser.ParseSections(json, maxSections: 4, maxExcerptChars: 900);

        sections.Should().HaveCount(1);
        sections[0].Excerpt.Should().Be("Apple\u2019s wide\u2013moat ecosystem");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"results":"nope"}""")]
    public void ParseSections_returns_empty_for_blank_or_malformed_input(string? json)
    {
        MorningstarResponseParser.ParseSections(json, maxSections: 4, maxExcerptChars: 900).Should().BeEmpty();
    }
}
