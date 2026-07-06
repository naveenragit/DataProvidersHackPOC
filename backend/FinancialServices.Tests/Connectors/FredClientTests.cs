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
/// FRED latest-on-or-before + <c>"."</c>-skip + failure coverage (package 04 acceptance A3/A4) plus the
/// corrections: transport-fault fail-loud and the point-of-use api-key guard. All dates stay on or
/// before "now" so the no-hindsight bound (fix 9b) never trips the fixtures.
/// </summary>
public sealed class FredClientTests
{
    private const string Observations = """
    {
      "observations": [
        { "date": "2026-03-31", "value": "1.23" },
        { "date": "2026-06-30", "value": "1.45" },
        { "date": "2026-07-01", "value": "." }
      ]
    }
    """;

    private static FredClient CreateClient(HttpMessageHandler handler, string? apiKey = "dummy-key")
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.stlouisfed.org/") };
        var options = Options.Create(new PrismOptions { FredApiKey = apiKey, SecUserAgent = "prism-test" });
        return new FredClient(http, options, NullLogger<FredClient>.Instance);
    }

    private static FredClient JsonClient(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        CreateClient(StubHttpMessageHandler.Json(json, status));

    [Fact] // A3 — asOf before the June point returns the March observation (dated ≤ as-of).
    public async Task GetLatestOnOrBefore_ReturnsObservationOnOrBeforeAsOf()
    {
        var client = JsonClient(Observations);

        var value = await client.GetLatestOnOrBeforeAsync(
            "BAMLC0A0CM", new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

        value.Should().Be(1.23m);
    }

    [Fact] // A3 — asOf past the "." sentinel skips it and returns the latest real value (June).
    public async Task GetLatestOnOrBefore_SkipsMissingValueSentinel()
    {
        var client = JsonClient(Observations);

        var value = await client.GetLatestOnOrBeforeAsync(
            "BAMLC0A0CM", new DateTimeOffset(2026, 7, 5, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

        value.Should().Be(1.45m);
    }

    [Fact] // A4 — an upstream 500 fails loud as UpstreamServiceException("FRED").
    public async Task GetLatestOnOrBefore_UpstreamError_ThrowsUpstreamServiceException()
    {
        var client = JsonClient("{}", HttpStatusCode.InternalServerError);

        var act = async () => await client.GetLatestOnOrBeforeAsync(
            "BAMLC0A0CM", new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<UpstreamServiceException>();
        assertion.Which.Service.Should().Be("FRED");
    }

    [Fact] // fix 1 — a transport fault (HttpRequestException) surfaces as UpstreamServiceException, not raw.
    public async Task GetLatestOnOrBefore_TransportFault_ThrowsUpstreamServiceException()
    {
        var client = CreateClient(new ThrowingHttpMessageHandler(new HttpRequestException("connection reset")));

        var act = async () => await client.GetLatestOnOrBeforeAsync(
            "BAMLC0A0CM", new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<UpstreamServiceException>();
        assertion.Which.Service.Should().Be("FRED");
    }

    [Fact] // fix 8 — a missing FRED key fails loud at first use (never a silent/fake fetch).
    public async Task GetLatestOnOrBefore_MissingApiKey_ThrowsConfigurationException()
    {
        var client = CreateClient(StubHttpMessageHandler.Json(Observations), apiKey: null);

        var act = async () => await client.GetLatestOnOrBeforeAsync(
            "BAMLC0A0CM", new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

        var assertion = await act.Should().ThrowAsync<ConfigurationException>();
        assertion.Which.Setting.Should().Be("Prism:FredApiKey");
    }
}
