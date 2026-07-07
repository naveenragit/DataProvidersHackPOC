using System.Text.RegularExpressions;

namespace FinancialServices.Api.Agents;

/// <summary>
/// The P2 post-validator that keeps the LLM honest (spec §C). A narration is <b>accepted only if</b>:
/// <list type="number">
///   <item>it cites <b>every</b> required evidence reference verbatim, and</item>
///   <item>every numeric/date token it contains also appears in the grounding facts we supplied
///     (narration numbers ⊆ grounding numbers).</item>
/// </list>
/// Otherwise it is <b>dropped</b> (returns <see cref="string.Empty"/>) and the caller keeps the
/// authoritative deterministic value. This makes it mechanically impossible for the model to alter,
/// invent, or contradict a deterministic number — the guarantee behind
/// <c>architecturalPlan/00-core-principles.md</c> P2. Pure (no I/O), so it is trivially unit-testable.
/// </summary>
public static partial class NarrationGuard
{
    // A numeric token: a run of digits optionally joined by a single separator to more digits, so dates
    // (2025-11-05), decimals (3.0), ratios (10:1) and thousands (1,234) survive as one token.
    [GeneratedRegex(@"\d+(?:[.,:/\-]\d+)*")]
    private static partial Regex NumericToken();

    // Prohibited trading-advice vocabulary (P4). Word-boundary + case-insensitive so "Holdings",
    // "bondholder" and "traded" are safe. The narration is UI copy — a single hit drops it entirely.
    [GeneratedRegex(@"\b(?:buy|sell|hold|recommend|allocate|trade|alpha|signal)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProhibitedVocabulary();

    /// <summary>
    /// Returns the trimmed narration if it cites every <paramref name="requiredRefs"/> entry and uses no
    /// number absent from <paramref name="groundingText"/>; otherwise returns <see cref="string.Empty"/>.
    /// </summary>
    public static string Sanitize(string? narrative, string groundingText, IReadOnlyList<string> requiredRefs)
    {
        if (string.IsNullOrWhiteSpace(narrative))
        {
            return string.Empty;
        }

        string text = narrative.Trim();

        // (0) P4: the narration is rendered as UI copy, so it must never contain trading-advice
        // vocabulary even though the agent instructions forbid it (instructions are not enforcement).
        if (ProhibitedVocabulary().IsMatch(text))
        {
            return string.Empty;
        }

        // (1) Every required evidence reference must be cited verbatim (case-insensitive).
        foreach (string reference in requiredRefs)
        {
            if (!string.IsNullOrEmpty(reference) &&
                !text.Contains(reference, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
        }

        // (2) Every numeric/date token in the narration must be present in the grounding facts.
        HashSet<string> allowed = NumericToken().Matches(groundingText)
            .Select(match => Normalize(match.Value))
            .ToHashSet(StringComparer.Ordinal);

        foreach (Match match in NumericToken().Matches(text))
        {
            if (!allowed.Contains(Normalize(match.Value)))
            {
                return string.Empty;
            }
        }

        return text;
    }

    // Drop thousands separators so "1,234" and "1234" compare equal; date/decimal structure is kept.
    private static string Normalize(string token) => token.Replace(",", "", StringComparison.Ordinal);
}
