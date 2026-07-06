using System.Text;

namespace FinancialServices.Api.Analysis;

/// <summary>Credit-rating data providers reconciled by Prism (Moody's + Morningstar DBRS real APIs; MSCI = labeled-synthetic slot).</summary>
public enum Provider
{
    Moodys,
    MorningstarDbrs,
    Msci
}

/// <summary>
/// Deterministic credit-rating notch ladder — core principle P2 (plain C#, no LLM, no network).
/// Every provider label is mapped onto one canonical 1–21 integer scale (1 = AAA/Aaa … 21 = C,
/// higher = weaker) so a "3-notch gap" is precise, reproducible, and auditable.
/// </summary>
public static class NotchLadder
{
    // Canonical long-term ladder (S&P/Fitch style anchor). 1 = AAA … 21 = C.
    private static readonly string[] Canonical =
    {
        "AAA", "AA+", "AA", "AA-", "A+", "A", "A-", "BBB+", "BBB", "BBB-",
        "BB+", "BB", "BB-", "B+", "B", "B-", "CCC+", "CCC", "CCC-", "CC", "C"
    };

    // Provider-specific label → canonical notch. DBRS uses "(high)/(mid)/(low)"; Moody's uses Aaa/Aa1…
    private static readonly Dictionary<string, int> Map = BuildMap();

    /// <summary>Resolve a provider label to its canonical notch (1–21). Tolerant of case and whitespace.</summary>
    /// <exception cref="ArgumentException">The label is null, blank, or not a known rating grade.</exception>
    public static int ToNotch(string label)
    {
        // Fail-loud on null/blank (P1) — a clear ArgumentException, never a NullReferenceException.
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        return Map.TryGetValue(Normalize(label), out var notch)
            ? notch
            // An unrecognized grade is a bad *value*, not a numeric range violation → ArgumentException.
            : throw new ArgumentException($"Unknown rating '{label}'.", nameof(label));
    }

    /// <summary>Canonical S&amp;P-style label for a notch (1–21).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The notch is outside the valid 1–21 range.</exception>
    public static string ToLabel(int notch) =>
        notch is < 1 or > 21
            ? throw new ArgumentOutOfRangeException(nameof(notch), notch, "Notch must be 1..21.")
            : Canonical[notch - 1];

    /// <summary>Signed notch gap b − a (positive = b is weaker/lower rated than a).</summary>
    public static int Gap(string a, string b) => ToNotch(b) - ToNotch(a);

    /// <summary>True when a and b sit on opposite sides of the investment-grade / high-yield line.</summary>
    public static bool CrossesIgHyBoundary(string a, string b) =>
        (ToNotch(a) <= 10) != (ToNotch(b) <= 10); // BBB- (10) is the IG floor

    // Tolerant match key (D6): uppercase and strip ALL whitespace so "A (LOW)", "A(LOW)", and
    // "A  (low)" resolve to the same notch. Public labels stay canonical (see ToLabel).
    private static string Normalize(string label)
    {
        var builder = new StringBuilder(label.Length);
        foreach (var ch in label)
        {
            if (!char.IsWhiteSpace(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static Dictionary<string, int> BuildMap()
    {
        // Ordinal comparer is safe and deterministic because every key is pre-normalized to
        // uppercase, whitespace-free form (D5). Indexer assignment is idempotent for the handful
        // of value-consistent overlaps (e.g. "AAA", "C") shared across alias sets (D4).
        var map = new Dictionary<string, int>(StringComparer.Ordinal);

        // 1) Canonical S&P/Fitch ladder (the anchor).
        for (var i = 0; i < Canonical.Length; i++)
        {
            map[Normalize(Canonical[i])] = i + 1;
        }

        // 2) Morningstar DBRS "(high)/(mid)/(low)" grades: (high) = one notch stronger,
        //    (mid) = the bare family notch (D2), (low) = one notch weaker.
        (string Family, int MidNotch)[] dbrsFamilies =
        {
            ("AA", 3), ("A", 6), ("BBB", 9), ("BB", 12), ("B", 15), ("CCC", 18)
        };
        foreach (var (family, midNotch) in dbrsFamilies)
        {
            map[Normalize($"{family} (high)")] = midNotch - 1;
            // "(mid)" is an INPUT alias only (bare family notch); it is never emitted by ToLabel as a display label.
            map[Normalize($"{family} (mid)")] = midNotch;
            map[Normalize($"{family} (low)")] = midNotch + 1;
        }

        // 3) Moody's Aaa/Aa1…C grades (1:1 rank cross-walk onto the 21 canonical notches, D3).
        (string Label, int Notch)[] moodyGrades =
        {
            ("Aaa", 1), ("Aa1", 2), ("Aa2", 3), ("Aa3", 4),
            ("A1", 5), ("A2", 6), ("A3", 7),
            ("Baa1", 8), ("Baa2", 9), ("Baa3", 10),
            ("Ba1", 11), ("Ba2", 12), ("Ba3", 13),
            ("B1", 14), ("B2", 15), ("B3", 16),
            ("Caa1", 17), ("Caa2", 18), ("Caa3", 19),
            ("Ca", 20), ("C", 21)
        };
        foreach (var (label, notch) in moodyGrades)
        {
            map[Normalize(label)] = notch;
        }

        return map;
    }
}
