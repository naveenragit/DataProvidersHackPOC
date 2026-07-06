using System.Globalization;
using System.Text.Json;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Connectors;

/// <summary>
/// Real FRED observations connector. Calls <c>https://api.stlouisfed.org/</c> only (SSRF guard P6/D7),
/// requests <c>observation_end=asOf</c> and takes the latest observation on or before the decision
/// date (P3 — no hindsight), skips FRED's <c>"."</c> missing-value sentinel, and fails loud with
/// <see cref="UpstreamServiceException"/> rather than inventing a number (P1).
/// </summary>
public sealed class FredClient(HttpClient http, IOptions<PrismOptions> options, ILogger<FredClient> logger)
    : IFredClient
{
    private const string AllowedHost = "api.stlouisfed.org";
    private readonly PrismOptions _options = options.Value;

    public async Task<decimal> GetLatestOnOrBeforeAsync(string seriesId, DateTimeOffset asOf, CancellationToken ct)
    {
        ValidateSeriesId(seriesId);
        AsOf.EnsureNotFuture(asOf);
        var apiKey = RequireApiKey();
        EnsureAllowedHost();

        using var activity = ConnectorTelemetry.Source.StartActivity("Fred.GetLatestOnOrBefore");
        activity?.SetTag("seriesId", seriesId);
        activity?.SetTag("asOf", asOf);
        // Never tag the request URL — it carries api_key. Only safe ids are tagged (P6/07).

        logger.LogDebug("FRED observations fetch for {SeriesId} as of {AsOf}", seriesId, asOf);

        var relativeUri =
            $"fred/series/observations?series_id={seriesId}&api_key={apiKey}" +
            $"&file_type=json&observation_end={asOf.UtcDateTime:yyyy-MM-dd}&sort_order=desc";

        var payload = await SendAsync(relativeUri, seriesId, ct).ConfigureAwait(false);

        List<Observation> observations;
        try
        {
            observations = ParseObservations(payload);
        }
        catch (JsonException ex)
        {
            throw new UpstreamServiceException(
                "FRED", $"FRED returned an unparseable observations document for series {seriesId}.", ex);
        }

        var latest = AsOf.LatestOnOrBefore(observations, o => o.Date, asOf);
        if (latest is null)
        {
            throw new UpstreamServiceException(
                "FRED", $"No FRED observation on or before {asOf.UtcDateTime:yyyy-MM-dd} for series {seriesId}.");
        }

        activity?.SetTag("observationDate", latest.Date);
        return latest.Value;
    }

    // Single HTTP choke point: transport faults (TLS/DNS/reset) and timeouts fail loud as
    // UpstreamServiceException while the caller's real cancellation still propagates (03). The failure
    // names the seriesId — never the api_key-bearing URL (P6).
    private async Task<string> SendAsync(string relativeUri, string seriesId, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(relativeUri, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamServiceException(
                "FRED", $"FRED request for series {seriesId} failed before a response was received.", ex);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new UpstreamServiceException("FRED", $"FRED request for series {seriesId} timed out.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new UpstreamServiceException(
                    "FRED", $"FRED returned HTTP {(int)response.StatusCode} for series {seriesId}.");
            }

            try
            {
                return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new UpstreamServiceException(
                    "FRED", $"FRED response for series {seriesId} could not be read.", ex);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new UpstreamServiceException("FRED", $"FRED response for series {seriesId} timed out.", ex);
            }
        }
    }

    // Fail loud at first real use if the FRED key is unset — the option is no longer [Required] so
    // /api/health and synthetic-only runs boot, but a real FRED fetch must be honestly configured (fix 8).
    private string RequireApiKey()
    {
        var apiKey = _options.FredApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ConfigurationException(
                "Prism:FredApiKey", "FRED API key is not configured; set Prism:FredApiKey.");
        }

        return apiKey;
    }

    private static List<Observation> ParseObservations(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var result = new List<Observation>();
        if (!document.RootElement.TryGetProperty("observations", out var observations) ||
            observations.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in observations.EnumerateArray())
        {
            var valueText = item.TryGetProperty("value", out var valueElement) ? valueElement.GetString() : null;
            if (string.IsNullOrEmpty(valueText) || valueText == ".")
            {
                continue; // FRED uses "." for a missing observation — skip, never fabricate.
            }

            if (!item.TryGetProperty("date", out var dateElement) ||
                !DateTimeOffset.TryParse(
                    dateElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
            {
                continue;
            }

            if (!decimal.TryParse(valueText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            result.Add(new Observation(date, value));
        }

        return result;
    }

    // FRED series ids are untrusted tool args (P6/D8): allow only word characters before URL-building.
    private static void ValidateSeriesId(string seriesId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(seriesId);
        if (!seriesId.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            throw new ArgumentException($"Invalid FRED series id '{seriesId}'.", nameof(seriesId));
        }
    }

    private void EnsureAllowedHost()
    {
        var baseAddress = http.BaseAddress;
        if (baseAddress is null ||
            !string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(baseAddress.Host, AllowedHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"FredClient must target https://{AllowedHost}; configured base address is '{baseAddress}'.");
        }
    }

    private sealed record Observation(DateTimeOffset Date, decimal Value);
}
