using System.Globalization;
using System.Text.Json;
using FinancialServices.Api.Analysis;
using FinancialServices.Api.Infrastructure;
using FinancialServices.Api.Infrastructure.Errors;
using FinancialServices.Api.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinancialServices.Api.Connectors;

/// <summary>
/// Real SEC EDGAR connector — <b>I/O only</b> (P2): it fetches companyfacts + submissions from
/// <c>https://data.sec.gov/</c> (SSRF guard P6/D7), returns the raw us-gaap concept facts plus the
/// issuer's latest filing on or before the decision date (submissions API), and fails loud with
/// <see cref="UpstreamServiceException"/> on any upstream/transport fault (P1 — never fabricates).
/// All fundamentals derivation lives in the pure <see cref="FundamentalsCalculator"/>.
/// </summary>
public sealed class EdgarClient(HttpClient http, IOptions<PrismOptions> options, ILogger<EdgarClient> logger)
    : IEdgarClient
{
    private const string AllowedHost = "data.sec.gov";
    private readonly PrismOptions _options = options.Value;

    public async Task<EdgarCompanyFacts> GetFactsAsOfAsync(string cik, DateTimeOffset asOf, CancellationToken ct)
    {
        var normalizedCik = NormalizeCik(cik);
        AsOf.EnsureNotFuture(asOf);
        EnsureConfigured();
        EnsureAllowedHost();

        using var activity = ConnectorTelemetry.Source.StartActivity("Edgar.GetFactsAsOf");
        activity?.SetTag("cik", normalizedCik);
        activity?.SetTag("asOf", asOf);

        logger.LogDebug("EDGAR fetch for CIK {Cik} as of {AsOf}", normalizedCik, asOf);

        var factsPayload = await SendAsync($"api/xbrl/companyfacts/CIK{normalizedCik}.json", ct).ConfigureAwait(false);
        var usGaap = ParseConceptFacts(factsPayload, normalizedCik);

        var submissionsPayload = await SendAsync($"submissions/CIK{normalizedCik}.json", ct).ConfigureAwait(false);
        var (latestFilingDate, latestFilingType) = ParseLatestFiling(submissionsPayload, normalizedCik, asOf);

        activity?.SetTag("latestFilingDate", latestFilingDate);
        activity?.SetTag("latestFilingType", latestFilingType);

        return new EdgarCompanyFacts(normalizedCik, latestFilingDate, latestFilingType, usGaap);
    }

    // Single choke point for the HTTP send so transport faults (TLS/DNS/reset) and timeouts fail loud
    // as UpstreamServiceException, while the caller's real cancellation still propagates (03).
    private async Task<string> SendAsync(string relativeUri, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(relativeUri, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new UpstreamServiceException("EDGAR", "EDGAR request failed before a response was received.", ex);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // HttpClient.Timeout elapsed (not the caller's cancellation) → a timeout, surfaced loud.
            throw new UpstreamServiceException("EDGAR", "EDGAR request timed out.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new UpstreamServiceException("EDGAR", $"EDGAR returned HTTP {(int)response.StatusCode}.");
            }

            try
            {
                return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new UpstreamServiceException("EDGAR", "EDGAR response body could not be read.", ex);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new UpstreamServiceException("EDGAR", "EDGAR response read timed out.", ex);
            }
        }
    }

    // Raw extraction only (P2): pull the FundamentalsCalculator's concept vocabulary out of companyfacts
    // as XbrlFacts — no as-of filtering, no derivation (the pure engine owns those).
    private static IReadOnlyDictionary<string, IReadOnlyList<XbrlFact>> ParseConceptFacts(string payload, string cik)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("facts", out var facts) ||
                !facts.TryGetProperty("us-gaap", out var usGaap))
            {
                throw new UpstreamServiceException(
                    "EDGAR", $"EDGAR companyfacts for CIK {cik} contained no us-gaap facts.");
            }

            var result = new Dictionary<string, IReadOnlyList<XbrlFact>>(StringComparer.Ordinal);
            foreach (var concept in FundamentalsCalculator.RelevantConcepts)
            {
                if (!usGaap.TryGetProperty(concept, out var conceptElement) ||
                    !conceptElement.TryGetProperty("units", out var units) ||
                    !units.TryGetProperty("USD", out var usd) ||
                    usd.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var list = new List<XbrlFact>();
                foreach (var item in usd.EnumerateArray())
                {
                    var fact = ReadFact(item);
                    if (fact is not null)
                    {
                        list.Add(fact);
                    }
                }

                if (list.Count > 0)
                {
                    result[concept] = list;
                }
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new UpstreamServiceException(
                "EDGAR", $"EDGAR returned an unparseable companyfacts document for CIK {cik}.", ex);
        }
    }

    private static XbrlFact? ReadFact(JsonElement item)
    {
        if (!item.TryGetProperty("end", out var endElement) || !TryParseDate(endElement, out var end) ||
            !item.TryGetProperty("filed", out var filedElement) || !TryParseDate(filedElement, out var filed))
        {
            return null;
        }

        DateTimeOffset? start = item.TryGetProperty("start", out var startElement) && TryParseDate(startElement, out var s)
            ? s
            : null;
        var frame = item.TryGetProperty("frame", out var frameElement) ? frameElement.GetString() : null;
        var accn = item.TryGetProperty("accn", out var accnElement) ? accnElement.GetString() : null;
        var form = item.TryGetProperty("form", out var formElement) ? formElement.GetString() : null;

        // TryGetDecimal (never GetDecimal): a value outside decimal range is skipped, not thrown (9c).
        decimal? value = item.TryGetProperty("val", out var valueElement)
            && valueElement.ValueKind == JsonValueKind.Number
            && valueElement.TryGetDecimal(out var parsed)
            ? parsed
            : null;

        return new XbrlFact(value, start, end, filed, frame, accn, form);
    }

    // LatestFilingDate/Type = the issuer's newest filing on or before asOf across ALL filings in the
    // submissions feed (includes 8-Ks etc.) — the stale-input rule's ground truth (fix 4 / P3).
    private static (DateTimeOffset? Date, string? Form) ParseLatestFiling(string payload, string cik, DateTimeOffset asOf)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("filings", out var filings) ||
                !filings.TryGetProperty("recent", out var recent) ||
                !recent.TryGetProperty("filingDate", out var dates) || dates.ValueKind != JsonValueKind.Array ||
                !recent.TryGetProperty("form", out var forms) || forms.ValueKind != JsonValueKind.Array)
            {
                return (null, null);
            }

            var dateArray = dates.EnumerateArray().ToArray();
            var formArray = forms.EnumerateArray().ToArray();

            DateTimeOffset? best = null;
            string? bestForm = null;
            for (var i = 0; i < dateArray.Length; i++)
            {
                if (!TryParseDate(dateArray[i], out var filingDate) || filingDate > asOf)
                {
                    continue;
                }

                if (best is null || filingDate > best)
                {
                    best = filingDate;
                    bestForm = i < formArray.Length ? formArray[i].GetString() : null;
                }
            }

            return (best, bestForm);
        }
        catch (JsonException ex)
        {
            throw new UpstreamServiceException(
                "EDGAR", $"EDGAR returned an unparseable submissions document for CIK {cik}.", ex);
        }
    }

    private static bool TryParseDate(JsonElement element, out DateTimeOffset value)
    {
        value = default;
        var text = element.GetString();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    // Tool/agent-supplied CIK is untrusted (P6/D8): strip to digits, reject empty/oversized, zero-pad.
    private static string NormalizeCik(string cik)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cik);
        var digits = new string(cik.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length is 0 or > 10)
        {
            throw new ArgumentException($"Invalid CIK '{cik}'.", nameof(cik));
        }

        return digits.PadLeft(10, '0');
    }

    // Fail loud at first real use if the SEC User-Agent is unset (SEC 403s without one) — the option is
    // no longer [Required] so /api/health boots, but a real fetch must be honestly configured (fix 8).
    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.SecUserAgent))
        {
            throw new ConfigurationException(
                "Prism:SecUserAgent", "SEC User-Agent is not configured; set Prism:SecUserAgent (SEC requires it).");
        }
    }

    // SSRF allowlist (P6/D7): fixed host AND https scheme — reject anything else the DI wiring did not set.
    private void EnsureAllowedHost()
    {
        var baseAddress = http.BaseAddress;
        if (baseAddress is null ||
            !string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(baseAddress.Host, AllowedHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"EdgarClient must target https://{AllowedHost}; configured base address is '{baseAddress}'.");
        }
    }
}
