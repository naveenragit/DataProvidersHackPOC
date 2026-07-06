using System.Net;
using FinancialServices.Api.Connectors;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinancialServices.Tests.Connectors;

/// <summary>
/// EDGAR connector coverage — now <b>I/O only</b> (P2): raw concept-fact extraction, the issuer's
/// LatestFilingDate from the submissions API (as-of, includes 8-Ks), transport + upstream fail-loud,
/// the point-of-use SecUserAgent guard, and the future-asOf bound. Fundamentals math is tested
/// separately in <see cref="FinancialServices.Tests.Analysis.FundamentalsCalculatorTests"/>.
/// </summary>
public sealed class EdgarClientTests
{
    private const string CompanyFacts = """
    {
      "cik": 1234567,
      "entityName": "NordStar Corp",
      "facts": {
        "us-gaap": {
          "CashAndCashEquivalentsAtCarryingValue": {
            "units": { "USD": [
              { "end": "2025-12-31", "val": 100, "filed": "2026-03-01", "form": "10-Q", "accn": "a-1" },
              { "end": "2026-03-31", "val": 150, "filed": "2026-05-20", "form": "10-Q", "accn": "a-2" }
            ] }
          },
          "LongTermDebtNoncurrent": {
            "units": { "USD": [
              { "end": "2026-03-31", "val": 500, "filed": "2026-05-20", "form": "10-Q", "accn": "a-2" }
            ] }
          }
        }
      }
    }
    """;

    // Submissions feed: an 8-K (2026-06-15) filed AFTER the latest 10-Q (2026-05-20). LatestFilingDate
    // must track the 8-K even though it carries no XBRL concept facts (fix 4 / STK-02).
    private const string Submissions = """
    {
      "cik": "1234567",
      "filings": {
        "recent": {
          "filingDate": ["2026-06-15", "2026-05-20", "2026-03-01"],
          "form": ["8-K", "10-Q", "10-Q"]
        }
      }
    }
    """;

    private static EdgarClient CreateClient(
        string factsJson, string submissionsJson, string secUserAgent = "prism-test contact@example.com")
    {
        var handler = new StubHttpMessageHandler(
            StubHttpMessageHandler.JsonResponse(factsJson),
            StubHttpMessageHandler.JsonResponse(submissionsJson));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://data.sec.gov/") };
        var options = Options.Create(new PrismOptions { FredApiKey = "x", SecUserAgent = secUserAgent });
        return new EdgarClient(http, options, NullLogger<EdgarClient>.Instance);
    }

    private static readonly DateTimeOffset AsOfLate = new(2026, 6, 25, 0, 0, 0, TimeSpan.Zero);

    [Fact] // fix 4 — LatestFilingDate = the newest submission ≤ asOf across ALL forms (an 8-K here).
    public async Task GetFactsAsOf_DerivesLatestFilingFromSubmissions_IncludingEightK()
    {
        var client = CreateClient(CompanyFacts, Submissions);

        var facts = await client.GetFactsAsOfAsync("1234567", AsOfLate, CancellationToken.None);

        facts.LatestFilingDate.Should().Be(new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));
        facts.LatestFilingType.Should().Be("8-K");
        facts.UsGaap.Should().ContainKey("CashAndCashEquivalentsAtCarryingValue");
        facts.UsGaap["CashAndCashEquivalentsAtCarryingValue"].Should().HaveCount(2);
    }

    [Fact] // No hindsight: asOf before the 8-K excludes it — LatestFilingDate falls back to the 10-Q.
    public async Task GetFactsAsOf_BeforeLatestFiling_ExcludesFutureSubmission()
    {
        var client = CreateClient(CompanyFacts, Submissions);

        var facts = await client.GetFactsAsOfAsync(
            "1234567", new DateTimeOffset(2026, 5, 25, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

        facts.LatestFilingDate.Should().Be(new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero));
        facts.LatestFilingType.Should().Be("10-Q");
    }

    [Fact] // A4 — an upstream 500 on companyfacts fails loud as UpstreamServiceException("EDGAR").
    public async Task GetFactsAsOf_UpstreamError_ThrowsUpstreamServiceException()
    {
        var handler = StubHttpMessageHandler.Json("{}", HttpStatusCode.InternalServerError);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://data.sec.gov/") };
        var options = Options.Create(new PrismOptions { FredApiKey = "x", SecUserAgent = "prism-test" });
        var client = new EdgarClient(http, options, NullLogger<EdgarClient>.Instance);

        var act = async () => await client.GetFactsAsOfAsync("1234567", AsOfLate, CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<UpstreamServiceException>();
        assertion.Which.Service.Should().Be("EDGAR");
    }

    [Fact] // fix 1 — a transport fault (HttpRequestException) surfaces as UpstreamServiceException, not raw.
    public async Task GetFactsAsOf_TransportFault_ThrowsUpstreamServiceException()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("connection reset"));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://data.sec.gov/") };
        var options = Options.Create(new PrismOptions { FredApiKey = "x", SecUserAgent = "prism-test" });
        var client = new EdgarClient(http, options, NullLogger<EdgarClient>.Instance);

        var act = async () => await client.GetFactsAsOfAsync("1234567", AsOfLate, CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<UpstreamServiceException>();
        assertion.Which.Service.Should().Be("EDGAR");
    }

    [Fact] // fix 8 — a blank SEC User-Agent fails loud at first use (never a silent 403).
    public async Task GetFactsAsOf_BlankSecUserAgent_ThrowsConfigurationException()
    {
        var client = CreateClient(CompanyFacts, Submissions, secUserAgent: "");

        var act = async () => await client.GetFactsAsOfAsync("1234567", AsOfLate, CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<ConfigurationException>();
        assertion.Which.Setting.Should().Be("Prism:SecUserAgent");
    }

    [Fact] // fix 9b — a future asOf is rejected at the connector boundary (no hindsight, P3).
    public async Task GetFactsAsOf_FutureAsOf_ThrowsArgumentOutOfRange()
    {
        var client = CreateClient(CompanyFacts, Submissions);

        var act = async () => await client.GetFactsAsOfAsync(
            "1234567", DateTimeOffset.UtcNow.AddDays(1), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
