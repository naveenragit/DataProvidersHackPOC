using System.Net;

namespace FinancialServices.Api.Infrastructure.Http;

/// <summary>
/// Bounded, jittered retry for transient upstream faults so Prism stays polite to SEC/FRED rate
/// limits (architecturalPlan/03 "backoff with jitter, bounded"; no Polly — dependency-light).
/// <b>Only idempotent GETs are retried</b> (Prism's real upstreams are all GET; a future POST body must
/// not be silently resent). Retries on <see cref="HttpRequestException"/> and 5xx/408/429 responses;
/// exponential base delay with <b>full jitter</b>, floored by any <c>Retry-After</c> the server sends;
/// honours the <see cref="CancellationToken"/> and never retries a cancel. The base delay is
/// ctor-injected so unit tests pass <see cref="TimeSpan.Zero"/> for determinism.
/// </summary>
public sealed class TransientRetryHandler : DelegatingHandler
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;

    public TransientRetryHandler(int maxAttempts = 3, TimeSpan? baseDelay = null)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "maxAttempts must be >= 1.");
        }

        _maxAttempts = maxAttempts;
        _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(200);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Retry only idempotent GETs — never resend a non-idempotent request (e.g. a future POST body).
        var canRetry = request.Method == HttpMethod.Get;

        for (var attempt = 1; ; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                // Non-retryable request, final attempt, or a non-transient status: hand it back verbatim.
                if (!canRetry || attempt >= _maxAttempts || !IsTransient(response.StatusCode))
                {
                    return response;
                }
            }
            catch (HttpRequestException) when (canRetry && attempt < _maxAttempts)
            {
                // Transient network fault with attempts remaining → back off and retry below.
                // OperationCanceledException is intentionally NOT caught, so cancellation propagates.
            }

            var delay = NextDelay(attempt, response);
            response?.Dispose();
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        (int)status >= 500
        || status == HttpStatusCode.RequestTimeout   // 408
        || status == HttpStatusCode.TooManyRequests;  // 429

    // Full jitter: delay ∈ [0, baseDelay × 2^(attempt-1)], floored by any server Retry-After hint.
    private TimeSpan NextDelay(int attempt, HttpResponseMessage? response)
    {
        var ceilingTicks = _baseDelay.Ticks * (1L << (attempt - 1));
        var jitter = TimeSpan.FromTicks((long)(Random.Shared.NextDouble() * ceilingTicks));

        var retryAfter = RetryAfterDelay(response);
        return retryAfter is { } floor && floor > jitter ? floor : jitter;
    }

    // Honour Retry-After (429/503): a delta is used directly; an HTTP-date is converted to a delay.
    private static TimeSpan? RetryAfterDelay(HttpResponseMessage? response)
    {
        var retryAfter = response?.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }

        if (retryAfter.Date is { } date)
        {
            var untilDate = date - DateTimeOffset.UtcNow;
            return untilDate > TimeSpan.Zero ? untilDate : TimeSpan.Zero;
        }

        return null;
    }
}
