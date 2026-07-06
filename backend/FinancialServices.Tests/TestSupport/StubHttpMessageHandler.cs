using System.Net;
using System.Text;

namespace FinancialServices.Tests.TestSupport;

/// <summary>
/// Hand-rolled <see cref="HttpMessageHandler"/> returning queued canned responses — no live network,
/// no Moq (architecturalPlan/11 dependency-light). Responses are replayed in order; the last one is
/// reused for any over-calls (so retry tests can queue <c>[500, 500, 200]</c>). Records the request
/// count and last-seen URI for as-of boundary assertions.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly IReadOnlyList<HttpResponseMessage> _responses;
    private int _index;

    public StubHttpMessageHandler(params HttpResponseMessage[] responses)
    {
        if (responses is null || responses.Length == 0)
        {
            throw new ArgumentException("At least one canned response is required.", nameof(responses));
        }

        _responses = responses;
    }

    /// <summary>Convenience factory for a single JSON response.</summary>
    public static StubHttpMessageHandler Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(JsonResponse(json, status));

    /// <summary>Builds one JSON <see cref="HttpResponseMessage"/> — queue several for multi-call flows
    /// (e.g. EDGAR fetches companyfacts then submissions).</summary>
    public static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public int RequestCount { get; private set; }

    public Uri? LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequestCount++;
        LastRequestUri = request.RequestUri;
        var response = _responses[Math.Min(_index, _responses.Count - 1)];
        _index++;
        return Task.FromResult(response);
    }
}
