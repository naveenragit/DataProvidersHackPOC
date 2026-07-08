using System.Text.Json;
using FinancialServices.Api.Models;

namespace FinancialServices.Api.Services.MarketContext;

/// <summary>
/// Pure, defensive parsers for the two Morningstar tool responses Prism consumes. Grounded in the real
/// captured shapes (P1 — no invented fields):
/// <list type="bullet">
/// <item><c>morningstar-id-lookup-tool</c> → <c>{"investments":{"AAPL":[{"morningstar_id":"0P000000GY",
/// "investment_name":"Apple Inc","ticker_symbol":"AAPL","investment_type":"ST","exchange":"…"}]}}</c></item>
/// <item><c>morningstar-analyst-research-tool</c> → <c>{"results":[{"content":"…","published_at":"…Z",
/// "title":"…","security_names":["…"],"url":"…"}]}</c></item>
/// </list>
/// Everything is best-effort: a missing/renamed field degrades that field to <see langword="null"/>
/// rather than throwing, so a provider-side shape change never 5xxes the panel.
/// </summary>
public static partial class MorningstarResponseParser
{
    /// <summary>Parses the id-lookup structured JSON into the first matched security, or null if none matched.</summary>
    public static MarketContextInvestment? ParseInvestment(string? structuredJson)
    {
        if (string.IsNullOrWhiteSpace(structuredJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(structuredJson);
            if (!document.RootElement.TryGetProperty("investments", out var investments) ||
                investments.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // Keyed by the identifier we passed; there is exactly one, so take the first non-empty match.
            foreach (var identifierGroup in investments.EnumerateObject())
            {
                if (identifierGroup.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var match in identifierGroup.Value.EnumerateArray())
                {
                    var morningstarId = GetString(match, "morningstar_id");
                    if (string.IsNullOrWhiteSpace(morningstarId))
                    {
                        continue;
                    }

                    return new MarketContextInvestment(
                        morningstarId,
                        GetString(match, "investment_name") ?? identifierGroup.Name,
                        GetString(match, "ticker_symbol"),
                        GetString(match, "investment_type"),
                        GetString(match, "exchange"));
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the analyst-research structured JSON into at most <paramref name="maxSections"/> attributed
    /// sections, each excerpt trimmed to <paramref name="maxExcerptChars"/> (keeps the panel light and
    /// limits licensed-data surface). Sections with no usable content are skipped.
    /// </summary>
    public static IReadOnlyList<MarketContextSection> ParseSections(
        string? structuredJson,
        int maxSections,
        int maxExcerptChars)
    {
        if (string.IsNullOrWhiteSpace(structuredJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(structuredJson);
            if (!document.RootElement.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var sections = new List<MarketContextSection>();
            foreach (var result in results.EnumerateArray())
            {
                if (sections.Count >= maxSections)
                {
                    break;
                }

                var content = GetString(result, "content");
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                sections.Add(new MarketContextSection(
                    GetString(result, "title") ?? "Morningstar analyst research",
                    ParseTimestamp(result, "published_at"),
                    Truncate(NormalizeText(content!), maxExcerptChars),
                    GetString(result, "url")));
            }

            return sections;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? GetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset? ParseTimestamp(JsonElement element, string property) =>
        GetString(element, property) is { } raw && DateTimeOffset.TryParse(raw, out var parsed)
            ? parsed
            : null;

    // Morningstar's content is double-escaped at the source: apostrophes/quotes/dashes arrive as the
    // LITERAL 6-char sequence "\u2019" rather than the character. Decode those for display only (this is
    // presentational normalization of a third party's narrative, not a change to any Prism datum).
    internal static string NormalizeText(string text) =>
        LiteralUnicodeEscape().Replace(
            text,
            match => ((char)int.Parse(match.Groups[1].ValueSpan, System.Globalization.NumberStyles.HexNumber)).ToString());

    [System.Text.RegularExpressions.GeneratedRegex(@"\\u([0-9a-fA-F]{4})")]
    private static partial System.Text.RegularExpressions.Regex LiteralUnicodeEscape();

    /// <summary>Trims to <paramref name="maxChars"/> on a word boundary and appends an ellipsis when cut.</summary>
    internal static string Truncate(string text, int maxChars)
    {
        var normalized = text.Trim();
        if (normalized.Length <= maxChars)
        {
            return normalized;
        }

        var slice = normalized[..maxChars];
        var lastSpace = slice.LastIndexOf(' ');
        if (lastSpace > maxChars / 2)
        {
            slice = slice[..lastSpace];
        }

        return slice.TrimEnd() + "…";
    }
}
