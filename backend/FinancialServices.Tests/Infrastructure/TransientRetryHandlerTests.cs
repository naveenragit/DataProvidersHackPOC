using System.Net;
using System.Net.Http.Headers;
using FinancialServices.Api.Infrastructure.Http;
using FinancialServices.Tests.TestSupport;
using FluentAssertions;
using Xunit;

namespace FinancialServices.Tests.Infrastructure;

/// <summary>
/// Retry-with-jitter behaviour (D6 + fix 7). Uses <c>baseDelay: TimeSpan.Zero</c> for deterministic,
/// fast runs: transient 5xx GETs are retried up to the bound, client 4xx are not, non-GET requests are
/// never retried, and a <c>Retry-After</c> hint is honoured without breaking the retry.
/// </summary>
public sealed class TransientRetryHandlerTests
{
    [Fact]
    public async Task Retries_TransientServerErrors_ThenSucceeds()
    {
        var stub = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK));
        using var handler = new TransientRetryHandler(maxAttempts: 3, baseDelay: TimeSpan.Zero) { InnerHandler = stub };
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("https://data.sec.gov/x", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stub.RequestCount.Should().Be(3);
    }

    [Fact]
    public async Task DoesNotRetry_ClientError()
    {
        var stub = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.BadRequest));
        using var handler = new TransientRetryHandler(maxAttempts: 3, baseDelay: TimeSpan.Zero) { InnerHandler = stub };
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("https://data.sec.gov/x", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        stub.RequestCount.Should().Be(1);
    }

    [Fact] // fix 7 — a non-idempotent POST is never retried (a future request body must not be resent).
    public async Task DoesNotRetry_NonGetRequest()
    {
        var stub = new StubHttpMessageHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            new HttpResponseMessage(HttpStatusCode.OK));
        using var handler = new TransientRetryHandler(maxAttempts: 3, baseDelay: TimeSpan.Zero) { InnerHandler = stub };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://data.sec.gov/x");

        var response = await client.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        stub.RequestCount.Should().Be(1);
    }

    [Fact] // fix 7 — a 429 carrying Retry-After is honoured (as a delay floor) and the GET still retries.
    public async Task Honours_RetryAfter_AndRetriesGet()
    {
        var throttled = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        throttled.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero); // zero floor keeps the test instant
        var stub = new StubHttpMessageHandler(throttled, new HttpResponseMessage(HttpStatusCode.OK));
        using var handler = new TransientRetryHandler(maxAttempts: 3, baseDelay: TimeSpan.Zero) { InnerHandler = stub };
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("https://data.sec.gov/x", CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stub.RequestCount.Should().Be(2);
    }
}
